using System.Reflection;
using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
using Wallow.Api;
using Wallow.Api.Endpoints;
using Wallow.Api.Extensions;
using Wallow.Api.Hubs;
using Wallow.Api.Jobs;
using Wallow.Api.Logging;
using Wallow.Api.Middleware;
using Wallow.Api.Services;
using Wallow.ApiKeys.Infrastructure.Authorization;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Jobs;
using Wallow.Identity.Infrastructure.Middleware;
using Wallow.Identity.Infrastructure.MultiTenancy;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Notifications.Infrastructure.Jobs;
using Wallow.ServiceDefaults;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Infrastructure.BackgroundJobs;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Infrastructure.Core.Cache;
using Wallow.Shared.Infrastructure.Core.Messaging;
using Wallow.Shared.Infrastructure.Core.Middleware;
using Wallow.Shared.Infrastructure.Core.Services;
using Wallow.Shared.Kernel.Extensions;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.Postgresql;

// Note: Using CreateLogger() instead of CreateBootstrapLogger() to support
// multiple WebApplicationFactory instances in integration tests
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Wallow API");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();

    // Ensure the host doesn't hang indefinitely during shutdown
    builder.Services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(10);
    });

    // Initialize telemetry diagnostics with configurable namespace prefix
    string namespacePrefix = builder.Configuration["Logging:NamespacePrefix"] ?? "Wallow";
    Wallow.Shared.Kernel.Diagnostics.Initialize(namespacePrefix);

    // Suppress Kestrel server header to avoid exposing server technology
    builder.WebHost.ConfigureKestrel((context, options) =>
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = 1_048_576;

        // Apply configurable connection limits from Performance section
        long? maxConcurrentConnections = context.Configuration.GetValue<long?>("Performance:KestrelMaxConcurrentConnections");
        long? maxConcurrentUpgradedConnections = context.Configuration.GetValue<long?>("Performance:KestrelMaxConcurrentUpgradedConnections");

        if (maxConcurrentConnections is > 0)
        {
            options.Limits.MaxConcurrentConnections = maxConcurrentConnections;
        }

        if (maxConcurrentUpgradedConnections is > 0)
        {
            options.Limits.MaxConcurrentUpgradedConnections = maxConcurrentUpgradedConnections;
        }
    });

    // Apply thread pool tuning from Performance section
    IConfigurationSection performanceSection = builder.Configuration.GetSection(PerformanceOptions.SectionName);
    int workerThreads = performanceSection.GetValue<int>("ThreadPoolMinWorkerThreads");
    int completionPortThreads = performanceSection.GetValue<int>("ThreadPoolMinCompletionPortThreads");
    if (workerThreads > 0 && completionPortThreads > 0)
    {
        ThreadPool.SetMinThreads(workerThreads, completionPortThreads);
    }

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Destructure.With<PiiDestructuringPolicy>()
            .Enrich.With(new ModuleEnricher(context.Configuration))
            .Enrich.WithProperty("Application", context.Configuration["Logging:NamespacePrefix"] ?? "Wallow")
            .WriteTo.Console(new Serilog.Templates.ExpressionTemplate(
                "[{@t:HH:mm:ss} {@l:u3}]" +
                " [M:\x1b[38;5;178m{Module}\x1b[0m]" +
                "{#if TenantName is not null} [T:\x1b[35m{TenantName}\x1b[0m]" +
                "{#else if TenantId is not null} [T:\x1b[35m{TenantId}\x1b[0m]{#end}" +
                "{#if ClientId is not null} [C:\x1b[36m{ClientId}\x1b[0m]{#end}" +
                "{#if UserId is not null} [U:\x1b[33m{UserId}\x1b[0m]{#end}" +
                "{#if RequestProtocol is not null} [{#if RequestProtocol = 'SSE'}\x1b[38;5;208mSSE\x1b[0m{#else}HTTP{#end}]{#end}" +
                "{#if RequestMethod is not null} {#if RequestMethod = 'GET'}\x1b[32m{RequestMethod}\x1b[0m" +
                "{#else if RequestMethod = 'POST'}\x1b[33m{RequestMethod}\x1b[0m" +
                "{#else if RequestMethod = 'PUT'}\x1b[34m{RequestMethod}\x1b[0m" +
                "{#else if RequestMethod = 'DELETE'}\x1b[31m{RequestMethod}\x1b[0m" +
                "{#else if RequestMethod = 'PATCH'}\x1b[36m{RequestMethod}\x1b[0m" +
                "{#else}{RequestMethod}{#end}{#end}" +
                "{#if StatusCode is not null} {#if StatusCode >= 200 and StatusCode < 300}\x1b[32m{StatusCode}\x1b[0m" +
                "{#else if StatusCode >= 300 and StatusCode < 400}\x1b[36m{StatusCode}\x1b[0m" +
                "{#else if StatusCode >= 400 and StatusCode < 500}\x1b[33m{StatusCode}\x1b[0m" +
                "{#else if StatusCode >= 500}\x1b[31m{StatusCode}\x1b[0m" +
                "{#else}{StatusCode}{#end}{#end}" +
                " {@m}\n{@x}"));

        // OpenTelemetry log export — conditional on EnableLogging flag
        if (context.Configuration.GetValue<bool>("OpenTelemetry:EnableLogging", false))
        {
            string otlpEndpoint = context.Configuration["OpenTelemetry:OtlpEndpoint"]!;
            string serviceName = context.Configuration["OpenTelemetry:ServiceName"]
                ?? "Wallow";

            configuration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint + "/v1/logs";
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                    ["service.namespace"] = context.Configuration["Logging:NamespacePrefix"] ?? "Wallow",
                    ["deployment.environment"] = context.HostingEnvironment.EnvironmentName
                };
            });
        }
    });

    // Redis — register before modules so that module service registration
    // (e.g. Identity DataProtection key persistence) can resolve IConnectionMultiplexer.
    // Uses a factory to defer the actual connection until first resolution.
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        IConfiguration config = sp.GetRequiredService<IConfiguration>();
        string connectionString = config.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string not configured");
        return ConnectionMultiplexer.Connect(connectionString);
    });

    // ============================================================================
    // WALLOW MODULES
    // Explicit module registration via WallowModules.cs
    // See docs/plans/2026-02-13-modular-monolith-consolidation.md
    // ============================================================================
    Wallow.Api.WallowModules.AddWallowModules(builder.Services, builder.Configuration, builder.Environment);
    builder.Services.AddWallowAuditing(builder.Configuration);
    builder.Services.AddAuthAuditing(builder.Configuration);

    // Wolverine — unified CQRS mediator + message bus
    // Use ManualOnly to prevent Wolverine from scanning native DLLs (QuestPDF/Skia)
    // which can cause crashes on macOS (exit codes 139/134)
    builder.Host.UseWolverine(opts =>
    {
        // Discover handlers in all Wallow assemblies
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Wallow.", StringComparison.Ordinal) == true))
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

        // Module tagging middleware — tags Wolverine messages with wallow.module
        opts.Policies.AddMiddleware(typeof(WolverineModuleTaggingMiddleware));

        // Tenant middleware — stamps outgoing messages with TenantId and restores it on incoming
        opts.Policies.AddMiddleware(typeof(TenantStampingMiddleware));
        opts.Policies.AddMiddleware(typeof(TenantRestoringMiddleware));

        // Authorization middleware — validates tenant context on external messages
        opts.Policies.AddMiddleware(typeof(WolverineAuthorizationMiddleware));

        // For integration tests - discover handlers from test assemblies
        if (builder.Environment.IsEnvironment("Testing"))
        {
            string? testAssemblyName = builder.Configuration["Wolverine:TestAssembly"];
            if (!string.IsNullOrEmpty(testAssemblyName))
            {
                try
                {
                    // Try to load the assembly if not already loaded
                    Assembly testAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.FullName == testAssemblyName)
                        ?? Assembly.Load(testAssemblyName);

                    opts.Discovery.IncludeAssembly(testAssembly);
                    Log.Information("Included test assembly {AssemblyName} for handler discovery", testAssemblyName);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load test assembly {AssemblyName}", testAssemblyName);
                }
            }
        }

        // Durable inbox/outbox on all endpoints (skip in Testing environment)
        // Inbox: guarantees at-least-once delivery with automatic deduplication (idempotency)
        // Outbox: guarantees messages are sent only after the transaction commits
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            opts.Policies.UseDurableInboxOnAllListeners();
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        }
    }, ExtensionDiscovery.ManualOnly);

    builder.Services.AddSingleton<IPresenceService, RedisPresenceService>();
    builder.Services.AddSingleton<IRealtimeDispatcher, SignalRRealtimeDispatcher>();

    // Distributed cache — reuses the singleton IConnectionMultiplexer registered above
    builder.Services.AddStackExchangeRedisCache(_ => { });
    builder.Services.AddSingleton<IConfigureOptions<RedisCacheOptions>>(sp =>
    {
        IConnectionMultiplexer mux = sp.GetRequiredService<IConnectionMultiplexer>();
        return new ConfigureNamedOptions<RedisCacheOptions>(
            Options.DefaultName,
#pragma warning disable CA2025 // Singleton IConnectionMultiplexer lifetime is managed by DI, not the Task
            options => options.ConnectionMultiplexerFactory = () => Task.FromResult(mux));
#pragma warning restore CA2025
    });

    // Wrap IDistributedCache with instrumented decorator for cache hit/miss metrics
    builder.Services.AddSingleton<IDistributedCache>(sp =>
    {
        IOptions<RedisCacheOptions> options =
            sp.GetRequiredService<IOptions<RedisCacheOptions>>();
        RedisCache inner = new(options);
        return new InstrumentedDistributedCache(inner);
    });

    // HybridCache — L1 in-memory + L2 distributed (Valkey) with automatic stampede protection
    builder.Services.AddHybridCache(options =>
    {
        options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = TimeSpan.FromMinutes(5),
            Expiration = TimeSpan.FromMinutes(30),
        };
    });

    // SSE real-time — connection manager, Redis-backed dispatcher, and subscriber
    builder.Services.AddSingleton<SseConnectionManager>();
    builder.Services.AddSingleton<ISseDispatcher, RedisSseDispatcher>();
    builder.Services.AddHostedService<SseRedisSubscriber>();

    // SignalR with Redis backplane — reuses the singleton IConnectionMultiplexer registered above
    builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(options =>
        {
            string redisPrefix = builder.Configuration["SignalR:RedisPrefix"] ?? "Wallow";
            options.Configuration.ChannelPrefix = RedisChannel.Literal(redisPrefix);
        });
    builder.Services.AddSingleton<IConfigureOptions<RedisOptions>>(sp =>
    {
        IConnectionMultiplexer mux = sp.GetRequiredService<IConnectionMultiplexer>();
        return new ConfigureNamedOptions<RedisOptions>(
            Options.DefaultName,
#pragma warning disable CA2025 // mux is a DI-managed singleton, not disposed here
            options => options.ConnectionFactory = _ => Task.FromResult<IConnectionMultiplexer>(mux));
#pragma warning restore CA2025
    });

    // Core services
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddAntiforgery();
    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

        // When PathBase="/api" handles the /api prefix, strip it from route templates
        // so controllers don't double-prefix (e.g. "api/v1/..." becomes "v1/...").
        string? pathBase = builder.Configuration["PathBase"];
        if (!string.IsNullOrEmpty(pathBase) &&
            pathBase.TrimStart('/').Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            options.Conventions.Add(new StripApiRoutePrefixConvention());
        }
    });
    builder.Services.AddApiVersioning(opts =>
    {
        opts.DefaultApiVersion = new ApiVersion(1);
        opts.AssumeDefaultVersionWhenUnspecified = true;
        opts.ReportApiVersions = true;
        opts.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddApiExplorer(opts =>
    {
        opts.GroupNameFormat = "'v'V";
        opts.SubstitutionFormat = "V";
        opts.SubstituteApiVersionInUrl = true;
    });
    builder.Services.AddSharedKernel();
    builder.Services.AddHtmlSanitization();
    builder.Services.AddCurrentUserService();
    builder.Services.AddApiServices(builder.Configuration, builder.Environment);
    builder.Services.AddHangfireServices(builder.Configuration);
    builder.Services.AddWallowBackgroundJobs();
    builder.Services.AddScoped<SystemHeartbeatJob>();
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddWallowRateLimiting();
    }
    builder.Services.AddFeatureManagement();

    WebApplication app = builder.Build();

    // Opt-in PathBase for reverse-proxy path-based routing (e.g. /api)
    string? pathBase = app.Configuration["PathBase"];
    if (!string.IsNullOrEmpty(pathBase))
    {
        app.UsePathBase(pathBase);
    }

    // ============================================================================
    // WALLOW MODULES INITIALIZATION
    // Explicit module initialization via WallowModules.cs
    // ============================================================================
    await Wallow.Api.WallowModules.InitializeWallowModulesAsync(app);
    await app.InitializeAppAuditingAsync();
    await app.InitializeAuthAuditingAsync();

    // Seed default roles, sync pre-registered OAuth2 clients, and bootstrap admin if configured
    await using (AsyncServiceScope seedScope = app.Services.CreateAsyncScope())
    {
        IServiceProvider sp = seedScope.ServiceProvider;

        // Seed default roles (admin, manager, user) — idempotent
        DefaultRoleSeeder roleSeeder = sp.GetRequiredService<DefaultRoleSeeder>();
        await roleSeeder.SeedAsync();

        // Admin bootstrap priority chain: CLI args > appsettings config > skip
        // Use services directly (not IMessageBus) because Wolverine hasn't started yet at this point
        AdminBootstrapOptions configOptions = sp.GetRequiredService<IOptions<AdminBootstrapOptions>>().Value;
        ISetupStatusChecker setupStatusChecker = sp.GetRequiredService<ISetupStatusChecker>();
        bool setupRequired = await setupStatusChecker.IsSetupRequiredAsync();

        if (setupRequired)
        {
            string? cliEmail = app.Configuration["AdminBootstrap:Email"];
            string? cliPassword = app.Configuration["AdminBootstrap:Password"];
            string? cliFirstName = app.Configuration["AdminBootstrap:FirstName"];
            string? cliLastName = app.Configuration["AdminBootstrap:LastName"];

            // CLI-supplied credentials take priority (e.g. --AdminBootstrap:Email=... on command line)
            // then fall back to appsettings-bound AdminBootstrapOptions
            BootstrapAdminCommand? bootstrapCommand = null;
            if (!string.IsNullOrWhiteSpace(cliEmail) && !string.IsNullOrWhiteSpace(cliPassword))
            {
                bootstrapCommand = new BootstrapAdminCommand(
                    cliEmail,
                    cliPassword,
                    cliFirstName ?? string.Empty,
                    cliLastName ?? string.Empty);
            }
            else if (configOptions.IsConfigured)
            {
                bootstrapCommand = new BootstrapAdminCommand(
                    configOptions.Email,
                    configOptions.Password,
                    configOptions.FirstName,
                    configOptions.LastName);
            }

            if (bootstrapCommand is not null)
            {
                IBootstrapAdminService bootstrapAdminService = sp.GetRequiredService<IBootstrapAdminService>();
                await bootstrapAdminService.EnsureRoleExistsAsync("admin");
                bool userExists = await bootstrapAdminService.UserExistsAsync(bootstrapCommand.Email);
                if (!userExists)
                {
                    Guid userId = await bootstrapAdminService.CreateUserAsync(
                        bootstrapCommand.Email,
                        bootstrapCommand.Password,
                        bootstrapCommand.FirstName,
                        bootstrapCommand.LastName);
                    await bootstrapAdminService.AssignRoleAsync(userId, "admin");
                }
            }
        }

    }

    // Middleware pipeline (order matters!)

    // Forwarded headers — must run before any middleware that inspects the request scheme.
    // Cloudflare (and other reverse proxies) terminate TLS and forward HTTP with
    // X-Forwarded-For / X-Forwarded-Proto headers. Without this, OpenIddict sees HTTP
    // and rejects requests with "This server only accepts HTTPS requests" (ID2083).
    if (!app.Environment.IsDevelopment())
    {
        ForwardedHeadersOptions forwardedHeadersOptions = new()
        {
            ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
        };
        // Clear defaults so headers are accepted from any proxy in the chain.
        // Safe because Kestrel is not directly exposed to the internet.
        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeadersOptions);
    }

    // Exception handling
    app.UseExceptionHandler("/error");
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "{RequestPath} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            string? clientId = httpContext.User.GetClientId();
            if (clientId is not null)
            {
                diagnosticContext.Set("ClientId", clientId);
            }
            string? userId = httpContext.User.GetUserId();
            if (userId is not null)
            {
                diagnosticContext.Set("UserId", userId);
            }
            if (httpContext.Items.TryGetValue("TenantId", out object? tenantId) && tenantId is string tenantIdStr)
            {
                diagnosticContext.Set("TenantId", tenantIdStr);
            }
            if (httpContext.Items.TryGetValue("TenantName", out object? tenantName) && tenantName is string tenantNameStr
                && !string.IsNullOrEmpty(tenantNameStr))
            {
                diagnosticContext.Set("TenantName", tenantNameStr);
            }

            // Detect SSE vs HTTP protocol
            bool isSse = string.Equals(
                httpContext.Response.ContentType,
                "text/event-stream",
                StringComparison.OrdinalIgnoreCase);
            diagnosticContext.Set("RequestProtocol", isSse ? "SSE" : "HTTP");
        };
    });

    // Correlation ID (read X-Correlation-Id or generate, push to LogContext + Activity)
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Setup gate (redirects non-setup requests to setup wizard when admin bootstrap is pending)
    app.UseMiddleware<SetupMiddleware>();

    // Security headers (CSP, X-Content-Type-Options, etc.)
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // HTTPS enforcement
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    // API version rewrite (backward compat: /api/foo → /api/v1/foo)
    // Must run before routing so the rewritten path is what the router sees.
    app.UseMiddleware<ApiVersionRewriteMiddleware>();

    // Explicit routing placement — ensures the version rewrite runs before route matching.
    // Without this, .NET auto-inserts UseRouting() at the start of the pipeline.
    app.UseRouting();

    // Dev tools — version-segmented API docs (one OpenAPI doc per API version group)
    if (app.Environment.IsDevelopment())
    {
        string scalarAppName = builder.Configuration["Branding:AppName"] ?? "Wallow";
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options =>
        {
            options
                .WithTitle($"{scalarAppName} API")
                .WithTheme(ScalarTheme.Purple)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .AddDocument("v1", $"{scalarAppName} API v1", isDefault: true);
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

    app.MapDefaultEndpoints();

    // Health checks
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = WriteHealthCheckResponse
    }).AllowAnonymous();

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthCheckResponse
    }).AllowAnonymous();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();

    app.MapHealthChecks("/health/startup", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("startup"),
        ResponseWriter = WriteHealthCheckResponse
    }).AllowAnonymous();

    // Info endpoint (non-production only — avoid exposing version info in production)
    if (!app.Environment.IsProduction())
    {
        app.MapGet("/", () => Results.Ok(new
        {
            Name = "Wallow API",
            Version = "1.0.0",
            Health = "/health"
        })).ExcludeFromDescription().AllowAnonymous();
    }

    // Rate limiting
    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    {
        app.UseRateLimiter();
    }

    // API key authentication (checks X-Api-Key header first, falls through to JWT if not present)
    // Only register when ApiKeys module is enabled — the middleware depends on IApiKeyService
    {
        IFeatureManager apiKeyFeatureCheck = app.Services.GetRequiredService<IFeatureManager>();
        if (await apiKeyFeatureCheck.IsEnabledAsync("Modules.ApiKeys"))
        {
            app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        }
    }

    // Authentication (OpenIddict token validation)
    app.UseAuthentication();

    // Tenant resolution (reads org claim from JWT → populates ITenantContext)
    // Note: For API key auth, tenant is already set by ApiKeyAuthenticationMiddleware
    app.UseMiddleware<TenantResolutionMiddleware>();

    // Tenant observability (sets wallow.tenant_id on Activity tag + W3C Baggage for downstream propagation)
    app.UseMiddleware<TenantBaggageMiddleware>();

    // SCIM authentication (Bearer token validation for /scim/v2/* endpoints)
    app.UseMiddleware<ScimAuthenticationMiddleware>();

    // Permission expansion (reads roles → expands to PermissionType claims)
    app.UseMiddleware<PermissionExpansionMiddleware>();

    // Authorization (checks [HasPermission] attributes)
    app.UseAuthorization();

    // Session management (revoke tokens for invalidated sessions, track activity)
    app.UseSessionRevocation();
    app.UseSessionActivity();

    // Antiforgery token validation for MVC form posts (paired with AutoValidateAntiforgeryTokenAttribute)
    app.UseAntiforgery();

    // Module tagging (tags HTTP requests with wallow.module for observability)
    app.UseMiddleware<ModuleTaggingMiddleware>();

    // Service account usage tracking
    app.UseServiceAccountTracking();

    // Hangfire dashboard
    app.UseHangfireDashboard();

    // Endpoints
    app.MapControllers();

    app.MapHub<RealtimeHub>("/hubs/realtime");
    app.MapGet("/events", SseEndpoint.HandleSseConnection).RequireAuthorization();
    app.MapAsyncApiEndpoints();

    // API-level recurring jobs (use DI-based IRecurringJobManager, not static RecurringJob)
    await using (AsyncServiceScope jobScope = app.Services.CreateAsyncScope())
    {
        IRecurringJobManager jobManager = jobScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        jobManager.AddOrUpdate<SystemHeartbeatJob>(
            "system-heartbeat",
            job => job.ExecuteAsync(),
            "*/5 * * * *");

        IFeatureManager jobFeatureManager = jobScope.ServiceProvider.GetRequiredService<IFeatureManager>();

        if (await jobFeatureManager.IsEnabledAsync("Modules.Notifications"))
        {
            jobManager.AddOrUpdate<RetryFailedEmailsJob>(
                "retry-failed-emails",
                job => job.ExecuteAsync(CancellationToken.None),
                "*/5 * * * *");
        }

        jobManager.AddOrUpdate<OpenIddictTokenPruningJob>(
            "openiddict-token-pruning",
            job => job.ExecuteAsync(),
            "0 */4 * * *");

        jobManager.AddOrUpdate<ExpiredInvitationPruningJob>(
            "expired-invitation-pruning",
            job => job.ExecuteAsync(),
            "0 * * * *");

        jobManager.AddOrUpdate<SessionPruningJob>(
            "session-pruning",
            job => job.ExecuteAsync(),
            Cron.Daily());

    }
    // Unhook OpenTelemetry Redis profiler before DI disposal to prevent ObjectDisposedException
    // race condition when SignalR's RedisHubLifetimeManager unsubscribes during shutdown
    IHostApplicationLifetime lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        IConnectionMultiplexer mux = app.Services.GetRequiredService<IConnectionMultiplexer>();
        if (mux is ConnectionMultiplexer connectionMultiplexer)
        {
            connectionMultiplexer.RegisterProfiler(null!);
        }
    });

    // Startup configuration validation — fail fast if critical settings are missing
    if (!app.Environment.IsEnvironment("Testing"))
    {
        Dictionary<string, string?> requiredConfig = new()
        {
            ["ConnectionStrings:DefaultConnection"] = app.Configuration.GetConnectionString("DefaultConnection"),
            ["ConnectionStrings:Redis"] = app.Configuration.GetConnectionString("Redis"),
        };

        List<string> missing = requiredConfig
            .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required configuration: {string.Join(", ", missing)}. " +
                "Ensure all required settings are configured in appsettings or environment variables.");
        }

        // Dev credential guardrails — prevent development secrets from being used in non-Development environments
        if (!app.Environment.IsDevelopment())
        {
            List<string> devCredentialViolations = [];

            string? signingKey = app.Configuration["Identity:SigningKey"];
            if (signingKey is not null && signingKey.Contains("DevOnly", StringComparison.OrdinalIgnoreCase))
            {
                devCredentialViolations.Add("Identity:SigningKey contains development placeholder");
            }

            string? defaultConnection = app.Configuration.GetConnectionString("DefaultConnection");
            if (defaultConnection is not null && defaultConnection.Contains("Password=wallow", StringComparison.OrdinalIgnoreCase))
            {
                devCredentialViolations.Add("ConnectionStrings:DefaultConnection uses default development password");
            }

            string? redisConnection = app.Configuration.GetConnectionString("Redis");
            if (redisConnection is not null && redisConnection.Contains("WallowValkey123!", StringComparison.Ordinal))
            {
                devCredentialViolations.Add("ConnectionStrings:Redis uses default development password");
            }

            string? s3AccessKey = app.Configuration["Storage:S3:AccessKey"];
            if (s3AccessKey is not null && s3AccessKey == "GKac08a4bd9e083da18a8619d6")
            {
                devCredentialViolations.Add("Storage:S3:AccessKey uses default development key");
            }

            string? adminEmail = app.Configuration["AdminBootstrap:Email"];
            if (adminEmail is not null && adminEmail.EndsWith("@wallow.dev", StringComparison.OrdinalIgnoreCase))
            {
                devCredentialViolations.Add("AdminBootstrap:Email uses development domain (@wallow.dev)");
            }

            if (devCredentialViolations.Count > 0)
            {
                throw new InvalidOperationException(
                    "Development credentials detected in non-Development environment. " +
                    "Override these values via environment variables or appsettings before deploying:\n- " +
                    string.Join("\n- ", devCredentialViolations));
            }
        }
    }

    lifetime.ApplicationStarted.Register(() =>
    {
        string urls = string.Join(", ", app.Urls);
        Log.Information("Wallow API is now listening on {Urls}", urls);
    });

    await app.StartAsync();

    // Sync pre-registered OAuth2 clients after host start (Wolverine requires a running host for IMessageBus)
    // Auto-creates organizations from TenantName and seeds members from SeedMembers — all config-driven
    await using (AsyncServiceScope postStartScope = app.Services.CreateAsyncScope())
    {
        IServiceProvider sp = postStartScope.ServiceProvider;
        PreRegisteredClientSyncService clientSync = sp.GetRequiredService<PreRegisteredClientSyncService>();
        await clientSync.SyncAsync(CancellationToken.None);
    }

    await app.WaitForShutdownAsync();
}
catch (OperationCanceledException)
{
    Log.Information("Application shutdown completed");
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
static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    IHostEnvironment env = context.RequestServices.GetRequiredService<IHostEnvironment>();

    if (!env.IsDevelopment() && !env.IsEnvironment("Testing"))
    {
        context.Response.StatusCode = report.Status == HealthStatus.Healthy
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
