using System.Reflection;
using Asp.Versioning;
using Hangfire;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using OpenIddict.Abstractions;
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
using Wallow.ApiKeys.Infrastructure.Modules;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Jobs;
using Wallow.Identity.Infrastructure.Middleware;
using Wallow.Identity.Infrastructure.MultiTenancy;
using Wallow.Notifications.Infrastructure.Jobs;
using Wallow.Notifications.Infrastructure.Modules;
using Wallow.ServiceDefaults;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Infrastructure.BackgroundJobs;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Infrastructure.Core.Cache;
using Wallow.Shared.Infrastructure.Core.Messaging;
using Wallow.Shared.Infrastructure.Core.Middleware;
using Wallow.Shared.Infrastructure.Core.Services;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Infrastructure.Jobs;
using Wallow.Storage.Infrastructure.Modules;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.Persistence;
using Wolverine.Postgresql;

// A regular logger supports multiple WebApplicationFactory hosts in one process.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    string appVersion = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    Log.Information("Starting Wallow API v{Version}", appVersion);

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();

    // Set the cooperative shutdown timeout.
    builder.Services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(10);
    });


    string namespacePrefix = builder.Configuration["Logging:NamespacePrefix"] ?? "Wallow";
    Wallow.Shared.Kernel.Diagnostics.Initialize(namespacePrefix);


    builder.WebHost.ConfigureKestrel((context, options) =>
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = 1_048_576;


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


    IConfigurationSection performanceSection = builder.Configuration.GetSection(PerformanceOptions.SectionName);
    int workerThreads = performanceSection.GetValue<int>("ThreadPoolMinWorkerThreads");
    int completionPortThreads = performanceSection.GetValue<int>("ThreadPoolMinCompletionPortThreads");
    if (workerThreads > 0 && completionPortThreads > 0)
    {
        ThreadPool.SetMinThreads(workerThreads, completionPortThreads);
    }


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

    // Register Redis before modules that resolve it during service registration.
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        IConfiguration config = sp.GetRequiredService<IConfiguration>();
        string connectionString = config.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string not configured");
        return ConnectionMultiplexer.Connect(connectionString);
    });


    IReadOnlyList<IWallowModule> enabledModules = Wallow.Api.WallowModules.AddWallowModules(
        builder.Services, builder.Configuration, builder.Environment);
    builder.Services.AddAuthAuditing(builder.Configuration);

    // Share the enabled handler assembly set between Wolverine and AsyncAPI.
    Assembly[] handlerAssemblies =
    [
        // Include host handlers in both discovery consumers.
        typeof(Wallow.Api.WallowModules).Assembly,

        // Shared infrastructure owns settings-change handlers outside any module.
        typeof(IWallowModule).Assembly,

        .. enabledModules.SelectMany(module => module.HandlerAssemblies),
    ];

    // Manual extension discovery below requires explicit runtime-compiler registration.
    builder.Host.UseWolverine(opts =>
    {
        // Dynamic handler code generation needs the runtime compiler with ManualOnly discovery.
        opts.UseRuntimeCompilation();

        // Keep service location explicit for opaque factories and request-scoped tenant instances.
        opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IOpenIddictApplicationManager>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITenantContext>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITenantContextSetter>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ISetupStatusChecker>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IBootstrapAdminService>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IOrganizationService>();

        // ASP.NET authorization handlers must not become Wolverine message handlers.
        opts.Discovery.CustomizeHandlerDiscovery(types => types.Excludes.Implements<IAuthorizationHandler>());

        // Pin the application assembly when multiple test hosts share a process.
        opts.ApplicationAssembly = typeof(WallowModules).Assembly;

        // Separate handlers have independent queues and retries; their execution order is unspecified.
        opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;

        // Discover only host/shared handlers and assemblies declared by enabled modules.
        foreach (Assembly assembly in handlerAssemblies)
        {
            opts.Discovery.IncludeAssembly(assembly);
        }

        // Use the same message-storage schema for all persistence registrations.
        opts.Durability.MessageStorageSchemaName = "wolverine";

        // Transactional handlers need PostgreSQL message storage in Testing as well as deployed hosts.
        string pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not configured");
        opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine");

        if (builder.Environment.IsEnvironment("Testing"))
        {
            // Isolated test hosts do not need multi-node assignment.
            opts.Durability.Mode = DurabilityMode.Solo;
        }

        // Lightweight transactions use SaveChangesAsync with the EF retry strategy.
        // Direct SQL mutations remain outside that SaveChanges unit of work.
        opts.UseEntityFrameworkCoreTransactions(TransactionMiddlewareMode.Lightweight);

        // Add transaction middleware to chains that expose a DbContext.
        // Keep one visible context per chain and explicit saves before realtime dispatch.
        opts.Policies.AutoApplyTransactions();


        opts.ConfigureStandardErrorHandling();
        opts.ConfigureMessageLogging();

        // Modules register validators; rediscovery would add duplicate registrations.
        opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);


        opts.Policies.AddMiddleware(typeof(WolverineModuleTaggingMiddleware));

        // Preserve tenant context across message dispatch.
        opts.Policies.AddMiddleware(typeof(TenantStampingMiddleware));
        opts.Policies.AddMiddleware(typeof(TenantRestoringMiddleware));

        // Require a tenant header on remote messages.
        opts.Policies.AddMiddleware(typeof(WolverineAuthorizationMiddleware));


        if (builder.Environment.IsEnvironment("Testing"))
        {
            string? testAssemblyName = builder.Configuration["Wolverine:TestAssembly"];
            if (!string.IsNullOrEmpty(testAssemblyName))
            {
                try
                {

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

        // Enable endpoint durability policies outside Testing.
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            opts.Policies.UseDurableInboxOnAllListeners();
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        }
        // Explicit discovery keeps extension loading under host control.
    }, ExtensionDiscovery.ManualOnly);

    builder.Services.AddSingleton<IPresenceService, RedisPresenceService>();
    builder.Services.AddSingleton<IRealtimeDispatcher, SignalRRealtimeDispatcher>();

    // Reuse the singleton Redis connection for distributed caching.
    builder.Services.AddStackExchangeRedisCache(_ => { });
    builder.Services.AddSingleton<IConfigureOptions<RedisCacheOptions>>(sp =>
    {
        IConnectionMultiplexer mux = sp.GetRequiredService<IConnectionMultiplexer>();
        return new ConfigureNamedOptions<RedisCacheOptions>(
            Options.DefaultName,
#pragma warning disable CA2025 // DI owns the singleton connection lifetime.
            options => options.ConnectionMultiplexerFactory = () => Task.FromResult(mux));
#pragma warning restore CA2025
    });


    builder.Services.AddSingleton<IDistributedCache>(sp =>
    {
        IOptions<RedisCacheOptions> options =
            sp.GetRequiredService<IOptions<RedisCacheOptions>>();
        RedisCache inner = new(options);
        return new InstrumentedDistributedCache(inner);
    });

    // Combine local and distributed cache storage.
    builder.Services.AddHybridCache(options =>
    {
        options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = TimeSpan.FromMinutes(5),
            Expiration = TimeSpan.FromMinutes(30),
        };
    });


    builder.Services.AddSingleton<SseConnectionManager>();
    builder.Services.AddSingleton<ISseDispatcher, RedisSseDispatcher>();
    builder.Services.AddHostedService<SseRedisSubscriber>();

    // Replace the Identity no-op revoker with the local connection revoker.
    builder.Services.AddSingleton<RealtimeConnectionRegistry>();
    builder.Services.AddSingleton<IRealtimeAccessRevoker, RealtimeAccessRevoker>();

    // Share the Redis connection with the SignalR backplane.
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
#pragma warning disable CA2025 // DI owns the singleton connection lifetime.
            options => options.ConnectionFactory = _ => Task.FromResult<IConnectionMultiplexer>(mux));
