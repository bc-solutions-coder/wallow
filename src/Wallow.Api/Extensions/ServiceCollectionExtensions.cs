using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Wallow.Api.HealthChecks;
using Wallow.Api.Middleware;
using Wallow.Shared.Infrastructure.Core.Resilience;
using Wallow.Shared.Kernel.Configuration;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RedisRateLimiting;
using Serilog;
using StackExchange.Redis;

namespace Wallow.Api.Extensions;

internal static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
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
            options.AddOperationTransformer((operation, context, _) =>
                TransformOperationSecurity(operation, context));
            options.AddOperationTransformer((operation, context, _) =>
                TransformOperationModuleTag(operation, context));
        });

        // CORS
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        ServiceUrlsOptions serviceUrls = configuration.GetSection(ServiceUrlsOptions.SectionName).Get<ServiceUrlsOptions>()
            ?? new ServiceUrlsOptions();

        if (!environment.IsDevelopment() && allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must be configured with at least one origin in non-Development environments.");
        }

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
                    .AllowAnyHeader()
                    .AllowCredentials();
            });

            // Named policy for development (explicit origins for SignalR)
            options.AddPolicy("Development", policy =>
            {
                policy
                    .WithOrigins(serviceUrls.ApiUrl, serviceUrls.AuthUrl, serviceUrls.WebUrl)
                    .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
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

            options.AddPolicy("scim", httpContext =>
                RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    GetTenantPartitionKey(httpContext),
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = RateLimitDefaults.ScimPermitLimit,
                        Window = TimeSpan.FromMinutes(RateLimitDefaults.ScimWindowMinutes)
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
        string? userId = httpContext.User.FindFirst("sub")?.Value;
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

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        string serviceName = configuration["OpenTelemetry:ServiceName"] ?? "Wallow";
        string? otlpGrpcEndpoint = configuration["OpenTelemetry:OtlpGrpcEndpoint"];

        if (!environment.IsDevelopment() && string.IsNullOrEmpty(otlpGrpcEndpoint))
        {
            throw new InvalidOperationException(
                "OpenTelemetry:OtlpGrpcEndpoint must be configured in non-Development environments. " +
                "Set the 'OpenTelemetry:OtlpGrpcEndpoint' configuration value.");
        }

        Log.Information("OpenTelemetry OTLP endpoint: {OtlpEndpoint}", otlpGrpcEndpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceNamespace: configuration["Logging:NamespacePrefix"] ?? "Wallow",
                    serviceVersion: typeof(ServiceCollectionExtensions).Assembly
                        .GetName().Version?.ToString() ?? "1.0.0")
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("deployment.environment", environment.EnvironmentName),
                    new KeyValuePair<string, object>("service.instance.id", Environment.MachineName)
                ]))
            .WithTracing(tracing =>
            {
                double samplingRatio = configuration.GetValue<double?>("Observability:TraceSamplingRatio")
                    ?? (environment.IsDevelopment() ? 1.0 : 0.1);

                tracing
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = FilterTelemetryRequest;
                    })
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddHttpClientInstrumentation(options =>
                    {
                        string s3Endpoint = (configuration["Storage:S3:Endpoint"] ?? "").TrimEnd('/');

                        options.EnrichWithHttpRequestMessage = (activity, message) =>
                        {
                            string requestUrl = message.RequestUri?.GetLeftPart(UriPartial.Authority) ?? "";

                            if (!string.IsNullOrEmpty(s3Endpoint) &&
                                requestUrl.Equals(new Uri(s3Endpoint).GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                            {
                                activity.SetTag("http.provider", "s3");
                            }
                        };
                    })
                    .AddRedisInstrumentation()
                    .AddSource("Wolverine")
                    .AddSource("Wallow")
                    .AddSource("Wallow.*");

                if (!string.IsNullOrEmpty(otlpGrpcEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpGrpcEndpoint);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetExemplarFilter(ExemplarFilterType.TraceBased)
                    .AddAspNetCoreInstrumentation()
                    .AddProcessInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("Wolverine")
                    .AddMeter("Wallow")
                    .AddMeter("Wallow.*")
                    .AddMeter("Microsoft.AspNetCore.Authentication")
                    .AddMeter("Microsoft.AspNetCore.Authorization")
                    .AddMeter("Microsoft.Extensions.Http.Resilience");

                if (!string.IsNullOrEmpty(otlpGrpcEndpoint))
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpGrpcEndpoint);
                    });
                }
            });

        return services;
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
