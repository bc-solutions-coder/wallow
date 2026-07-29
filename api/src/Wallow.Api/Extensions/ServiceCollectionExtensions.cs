using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using RedisRateLimiting;
using StackExchange.Redis;
using Wallow.Api.HealthChecks;
using Wallow.Api.Middleware;
using Wallow.Shared.Infrastructure.Core.Resilience;
using Wallow.Shared.Kernel.Extensions;
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

        // OpenAPI documentation (Scalar)
        string appName = configuration["Branding:AppName"] ?? "Wallow";
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) => TransformDocumentInfo(document, appName));
            options.AddDocumentTransformer((document, _, _) => TransformDocumentSecurity(document));
            options.AddDocumentTransformer((document, _, _) => TransformDocumentExcludeTestSupport(document));
            options.AddOperationTransformer((operation, context, _) =>
                TransformOperationSecurity(operation, context));
            options.AddOperationTransformer((operation, context, _) =>
                TransformOperationModuleTag(operation, context));
            options.AddOperationTransformer((operation, context, _) =>
                TransformOperationId(operation, context));
        });

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

    public static IServiceCollection AddWallowRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

            options.AddPolicy("auth", httpContext =>
                RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    GetTenantPartitionKey(httpContext),
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = RateLimitDefaults.AuthPermitLimit,
                        Window = TimeSpan.FromMinutes(RateLimitDefaults.AuthWindowMinutes)
                    }));

            options.AddPolicy("upload", httpContext =>
                RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    GetTenantPartitionKey(httpContext),
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = RateLimitDefaults.UploadPermitLimit,
                        Window = TimeSpan.FromHours(RateLimitDefaults.UploadWindowHours)
                    }));

            options.AddPolicy("developer-app-registration", httpContext =>
                RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    GetUserPartitionKey(httpContext),
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = RateLimitDefaults.DeveloperAppRegistrationPermitLimit,
                        Window = TimeSpan.FromHours(RateLimitDefaults.DeveloperAppRegistrationWindowHours)
                    }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    GetTenantPartitionKey(httpContext),
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = RateLimitDefaults.GlobalPermitLimit,
                        Window = TimeSpan.FromHours(RateLimitDefaults.GlobalWindowHours)
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                HttpContext httpContext = context.HttpContext;
                httpContext.Response.StatusCode = 429;
                httpContext.Response.ContentType = "application/problem+json";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    httpContext.Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString();
                }

                if (context.Lease.TryGetMetadata(MetadataName.ReasonPhrase, out string? reason))
                {
                    httpContext.Response.Headers["X-RateLimit-Limit"] = reason;
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

                await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            };
        });

        return services;
    }

    private static string GetUserPartitionKey(HttpContext httpContext)
    {
        string? userId = httpContext.User.GetUserId();
        return userId ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string GetTenantPartitionKey(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue("TenantId", out object? tenantId) && tenantId is string tenantIdStr)
        {
            return tenantIdStr;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    internal static Task TransformDocumentInfo(OpenApiDocument document, string appName)
    {
        document.Info = new OpenApiInfo
        {
            Title = $"{appName} API",
            Version = "v1",
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
