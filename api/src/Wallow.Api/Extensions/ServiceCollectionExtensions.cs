using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Asp.Versioning.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using RedisRateLimiting;
using StackExchange.Redis;
using Wallow.Api.HealthChecks;
using Wallow.Api.Middleware;
using Wallow.Shared.Infrastructure.Core.Resilience;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Infrastructure.Configuration;

namespace Wallow.Api.Extensions;

internal static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Tag applied by <c>TestSupportController</c>; its operations are internal-only scaffolding
    /// and must never reach the public v1 document or the generated SDK client.
    /// </summary>
    private const string TestSupportTagName = "Test Support";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Problem Details
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["api"] = "Wallow";
                context.ProblemDetails.Extensions["version"] = "1.0.0";
            };
        });

        // Global Exception Handler
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // XML documentation comments for the "v1" OpenAPI document. The framework's XML
        // comment support is a compile-time interceptor that attaches to user-code
        // AddOpenApi call sites; Asp.Versioning's versioned AddOpenApi call lives inside
        // its own assembly, where the interceptor cannot reach. This call is deliberately
        // redundant as a registration (AV0029, suppressed via NoWarn in the csproj — the
        // interceptor re-emits the call in generated source, out of reach of a pragma)
        // but is the anchor the interceptor needs: it configures the same named
        // OpenApiOptions ("v1" = the version's group name) the versioned pipeline
        // resolves. The package's runtime XmlCommentsTransformer is not a substitute —
        // it renders <see cref> references as empty text and misattributes method
        // summaries to parameter descriptions. A future v2 needs its own call.
        services.AddOpenApi("v1");

        // Health checks - connection strings resolved lazily via factories
        // to support Testcontainers dynamic connection strings
        IHealthChecksBuilder healthChecks = services.AddHealthChecks()
            .AddNpgSql(
                sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!,
                name: "postgresql", tags: ["db", "ready"])
            .AddHangfire(options =>
            {
                options.MinimumAvailableServers = 1;
            }, name: "hangfire", tags: ["jobs", "ready"])
            .AddRedis(
                sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")
                    ?? throw new InvalidOperationException("Redis connection string not configured"),
                name: "redis",
                tags: ["infrastructure", "ready"])

            // Not tagged "ready" on purpose: a poison message in the DLQ must degrade /health
            // without failing readiness probes and restart-looping the container (Wallow-qi90.2).
            .AddCheck<WolverineDeadLetterQueueHealthCheck>("wolverine-dlq", tags: ["messaging"])
            .AddCheck("startup", () => HealthCheckResult.Healthy(),
                tags: ["startup"])
            .AddCheck("startup-ready", () => HealthCheckResult.Healthy(),
                tags: ["infrastructure", "ready"]);

        // S3 health check - only when S3 storage provider is configured
        StorageOptions storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
                                        ?? new StorageOptions();
        if (storageOptions.Provider == StorageProvider.S3)
        {
            services.AddHealthChecks()
                .AddCheck<S3HealthCheck>("s3", tags: ["storage", "ready"]);
        }

        services.AddHttpClient("HealthChecks")
            .AddWallowResilienceHandler("health-check");

        services.AddSingleton<IHealthCheckPublisher, HealthCheckMetricsPublisher>();

        return services;
    }

    public static IServiceCollection AddWallowRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

            options.AddPolicy("auth", httpContext =>
                RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    GetTenantPartitionKey(httpContext, "auth"),
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = GetRateLimits(httpContext).Auth.PermitLimit,
                        Window = TimeSpan.FromMinutes(GetRateLimits(httpContext).Auth.WindowMinutes)
                    }));

            options.AddPolicy("upload", httpContext =>
                RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    GetTenantPartitionKey(httpContext, "upload"),
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = GetRateLimits(httpContext).Upload.PermitLimit,
                        Window = TimeSpan.FromHours(GetRateLimits(httpContext).Upload.WindowHours)
                    }));

            options.AddPolicy("registration", httpContext =>
                RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    GetUserPartitionKey(httpContext, "registration"),
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = GetRateLimits(httpContext).Registration.PermitLimit,
                        Window = TimeSpan.FromHours(GetRateLimits(httpContext).Registration.WindowHours)
                    }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    GetTenantPartitionKey(httpContext, "global"),
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = GetRateLimits(httpContext).Global.PermitLimit,
                        Window = TimeSpan.FromHours(GetRateLimits(httpContext).Global.WindowHours)
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                HttpContext httpContext = context.HttpContext;
                httpContext.Response.StatusCode = 429;

                // RedisRateLimiting leases publish the library's RateLimitMetadataName entries,
                // not the framework's MetadataName ones — reading the framework names here
                // silently yields no headers at all.
                if (context.Lease.TryGetMetadata(RateLimitMetadataName.RetryAfter, out int retryAfterSeconds))
                {
                    httpContext.Response.Headers["Retry-After"] =
                        retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                }

                if (context.Lease.TryGetMetadata(RateLimitMetadataName.Limit, out string? limit))
                {
                    httpContext.Response.Headers["X-RateLimit-Limit"] = limit;
                }

                httpContext.Response.Headers["X-RateLimit-Remaining"] = "0";

                ProblemDetails problemDetails = new()
                {
                    Status = 429,
                    Type = "about:blank",
                    Title = "Too Many Requests",
                    Detail = "Rate limit exceeded. Please retry after the duration indicated in the Retry-After header.",
                    Instance = httpContext.Request.Path
                };

                // WriteAsJsonAsync stamps application/json unless the media type is passed
                // explicitly, silently overwriting a ContentType set beforehand.
                await httpContext.Response.WriteAsJsonAsync(
                    problemDetails,
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken);
            };
        });

        return services;
    }

    private static RateLimitingOptions GetRateLimits(HttpContext httpContext)
    {
        return httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
    }

    // Partition keys are prefixed with the policy name so two policies keyed on the same
    // principal never share a Redis counter. UseRateLimiter runs after authentication and
    // tenant resolution (see Program.cs), so a user or tenant is genuinely available here;
    // the IP fallback only covers anonymous traffic.
    private static string GetUserPartitionKey(HttpContext httpContext, string policy)
    {
        string? userId = httpContext.User.GetUserId();
        return $"{policy}:{userId ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    // ITenantContext rather than HttpContext.Items: TenantResolutionMiddleware sets both,
    // but ApiKeyAuthenticationMiddleware sets only the context, so this covers both paths.
    private static string GetTenantPartitionKey(HttpContext httpContext, string policy)
    {
        ITenantContext tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
        if (tenantContext.IsResolved)
        {
            return $"{policy}:{tenantContext.TenantId.Value}";
        }

        return GetUserPartitionKey(httpContext, policy);
    }

    /// <summary>
    /// Configures one versioned OpenAPI document. Asp.Versioning registers a document per
    /// discovered API version, named by its ApiExplorer group name (e.g. "v1"), and invokes
    /// this callback once for each; the same transformer pipeline applies to every version.
    /// </summary>
    internal static void ConfigureVersionedOpenApiDocument(
        VersionedOpenApiOptions options,
        IConfiguration configuration)
    {
        string appName = configuration["Branding:AppName"] ?? "Wallow";
        string version = options.Description.GroupName;
        OpenApiOptions document = options.Document;
        document.AddDocumentTransformer((doc, _, _) => TransformDocumentInfo(doc, appName, version));
        document.AddDocumentTransformer((doc, _, _) => TransformDocumentSecurity(doc));
        document.AddDocumentTransformer((doc, _, _) => TransformDocumentExcludeTestSupport(doc));
        document.AddDocumentTransformer((doc, _, _) => TransformDocumentScrubEmptyPlaceholders(doc));
        document.AddOperationTransformer((operation, context, _) =>
            TransformOperationSecurity(operation, context));
        document.AddOperationTransformer((operation, context, _) =>
            TransformOperationModuleTag(operation, context));
        document.AddOperationTransformer((operation, context, _) =>
            TransformOperationId(operation, context));
    }

    internal static Task TransformDocumentInfo(OpenApiDocument document, string appName, string version)
    {
        document.Info = new OpenApiInfo
        {
            Title = $"{appName} API",
            Version = version,
            Description = "A modular monolith API built with Clean Architecture, DDD, and CQRS",
            Contact = new OpenApiContact
            {
                Name = appName
            }
        };
        return Task.CompletedTask;
    }

    internal static Task TransformDocumentSecurity(OpenApiDocument document)
    {
        OpenApiComponents components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token"
        };

        OpenApiSecuritySchemeReference securitySchemeRef = new OpenApiSecuritySchemeReference("Bearer", document);
        document.Security = [new OpenApiSecurityRequirement { [securitySchemeRef] = [] }];

        return Task.CompletedTask;
    }

    internal static Task TransformDocumentExcludeTestSupport(OpenApiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Paths is not null)
        {
            List<string> emptiedPaths = [];

            foreach (KeyValuePair<string, IOpenApiPathItem> path in document.Paths)
            {
                Dictionary<HttpMethod, OpenApiOperation>? operations = path.Value.Operations;
                if (operations is null)
                {
                    continue;
                }

                List<HttpMethod> testSupportMethods = operations
                    .Where(operation => IsTestSupportOperation(operation.Value))
                    .Select(operation => operation.Key)
                    .ToList();

                foreach (HttpMethod method in testSupportMethods)
                {
                    operations.Remove(method);
                }

                if (operations.Count == 0)
                {
                    emptiedPaths.Add(path.Key);
                }
            }

            foreach (string emptiedPath in emptiedPaths)
            {
                document.Paths.Remove(emptiedPath);
            }
        }

        if (document.Tags is not null)
        {
            List<OpenApiTag> testSupportTags = document.Tags
                .Where(tag => string.Equals(tag.Name, TestSupportTagName, StringComparison.Ordinal))
                .ToList();

            foreach (OpenApiTag testSupportTag in testSupportTags)
            {
                document.Tags.Remove(testSupportTag);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Asp.Versioning's ApiExplorer transformer stamps empty-string summaries on operations and
    /// empty-string descriptions on parameters that have no XML docs. Empty strings are noise in
    /// the contract (and churn in the committed snapshot), so drop them back to null.
    /// </summary>
    internal static Task TransformDocumentScrubEmptyPlaceholders(OpenApiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        foreach (IOpenApiPathItem pathItem in document.Paths.Values)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (OpenApiOperation operation in pathItem.Operations.Values)
            {
                if (operation.Summary is { Length: 0 })
                {
                    operation.Summary = null;
                }

                if (operation.Description is { Length: 0 })
                {
                    operation.Description = null;
                }

                if (operation.Parameters is null)
                {
                    continue;
                }

                foreach (IOpenApiParameter parameter in operation.Parameters)
                {
                    if (parameter is OpenApiParameter { Description.Length: 0 } concreteParameter)
                    {
                        concreteParameter.Description = null;
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsTestSupportOperation(OpenApiOperation operation) =>
        operation.Tags?.Any(tag => string.Equals(tag.Name, TestSupportTagName, StringComparison.Ordinal)) == true;

    internal static Task TransformOperationSecurity(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context)
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
        bool hasAllowAnonymous = metadata
            .OfType<AllowAnonymousAttribute>()
            .Any();

        if (hasAllowAnonymous)
        {
            operation.Security?.Clear();
        }

        return Task.CompletedTask;
    }

    internal static Task TransformOperationModuleTag(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context)
    {
        // If the controller already has an explicit [Tags] attribute, don't override
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<TagsAttribute>().Any())
        {
            return Task.CompletedTask;
        }

        string? ns = (context.Description.ActionDescriptor as ControllerActionDescriptor)
            ?.ControllerTypeInfo.Namespace;

        if (ns is not null)
        {
            Match match = ModuleNamePattern().Match(ns);
            if (match.Success)
            {
                string moduleName = match.Groups[1].Value;
                operation.Tags = new HashSet<OpenApiTagReference>();
                operation.Tags.Add(new OpenApiTagReference(moduleName));
            }
        }

        return Task.CompletedTask;
    }

    internal static Task TransformOperationId(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        // MethodInfo.Name, not ActionName: an [ActionName] override renames the route, not the
        // C# method, and the generated SDK reads better keyed to the method the API actually has.
        if (context.Description.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            operation.OperationId = $"{descriptor.ControllerName}{descriptor.MethodInfo.Name}";
        }

        return Task.CompletedTask;
    }

    [GeneratedRegex(@"^Wallow\.(\w+)\.Api\b", RegexOptions.NonBacktracking)]
    private static partial Regex ModuleNamePattern();

    internal static bool FilterTelemetryRequest(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "";
        return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/alive", StringComparison.OrdinalIgnoreCase);
    }
}
