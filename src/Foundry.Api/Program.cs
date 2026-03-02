using System.Reflection;
using Asp.Versioning;
using Elsa.Extensions;
using Elsa.Workflows.Api;
using Foundry.Api.Extensions;
using Foundry.Api.Hubs;
using Foundry.Api.Jobs;
using Foundry.Api.Logging;
using Foundry.Api.Middleware;
using Foundry.Api.Services;
using Foundry.Communications.Infrastructure.Jobs;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Identity.Infrastructure.Middleware;
using Foundry.Identity.Infrastructure.MultiTenancy;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Infrastructure.Auditing;
using Foundry.Shared.Infrastructure.BackgroundJobs;
using Foundry.Shared.Infrastructure.Middleware;
using Foundry.Shared.Infrastructure.Services;
using Foundry.Shared.Infrastructure.Workflows;
using Foundry.Shared.Kernel.Extensions;
using Foundry.Shared.Kernel.Messaging;
using Hangfire;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

// Note: Using CreateLogger() instead of CreateBootstrapLogger() to support
// multiple WebApplicationFactory instances in integration tests
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Foundry API");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.With<ModuleEnricher>()
            .Enrich.WithProperty("Application", "Foundry")
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{Module}] [{TraceId}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        // OpenTelemetry log export — enabled in all environments
        {
            string otlpEndpoint = context.Configuration["OpenTelemetry:OtlpEndpoint"]
                ?? "http://localhost:4318";
            string serviceName = context.Configuration["OpenTelemetry:ServiceName"]
                ?? "Foundry";

            configuration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint + "/v1/logs";
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                    ["service.namespace"] = "Foundry",
                    ["deployment.environment"] = context.HostingEnvironment.EnvironmentName
                };
            });
        }
    });

    // ============================================================================
    // FOUNDRY MODULES
    // Explicit module registration via FoundryModules.cs
    // See docs/plans/2026-02-13-modular-monolith-consolidation.md
    // ============================================================================
    Foundry.Api.FoundryModules.AddFoundryModules(builder.Services, builder.Configuration);
    builder.Services.AddFoundryAuditing(builder.Configuration);

    // Wolverine — unified CQRS mediator + message bus
    // Use ManualOnly to prevent Wolverine from scanning native DLLs (QuestPDF/Skia)
    // which can cause crashes on macOS (exit codes 139/134)
    builder.Host.UseWolverine(opts =>
    {
        // Discover handlers in all Foundry assemblies
        foreach (Assembly? assembly in AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Foundry.", StringComparison.Ordinal) == true))
        {
            opts.Discovery.IncludeAssembly(assembly);
        }

        // Align message storage schema across all stores (PostgreSQL)
        // This prevents conflicts when PersistMessagesWithPostgresql() is used
        opts.Durability.MessageStorageSchemaName = "wolverine";

        // PostgreSQL persistence for durable outbox/inbox (disabled in Testing to prevent polling after containers disposed)
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            string pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Database connection string not configured");
            opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine");
        }

        // EF Core transaction integration — enlist Wolverine messages in EF Core transactions
        opts.UseEntityFrameworkCoreTransactions();

        // Standard error handling policies (retry, DLQ)
        opts.ConfigureStandardErrorHandling();
        opts.ConfigureMessageLogging();

        // FluentValidation middleware — validates commands before handlers
        opts.UseFluentValidation();

        // Module tagging middleware — tags Wolverine messages with foundry.module
        opts.Policies.AddMiddleware(typeof(WolverineModuleTaggingMiddleware));

        // Tenant middleware — stamps outgoing messages with TenantId and restores it on incoming
        opts.Policies.AddMiddleware(typeof(TenantStampingMiddleware));
        opts.Policies.AddMiddleware(typeof(TenantRestoringMiddleware));

        // For integration tests - discover handlers from test assemblies
        if (builder.Environment.IsEnvironment("Testing"))
        {
            string? testAssemblyName = builder.Configuration["Wolverine:TestAssembly"];
            if (!string.IsNullOrEmpty(testAssemblyName))
            {
                try
                {
                    // Try to load the assembly if not already loaded
                    Assembly? testAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.FullName == testAssemblyName);

                    if (testAssembly == null)
                    {
                        // Assembly isn't loaded yet, try to load it
                        testAssembly = System.Reflection.Assembly.Load(testAssemblyName);
                    }

                    opts.Discovery.IncludeAssembly(testAssembly);
                    Log.Information("Included test assembly {AssemblyName} for handler discovery", testAssemblyName);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load test assembly {AssemblyName}", testAssemblyName);
                }
            }
        }

        // Module messaging transport — defaults to in-memory local queues
        // Set ModuleMessaging:Transport to "RabbitMq" to enable RabbitMQ transport
        string transport = builder.Configuration.GetValue<string>("ModuleMessaging:Transport") ?? "InMemory";

        if (transport.Equals("RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            string rabbitMqConnection = builder.Configuration.GetConnectionString("RabbitMq")
                ?? throw new InvalidOperationException(
                    "RabbitMq connection string is required when ModuleMessaging:Transport is 'RabbitMq'");

            RabbitMqTransportExpression rabbitMq = opts.UseRabbitMq(new Uri(rabbitMqConnection))
                .AutoProvision()
                .UseConventionalRouting();

            if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
            {
                rabbitMq.AutoPurgeOnStartup();
            }

            // Test queue for integration tests
            if (builder.Environment.IsEnvironment("Testing"))
            {
                rabbitMq.DeclareQueue("test-inbox");
                opts.ListenToRabbitQueue("test-inbox");
            }
        }

        // Durable outbox on all sending endpoints (skip in Testing environment)
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        }
    }, ExtensionDiscovery.ManualOnly);

    // Redis — use a factory to defer connection until after WebApplicationFactory
    // overrides are applied (the top-level code runs before ConfigureWebHost)
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        IConfiguration config = sp.GetRequiredService<IConfiguration>();
        string connectionString = config.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string not configured");
        return ConnectionMultiplexer.Connect(connectionString);
    });
    builder.Services.AddSingleton<IPresenceService, RedisPresenceService>();
    builder.Services.AddSingleton<IRealtimeDispatcher, SignalRRealtimeDispatcher>();

    // Distributed cache — used by modules like FeatureFlags for caching
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        // Defer connection to use final config (WebApplicationFactory overrides)
        options.ConnectionMultiplexerFactory = async () =>
        {
            string connStr = builder.Configuration.GetConnectionString("Redis")!;
            return await ConnectionMultiplexer.ConnectAsync(connStr);
        };
    });

    // SignalR with Redis backplane — defer connection to use final config
    // (WebApplicationFactory overrides are applied after service registration)
    ConfigurationManager configRef = builder.Configuration;
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("Foundry");
            options.ConnectionFactory = async writer =>
            {
                string connStr = configRef.GetConnectionString("Redis")!;
                return await ConnectionMultiplexer.ConnectAsync(connStr, writer);
            };
        });

    // Core services
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddControllers();
    builder.Services.AddApiVersioning(opts =>
    {
        opts.DefaultApiVersion = new ApiVersion(1, 0);
        opts.AssumeDefaultVersionWhenUnspecified = true;
        opts.ReportApiVersions = true;
        opts.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddApiExplorer(opts =>
    {
        opts.GroupNameFormat = "'v'VVV";
        opts.SubstituteApiVersionInUrl = true;
    });
    builder.Services.AddSharedKernel();
    builder.Services.AddHtmlSanitization();
    builder.Services.AddApiServices(builder.Configuration, builder.Environment);
    builder.Services.AddObservability(builder.Configuration, builder.Environment);
    builder.Services.AddHangfireServices(builder.Configuration);
    builder.Services.AddFoundryBackgroundJobs();
    builder.Services.AddFoundryWorkflows(builder.Configuration, builder.Environment);
    builder.Services.AddScoped<SystemHeartbeatJob>();
    builder.Services.AddFoundryRateLimiting();

    WebApplication app = builder.Build();

    // ============================================================================
    // FOUNDRY MODULES INITIALIZATION
    // Explicit module initialization via FoundryModules.cs
    // ============================================================================
    await Foundry.Api.FoundryModules.InitializeFoundryModulesAsync(app);
    await app.InitializeAuditingAsync();

    // Middleware pipeline (order matters!)

    // Exception handling
    app.UseExceptionHandler("/error");
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    // Security headers (CSP, X-Content-Type-Options, etc.)
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // HTTPS enforcement
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
    app.UseHttpsRedirection();

    // API version rewrite (backward compat: /api/foo → /api/v1/foo)
    // Must run before routing so the rewritten path is what the router sees.
    app.UseMiddleware<ApiVersionRewriteMiddleware>();

    // Explicit routing placement — ensures the version rewrite runs before route matching.
    // Without this, .NET auto-inserts UseRouting() at the start of the pipeline.
    app.UseRouting();

    // Dev tools — version-segmented API docs (one OpenAPI doc per API version group)
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options =>
        {
            options
                .WithTitle("Foundry API")
                .WithTheme(ScalarTheme.Purple)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .AddDocument("v1", "Foundry API v1", isDefault: true);
        }).AllowAnonymous();
    }

    // CORS
    if (app.Environment.IsDevelopment())
    {
        app.UseCors("Development");
    }
    else
    {
        app.UseCors();
    }

    // Health checks
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = WriteHealthCheckResponse
    }).AllowAnonymous();

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthCheckResponse
    }).AllowAnonymous();

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();

    // Info endpoint
    app.MapGet("/", () => Results.Ok(new
    {
        Name = "Foundry API",
        Version = "1.0.0",
        Health = "/health"
    })).ExcludeFromDescription().AllowAnonymous();

    // Rate limiting
    app.UseRateLimiter();

    // API key authentication (checks X-Api-Key header first, falls through to JWT if not present)
    app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

    // Authentication (Keycloak OIDC JWT validation)
    app.UseAuthentication();

    // Tenant resolution (reads org claim from JWT → populates ITenantContext)
    // Note: For API key auth, tenant is already set by ApiKeyAuthenticationMiddleware
    app.UseMiddleware<TenantResolutionMiddleware>();

    // SCIM authentication (Bearer token validation for /scim/v2/* endpoints)
    app.UseMiddleware<ScimAuthenticationMiddleware>();

    // Permission expansion (reads Keycloak roles → expands to PermissionType claims)
    app.UseMiddleware<PermissionExpansionMiddleware>();

    // Authorization (checks [HasPermission] attributes)
    app.UseAuthorization();

    // Module tagging (tags HTTP requests with foundry.module for observability)
    app.UseMiddleware<ModuleTaggingMiddleware>();

    // Service account usage tracking
    app.UseServiceAccountTracking();

    // Hangfire dashboard
    app.UseHangfireDashboard();

    // Endpoints
    app.MapControllers();

    // Elsa Workflow engine (runs in all environments)
    app.UseWorkflows();

    // Elsa Workflow management API (dev only — not exposed in production)
    if (app.Environment.IsDevelopment())
    {
        string elsaRoutePrefix = app.Services.GetRequiredService<IOptions<ApiEndpointOptions>>().Value.RoutePrefix;
        app.UseWorkflowsApi(elsaRoutePrefix);
        app.UseJsonSerializationErrorHandler();
    }

    app.MapHub<RealtimeHub>("/hubs/realtime");
    app.MapAsyncApiEndpoints();

    // API-level recurring jobs (use DI-based IRecurringJobManager, not static RecurringJob)
    await using (AsyncServiceScope jobScope = app.Services.CreateAsyncScope())
    {
        IRecurringJobManager jobManager = jobScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        jobManager.AddOrUpdate<SystemHeartbeatJob>(
            "system-heartbeat",
            job => job.ExecuteAsync(),
            "*/5 * * * *");

        jobManager.AddOrUpdate<RetryFailedEmailsJob>(
            "retry-failed-emails",
            job => job.ExecuteAsync(CancellationToken.None),
            "*/5 * * * *");

    }
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Health check response writer
static Task WriteHealthCheckResponse(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
{
    context.Response.ContentType = "application/json";

    IHostEnvironment env = context.RequestServices.GetRequiredService<IHostEnvironment>();

    if (env.IsProduction())
    {
        context.Response.StatusCode = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() });
    }

    object response = new
    {
        status = report.Status.ToString(),
        duration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            duration = e.Value.Duration.TotalMilliseconds,
            description = e.Value.Description,
            error = e.Value.Exception?.Message
        })
    };

    return context.Response.WriteAsJsonAsync(response);
}
