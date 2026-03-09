using System.Threading.RateLimiting;
using Foundry.Api.HealthChecks;
using Foundry.Api.Middleware;
using Foundry.Storage.Domain.Enums;
using Foundry.Storage.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Foundry.Shared.Infrastructure.Core.Resilience;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using RabbitMQ.Client;
using Serilog;

namespace Foundry.Api.Extensions;

internal static class ServiceCollectionExtensions
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
                context.ProblemDetails.Extensions["api"] = "Foundry";
                context.ProblemDetails.Extensions["version"] = "1.0.0";
            };
        });

        // Global Exception Handler
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // OpenAPI documentation (Scalar)
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) => TransformDocumentInfo(document));
            options.AddDocumentTransformer((document, _, _) => TransformDocumentSecurity(document));
            options.AddOperationTransformer((operation, context, _) =>
                TransformOperationSecurity(operation, context));
        });

        // CORS
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

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
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });

            // Named policy for development (explicit localhost origins for SignalR)
            options.AddPolicy("Development", policy =>
            {
                policy
                    .WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:5000")
                    .AllowAnyMethod()
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
            .AddCheck<KeycloakHealthCheck>("keycloak", tags: ["infrastructure", "ready"]);

        // RabbitMQ health check — only when RabbitMQ transport is active
        string transport = configuration.GetValue<string>("ModuleMessaging:Transport") ?? "InMemory";
        if (transport.Equals("RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            healthChecks.AddRabbitMQ(sp =>
            {
                IConfiguration config = sp.GetRequiredService<IConfiguration>();
                string rabbitHost = config["RabbitMQ:Host"]!;
                string rabbitUser = config["RabbitMQ:Username"]!;
                string rabbitPass = config["RabbitMQ:Password"]!;
                Uri rabbitUri = new Uri($"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:5672");
                ConnectionFactory factory = new() { Uri = rabbitUri };
                return factory.CreateConnectionAsync();
            }, name: "rabbitmq", tags: ["messaging", "ready"]);
        }

        // S3 health check - only when S3 storage provider is configured
        StorageOptions storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
                                        ?? new StorageOptions();
        if (storageOptions.Provider == StorageProvider.S3)
        {
            services.AddHealthChecks()
                .AddCheck<S3HealthCheck>("s3", tags: ["storage", "ready"]);
        }

        services.AddHttpClient("HealthChecks")
            .AddFoundryResilienceHandler("health-check");

        services.AddSingleton<IHealthCheckPublisher, HealthCheckMetricsPublisher>();

        return services;
    }

    public static IServiceCollection AddFoundryRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetTenantPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = RateLimitDefaults.AuthPermitLimit,
                        Window = TimeSpan.FromMinutes(RateLimitDefaults.AuthWindowMinutes),
                        QueueLimit = 0
                    }));

            options.AddPolicy("upload", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetTenantPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = RateLimitDefaults.UploadPermitLimit,
                        Window = TimeSpan.FromHours(RateLimitDefaults.UploadWindowHours),
                        QueueLimit = 0
                    }));

            options.AddPolicy("scim", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetTenantPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = RateLimitDefaults.ScimPermitLimit,
                        Window = TimeSpan.FromMinutes(RateLimitDefaults.ScimWindowMinutes),
                        QueueLimit = 0
                    }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetTenantPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = RateLimitDefaults.GlobalPermitLimit,
                        Window = TimeSpan.FromHours(RateLimitDefaults.GlobalWindowHours),
                        QueueLimit = 0
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
        string serviceName = configuration["OpenTelemetry:ServiceName"] ?? "Foundry";
        string? otlpGrpcEndpoint = configuration["OpenTelemetry:OtlpGrpcEndpoint"];

        if (!environment.IsDevelopment() && string.IsNullOrEmpty(otlpGrpcEndpoint))
        {
            throw new InvalidOperationException(
                "OpenTelemetry:OtlpGrpcEndpoint must be configured in non-Development environments. " +
                "Set the 'OpenTelemetry:OtlpGrpcEndpoint' configuration value.");
        }

        otlpGrpcEndpoint ??= "http://localhost:4317";
        Log.Information("OpenTelemetry OTLP endpoint: {OtlpEndpoint}", otlpGrpcEndpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceNamespace: "Foundry",
                    serviceVersion: typeof(ServiceCollectionExtensions).Assembly
                        .GetName().Version?.ToString() ?? "1.0.0")
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment", environment.EnvironmentName),
                    new("service.instance.id", Environment.MachineName)
                }))
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
                        string keycloakBaseUrl = (configuration["Keycloak:auth-server-url"] ?? "").TrimEnd('/');
                        string s3Endpoint = (configuration["Storage:S3:Endpoint"] ?? "").TrimEnd('/');

                        options.EnrichWithHttpRequestMessage = (activity, message) =>
                        {
                            string requestUrl = message.RequestUri?.GetLeftPart(UriPartial.Authority) ?? "";

                            if (!string.IsNullOrEmpty(keycloakBaseUrl) &&
                                requestUrl.Equals(new Uri(keycloakBaseUrl).GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                            {
                                activity.SetTag("http.provider", "keycloak");
                            }
                            else if (!string.IsNullOrEmpty(s3Endpoint) &&
                                     requestUrl.Equals(new Uri(s3Endpoint).GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                            {
                                activity.SetTag("http.provider", "s3");
                            }
                        };
                    })
                    .AddRedisInstrumentation()
                    .AddSource("Wolverine")
                    .AddSource("Foundry")
                    .AddSource("Foundry.*")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpGrpcEndpoint);
                    });
            })
            .WithMetrics(metrics => metrics
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddAspNetCoreInstrumentation()
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Wolverine")
                .AddMeter("Foundry")
                .AddMeter("Foundry.*")
                .AddMeter("Microsoft.AspNetCore.Authentication")
                .AddMeter("Microsoft.AspNetCore.Authorization")
                .AddMeter("Microsoft.Extensions.Http.Resilience")
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpGrpcEndpoint);
                }));

        return services;
    }

    internal static Task TransformDocumentInfo(OpenApiDocument document)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Foundry API",
            Version = "v1",
            Description = "A modular monolith API built with Clean Architecture, DDD, and CQRS",
            Contact = new OpenApiContact
            {
                Name = "Foundry"
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

    internal static bool FilterTelemetryRequest(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "";
        return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/alive", StringComparison.OrdinalIgnoreCase);
    }
}
