using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Asp.Versioning.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using RedisRateLimiting;
using StackExchange.Redis;
using Wallow.Api.HealthChecks;
using Wallow.Api.Middleware;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Api.Settings;
using Wallow.Shared.Infrastructure.Core.Resilience;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Infrastructure.Configuration;

namespace Wallow.Api.Extensions;

internal static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Tag identifying operations excluded from the public OpenAPI document.
    /// </summary>
    private const string TestSupportTagName = "Test Support";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Share runtime problem formatting and OpenAPI problem conventions.
        services.AddWallowProblemDetails();


        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Shared catalogs remain available even when optional modules are disabled.
        services.AddErrorCatalog(typeof(SharedErrors));
        services.AddErrorCatalog(typeof(SettingsErrors));

        // Anchor XML-comment interception in this assembly for the named v1 document.
        // The versioning package registers its call elsewhere; AV0029 is suppressed in the project.
        // Additional document versions need matching local anchors.
        services.AddOpenApi("v1");

        // Resolve connection strings lazily so test hosts can replace them.
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

            // Exclude dead letters from readiness while reporting them on the full health endpoint.
            .AddCheck<WolverineDeadLetterQueueHealthCheck>("wolverine-dlq", tags: ["messaging"])
            .AddCheck("startup", () => HealthCheckResult.Healthy(),
                tags: ["startup"])
            .AddCheck("startup-ready", () => HealthCheckResult.Healthy(),
                tags: ["infrastructure", "ready"]);


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

            options.OnRejected = async (context, _) =>
            {
                HttpContext httpContext = context.HttpContext;
                httpContext.Response.StatusCode = 429;

                // Read the Redis limiter metadata names to populate rejection headers.
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

                IProblemDetailsService problemDetailsService =
                    httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                // Keep Retry-After in the header and use the catalog problem detail.
                await problemDetailsService.TryWriteProblemAsync(
                    httpContext,
                    SharedErrors.RateLimitExceeded);
            };
        });

        return services;
    }

    private static RateLimitingOptions GetRateLimits(HttpContext httpContext)
    {
        return httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
    }

    // Prefix counters by policy; use user id, then remote IP, then unknown.
    private static string GetUserPartitionKey(HttpContext httpContext, string policy)
    {
        string? userId = httpContext.User.GetUserId();
        return $"{policy}:{userId ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    // API-key authentication populates ITenantContext even without a request item.
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
    /// Applies the host transformer pipeline to a versioned OpenAPI document.
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
        document.AddDocumentTransformer((doc, context, _) =>
            TransformDocumentErrorCodes(doc, context.ApplicationServices.GetRequiredService<ErrorCatalog>()));
        document.AddDocumentTransformer((doc, _, _) => TransformDocumentProblemContentTypes(doc));
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
    /// Removes empty operation summaries/descriptions and concrete parameter descriptions.
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

    /// <summary>
    /// Schema name for the aggregated catalog error-code enum.
    /// </summary>
    internal const string ErrorCodeSchemaName = "ErrorCode";

    /// <summary>
    /// Exports catalog codes and descriptions, then adds problem-contract fields to
    /// schemas whose names end in ProblemDetails.
    /// </summary>
    internal static Task TransformDocumentErrorCodes(OpenApiDocument document, ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(catalog);

        OpenApiComponents components = document.Components ??= new OpenApiComponents();
        components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        JsonArray descriptions = [];
        List<JsonNode> codes = [];
        foreach (ErrorCatalogEntry entry in catalog.Entries)
        {
            codes.Add(JsonValue.Create(entry.Code));
            descriptions.Add(JsonValue.Create(entry.DefaultMessage));
        }

        components.Schemas[ErrorCodeSchemaName] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Machine-readable code identifying why a request failed. Each code has " +
                "exactly one owning catalog and a fixed HTTP status.",
            Enum = codes,
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                ["x-enum-descriptions"] = new JsonNodeExtension(descriptions)
            }
        };

        // Match the schema naming convention used for problem responses.
        List<string> problemSchemaNames = components.Schemas.Keys
            .Where(name => name.EndsWith("ProblemDetails", StringComparison.Ordinal))
            .ToList();
        foreach (string schemaName in problemSchemaNames)
        {
            if (components.Schemas[schemaName] is not OpenApiSchema problemDetails)
            {
                continue;
            }

            problemDetails.Properties ??= new Dictionary<string, IOpenApiSchema>();
            problemDetails.Properties[ProblemContract.CodeMember] =
                new OpenApiSchemaReference(ErrorCodeSchemaName, document);
            problemDetails.Properties[ProblemContract.TraceIdMember] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Correlation id of the failed request, for support and log lookup."
            };
            problemDetails.Properties.Remove("instance");

            problemDetails.Required ??= new HashSet<string>(StringComparer.Ordinal);
            foreach (string member in ProblemContract.AlwaysPresentMembers)
            {
                problemDetails.Required.Add(member);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Uses application/problem+json for numeric error responses referencing a schema
    /// whose name ends in ProblemDetails.
    /// </summary>
    internal static Task TransformDocumentProblemContentTypes(OpenApiDocument document)
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
                if (operation.Responses is null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, IOpenApiResponse> response in operation.Responses)
                {
                    if (!IsErrorStatus(response.Key) || response.Value is not OpenApiResponse { Content: { Count: > 0 } content })
                    {
                        continue;
                    }

                    OpenApiMediaType? problemBody = content.Values
                        .OfType<OpenApiMediaType>()
                        .FirstOrDefault(media => media.Schema is OpenApiSchemaReference reference
                            && reference.Reference.Id?.EndsWith("ProblemDetails", StringComparison.Ordinal) == true);
                    if (problemBody is null)
                    {
                        continue;
                    }

                    content.Clear();
                    content[ProblemContract.ContentType] = problemBody;
                }
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsErrorStatus(string responseKey) =>
        int.TryParse(responseKey, NumberStyles.None, CultureInfo.InvariantCulture, out int status)
        && status >= StatusCodes.Status400BadRequest;

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

        // Keep operation ids tied to C# method names even when ActionName changes.
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