#pragma warning restore CA2025
    });


    builder.Services.AddHttpContextAccessor();
    IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews();

    // Remove disabled-module controllers before routing and OpenAPI discover them.
    mvcBuilder.ConfigureApplicationPartManager(manager =>
        Wallow.Api.WallowModules.RemoveDisabledModuleApiParts(manager, enabledModules));

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
    }).AddOpenApi(options =>
        Wallow.Api.Extensions.ServiceCollectionExtensions.ConfigureVersionedOpenApiDocument(
            options, builder.Configuration));
    builder.Services.AddSharedKernel();
    builder.Services.AddHtmlSanitization();
    builder.Services.AddCurrentUserService();
    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddHangfireServices(builder.Configuration);
    builder.Services.AddWallowBackgroundJobs();
    builder.Services.AddScoped<SystemHeartbeatJob>();
    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddWallowRateLimiting(builder.Configuration);
    }
    builder.Services.AddFeatureManagement();

    WebApplication app = builder.Build();

    // Validate duplicate error codes at startup, before any document request.
    _ = app.Services.GetRequiredService<ErrorCatalog>();


    string? pathBase = app.Configuration["PathBase"];
    if (!string.IsNullOrEmpty(pathBase))
    {
        app.UsePathBase(pathBase);
    }


    await Wallow.Api.WallowModules.InitializeWallowModulesAsync(app, enabledModules);
    await app.InitializeAuthAuditingAsync();



    // Restore the forwarded scheme before HTTPS and authentication middleware inspect it.
    if (!app.Environment.IsDevelopment())
    {
        ForwardedHeadersOptions forwardedHeadersOptions = new()
        {
            ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
        };
        // Trust forwarded headers from any sender; deployment must restrict direct Kestrel access.
        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeadersOptions);
    }

    // Use registered exception handlers and problem writers without route re-execution.
    app.UseExceptionHandler();

    // Fill eligible empty error responses through the registered problem writer.
    app.UseStatusCodePages();
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


            bool isSse = string.Equals(
                httpContext.Response.ContentType,
                "text/event-stream",
                StringComparison.OrdinalIgnoreCase);
            diagnosticContext.Set("RequestProtocol", isSse ? "SSE" : "HTTP");
        };
    });


    app.UseMiddleware<CorrelationIdMiddleware>();

    // Reject protected requests with Setup.Required while bootstrap is pending.
    app.UseMiddleware<SetupMiddleware>();


    app.UseMiddleware<SecurityHeadersMiddleware>();


    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    // Rewrite unversioned paths before route matching.
    app.UseMiddleware<ApiVersionRewriteMiddleware>();

    // Place routing here so it observes the rewritten path.
    app.UseRouting();


    if (app.Environment.IsDevelopment())
    {
        string scalarAppName = builder.Configuration["Branding:AppName"] ?? "Wallow";
        app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
        app.MapScalarApiReference(options =>
        {
            options
                .WithTitle($"{scalarAppName} API")
                .WithTheme(ScalarTheme.Purple)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .AddDocument("v1", $"{scalarAppName} API v1", isDefault: true);
        }).AllowAnonymous();
    }

    app.MapDefaultEndpoints();


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


    if (!app.Environment.IsProduction())
    {
        app.MapGet("/", () => Results.Ok(new
        {
            Name = "Wallow API",
            Version = appVersion,
            Health = "/health"
        })).ExcludeFromDescription().AllowAnonymous();
    }

    // API-key middleware requires the enabled ApiKeys service registration.
    if (enabledModules.IsModuleEnabled<ApiKeysModule>())
    {
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }


    app.UseAuthentication();

    // Resolve bearer/cookie tenant context; API keys may have populated it already.
    app.UseMiddleware<TenantResolutionMiddleware>();


    app.UseMiddleware<TenantBaggageMiddleware>();


    app.UseMiddleware<PermissionExpansionMiddleware>();

    // Partition limits after authentication and tenant resolution; Development opts out.
    if (!app.Environment.IsDevelopment())
    {
        app.UseRateLimiter();
    }

    // Return 404 before fallback authorization can challenge an unmatched path.
    // Use middleware so no catch-all route changes controller matching; Hangfire handles its own path.
    app.Use(static async (context, next) =>
    {
        if (context.GetEndpoint() is null && !context.Request.Path.StartsWithSegments("/hangfire"))
        {

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });


    app.UseAuthorization();


    app.UseSessionActivity();


    app.UseMiddleware<ModuleTaggingMiddleware>();


    app.UseServiceAccountTracking();


    app.UseHangfireDashboard();


    app.MapControllers();

    app.MapHub<RealtimeHub>("/hubs/realtime");
    // EventSource consumes this stream; exclude its untyped response from SDK generation.
    app.MapGet("/events", SseEndpoint.HandleSseConnection).RequireAuthorization().ExcludeFromDescription();
    app.MapAsyncApiEndpoints(handlerAssemblies);


    await using (AsyncServiceScope jobScope = app.Services.CreateAsyncScope())
    {
        IRecurringJobManager jobManager = jobScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        jobManager.AddOrUpdate<SystemHeartbeatJob>(
            "system-heartbeat",
            job => job.ExecuteAsync(),
            "*/5 * * * *");

        if (enabledModules.IsModuleEnabled<NotificationsModule>())
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

        if (enabledModules.IsModuleEnabled<StorageModule>())
        {
            jobManager.AddOrUpdate<OrphanedObjectSweepJob>(
                "storage-orphaned-object-sweep",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily());
        }

    }
    // Detach Redis profiling before SignalR unsubscribes during DI disposal.
    IHostApplicationLifetime lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        IConnectionMultiplexer mux = app.Services.GetRequiredService<IConnectionMultiplexer>();
        if (mux is ConnectionMultiplexer connectionMultiplexer)
        {
            connectionMultiplexer.RegisterProfiler(null!);
        }
    });


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

        // Reject recognized development credential placeholders outside Development and Testing.
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
        Log.Information("Wallow API v{Version} is now listening on {Urls}", appVersion, urls);
    });

    await app.StartAsync();

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
