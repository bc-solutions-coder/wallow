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
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Infrastructure.Jobs;
using Wallow.Storage.Infrastructure.Modules;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.Persistence;
using Wolverine.Postgresql;

// Note: Using CreateLogger() instead of CreateBootstrapLogger() to support
// multiple WebApplicationFactory instances in integration tests
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
    IReadOnlyList<IWallowModule> enabledModules = Wallow.Api.WallowModules.AddWallowModules(
        builder.Services, builder.Configuration, builder.Environment);
    builder.Services.AddWallowAuditing(builder.Configuration);
    builder.Services.AddAuthAuditing(builder.Configuration);

    // The one handler-discovery list, shared by Wolverine below and by the AsyncAPI document near
    // the bottom of this file. Both used to run their own AppDomain.CurrentDomain.GetAssemblies()
    // scan over every "Wallow.*" name, which made the discovered handler set a function of what
    // earlier code happened to touch first — and, measured, gave the two scans DIFFERENT sets
    // rather than merely differently-ordered ones.
    Assembly[] handlerAssemblies =
    [
        // The host itself. Wolverine also picks this up through opts.ApplicationAssembly below;
        // naming it here is what keeps the AsyncAPI document looking at the same set.
        typeof(Wallow.Api.WallowModules).Assembly,

        // Wallow.Shared.Infrastructure belongs to no module but owns two real Wolverine handlers
        // (SettingsCacheInvalidationHandlers, for TenantSettingChangedEvent and
        // UserSettingChangedEvent), so the host declares it unconditionally.
        typeof(IWallowModule).Assembly,

        .. enabledModules.SelectMany(module => module.HandlerAssemblies),
    ];

    // Wolverine — unified CQRS mediator + message bus.
    //
    // This call ends with ExtensionDiscovery.ManualOnly — the argument is ~200 lines below,
    // on the closing line of UseWolverine, which is why it is called out here.
    //
    // ManualOnly is NOT a workaround for the macOS native-DLL crash (exit 139/134) it was
    // originally added for. That crash was a property of the pre-6.0 AssemblyFinder, which
    // probed the bin directory at startup and could load QuestPDF/Skia natives. Wolverine 6.0
    // (GH-2902) deleted that scan outright and replaced it with a compile-time source
    // generator: JasperFx.SourceGenerator emits a JasperFx.Generated.DiscoveredExtensions
    // manifest per assembly, which Wolverine reads through the application's REFERENCE GRAPH.
    // Per guide/extensions.md, "as of Wolverine 6.0 there is no runtime bin-directory assembly
    // scan for extensions". Wallow is on 6.21.0, so nothing probes the bin directory with or
    // without the flag.
    //
    // What ManualOnly still governs is IWolverineExtension AUTO-discovery. It is retained for
    // explicit control over which extensions load — not for the crash. Note the coupling to
    // UseRuntimeCompilation() immediately below, which is only necessary BECAUSE of it.
    builder.Host.UseWolverine(opts =>
    {
        // Wolverine 6 removed the runtime Roslyn compiler from the core package.
        // We use TypeLoadMode.Dynamic (compile handler/middleware code at runtime),
        // so the runtime compiler must be registered explicitly. Referencing the
        // WolverineFx.RuntimeCompilation package alone does not auto-register it here,
        // so call it directly. See https://wolverinefx.net/guide/codegen.html (GH-2876).
        //
        // "Does not auto-register" is true ONLY because of ExtensionDiscovery.ManualOnly:
        // the package ships an IWolverineExtension, and auto-discovery is exactly the
        // mechanism ManualOnly turns off. The two are coupled in both directions — dropping
        // ManualOnly would make this call redundant, and keeping it makes this call REQUIRED.
        // Without it, TypeLoadMode.Dynamic has no compiler and every handler fails to build.
        opts.UseRuntimeCompilation();

        // Wolverine 6 defaults ServiceLocationPolicy to NotAllowed: any handler or
        // middleware dependency the codegen cannot inline-construct throws
        // InvalidServiceLocationException at codegen time instead of silently falling
        // back to the container (which logged a "will throw in Wolverine 6.0" warning
        // under the previous AllowedButWarn stopgap). Keep the strict default so a new
        // accidental service location fails fast, and explicitly opt in the handful of
        // dependencies whose registrations genuinely require runtime resolution:
        //   - ITenantContext / ITenantContextSetter are forwarded to a single
        //     request-scoped TenantContext via an opaque lambda factory. Service
        //     location is the correct behavior here: inlining a fresh 'new TenantContext()'
        //     would hand handlers an empty tenant instead of the instance the HTTP
        //     tenant-resolution middleware populated for the request.
        //   - ISetupStatusChecker (behind IsSetupRequiredHandler) reaches IdentityDbContext,
        //     registered by AddDbContext's framework factory, which the codegen cannot see
        //     through.
        //   - IBootstrapAdminService and IOrganizationService (behind BootstrapAdminHandler) reach
        //     ASP.NET Identity's UserManager and OpenIddict's managers, both registered as opaque
        //     lambda factories the codegen cannot see through either.
        // HandlerCodegenTests compiles every discovered handler and fails if this list is short one
        // entry; WolverineCodegenPolicyTests asserts the list itself has not grown unnoticed.
        // See https://wolverinefx.net/guide/codegen.html.
        opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IOpenIddictApplicationManager>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITenantContext>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITenantContextSetter>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ISetupStatusChecker>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IBootstrapAdminService>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IOrganizationService>();

        // ASP.NET's authorization handlers are not Wolverine handlers, but they match its naming
        // convention exactly: the class ends in "Handler" and AuthorizationHandler<T> exposes a
        // public HandleAsync(AuthorizationHandlerContext). Left alone, Wolverine builds a message
        // chain for AuthorizationHandlerContext whose dependencies (SignInManager, UserManager)
        // cannot be inlined, so a type nothing ever sends fails the codegen policy.
        opts.Discovery.CustomizeHandlerDiscovery(types => types.Excludes.Implements<IAuthorizationHandler>());

        // Pin the application assembly explicitly. Wolverine otherwise infers it with a
        // stack walk whose result is cached in a process-wide static (JasperFxOptions.
        // RememberedApplicationAssembly), so in a test process that stands up several hosts
        // the first one to boot decides for all of them. Every host in the suite currently
        // boots this same Program.cs, so the inferred value is already Wallow.Api — this is
        // preventive, not a fix. The setter also Fills the discovery collection, which is why
        // Wallow.Api appears twice in the assembly list; that duplicate is harmless.
        opts.ApplicationAssembly = typeof(WallowModules).Assembly;

        // Give every handler for a message type its own chain, its own local queue, and its own
        // retry loop. Under the default (ClassicCombineIntoOneLogicalHandler) all handlers for a
        // message are welded into ONE logical handler behind ONE retry loop, so a failure in a
        // late handler re-runs the earlier ones that already committed — duplicate emails,
        // duplicate notification rows. Four message types have more than one handler today:
        // EmailVerifiedEvent (2, and it crosses a module boundary: Inquiries links the submitter,
        // Notifications sends the welcome email), InquirySubmittedEvent (3),
        // InquiryCommentAddedEvent (3) and InquiryStatusChangedEvent (2). Under the default a
        // Notifications failure retried the Inquiries link-up — a module boundary violated by a
        // retry policy. Separated is what JasperFx recommends for modular monoliths, and
        // MultipleHandlerSeparationTests pins the behaviour.
        //
        // Consequence to know about: the handlers for one message now run CONCURRENTLY on
        // independent local queues. There is no ordering between the email send, the in-app
        // notification write and the SSE push any more; none of them depended on it.
        opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;

        // Discover handlers in exactly the assemblies the enabled modules declare — never by
        // scanning the AppDomain. The old scan was correct only by coincidence: a disabled module's
        // .Application assembly stays unloaded purely because its AddXModule body never ran and so
        // never touched a type in it. A refactor moving that type to .Domain would have dropped
        // every handler in that assembly with no error at all, just fewer chains. Registering by
        // [assembly: WolverineModule] instead is not an option: it discovers unconditionally, so a
        // DISABLED module's handlers would be found, codegen would build chains for them, and
        // ServiceLocationPolicy.NotAllowed above would throw on DI that was never registered.
        foreach (Assembly assembly in handlerAssemblies)
        {
            opts.Discovery.IncludeAssembly(assembly);
        }

        // Align message storage schema across all stores (PostgreSQL)
        // This prevents conflicts when PersistMessagesWithPostgresql() is used
        opts.Durability.MessageStorageSchemaName = "wolverine";

        // PostgreSQL persistence for the durable outbox/inbox. Registered in EVERY environment,
        // Testing included. Wolverine.EntityFrameworkCore's EfCoreEnvelopeTransaction throws
        // "This Wolverine application is not using Database backed message persistence" the first
        // time a transactional handler chain runs against a host with no message store, so a host
        // without one cannot execute a transactional chain at all — and leaving Testing without a
        // store would make the transactional path the only path production takes but no test does.
        // The test host already owns a Testcontainers Postgres (Wallow.Tests.Common's
        // WallowApiFactory), and Wolverine migrates its own "wolverine" schema into it on startup:
        // AutoBuildMessageStorageOnStartup defaults to CreateOrUpdate.
        string pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not configured");
        opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine");

        if (builder.Environment.IsEnvironment("Testing"))
        {
            // Solo mode skips leadership election and node assignment, and starts the durability
            // agents immediately. Every test class boots its own host against its own throwaway
            // container, so a second node never exists; Balanced mode would only add its election
            // round-trips to each of those cold starts. This is JasperFx's own recommendation for
            // test harnesses (guide/testing.html — "Running Wolverine in Solo Mode").
            opts.Durability.Mode = DurabilityMode.Solo;
        }

        // EF Core transaction integration — enlist Wolverine messages in EF Core transactions.
        //
        // Lightweight, NOT the default Eager. Eager opens the unit of work with an explicit
        // DbContext.Database.BeginTransactionAsync(); all seven modules configure
        // npgsql.EnableRetryOnFailure(...) on their DbContext, which installs
        // NpgsqlRetryingExecutionStrategy, and that strategy refuses a user-initiated transaction
        // ("The configured execution strategy 'NpgsqlRetryingExecutionStrategy' does not support
        // user-initiated transactions"). Measured: Eager + AutoApplyTransactions returns 500 on
        // login in CrossOrgRoleIsolationTests, thrown out of the generated SendEmailCommand handler.
        // Lightweight instead treats DbContext.SaveChangesAsync() as the sole transaction boundary,
        // so the unit of work runs inside EF's own execution strategy and the retry policy survives.
        // Removing EnableRetryOnFailure from the modules was rejected: it would surrender
        // transient-fault retry on every DB call in the product, HTTP paths included, to fix a
        // message-handler problem.
        //
        // What Lightweight gives up: operations that bypass SaveChangesAsync are outside the
        // handler's unit of work. Exactly two exist — Notifications'
        // TenantPushConfigurationRepository (ExecuteDeleteAsync) and NotificationRepository
        // (ExecuteUpdateAsync). Both are single statements, atomic on their own.
        opts.UseEntityFrameworkCoreTransactions(TransactionMiddlewareMode.Lightweight);

        // Apply that transaction middleware to every chain whose service dependencies transitively
        // reach a module DbContext: the chain gains a <Module>DbContext.SaveChangesAsync
        // postprocessor, so the handler's writes and the outgoing messages it cascades commit
        // together instead of the handler saving mid-flight and the messages escaping a later
        // failure. Measured: 65 of the 88 top-level HandlerGraph.Chains, and 66 of the 94
        // HandlerGraph.AllChains() — the pair differs because MultipleHandlerBehavior.Separated
        // replaces each of the four multi-handler message types' parent chain with its per-handler
        // sticky sub-chains (88 - 4 + 10 = 94). The policy does reach those sub-chains: exactly one
        // of the ten is transactional (EmailVerifiedEvent's Inquiries handler, the only one of the
        // ten that touches a DbContext), which is the +1. Quote AllChains() when quoting one number.
        //
        // Two things this makes newly fatal, both currently clean:
        //   - A chain whose transitive dependencies reach MORE THAN ONE DbContext throws out of
        //     EFCorePersistenceFrameProvider.DetermineDbContextType at codegen time — i.e. on the
        //     first message of that type, not at startup. Keep a handler inside one module's
        //     DbContext; cross-module work goes through an integration event, not a second
        //     repository.
        //   - A handler that saves explicitly and then dispatches (SendNotificationHandler pushes
        //     to realtime after its save) must keep that ordering. Dropping the explicit save so
        //     the postprocessor covers it would move the push BEFORE the commit and give SSE
        //     consumers a read-your-writes race.
        opts.Policies.AutoApplyTransactions();

        // Standard error handling policies (retry, DLQ)
        opts.ConfigureStandardErrorHandling();
        opts.ConfigureMessageLogging();

        // FluentValidation middleware — validates commands before handlers.
        // Each module registers its own validators (AddXApplication -> AddValidatorsFromAssembly),
        // so Wolverine must NOT also discover them: its scan appends registrations with a plain
        // IServiceCollection.Add, leaving two IValidator<T> entries per command. Two registrations
        // flip FluentValidationPolicy from ExecuteOne(IValidator<T>) to
        // ExecuteMany(IEnumerable<IValidator<T>>), and the enumerable is service-located from the
        // root provider — which throws "Cannot resolve scoped service
        // 'IEnumerable<IValidator<T>>' from root provider" under Development scope validation.
        opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);

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
        // ExtensionDiscovery.ManualOnly — disables IWolverineExtension auto-discovery, which is
        // why UseRuntimeCompilation() above must be called explicitly. It is NOT a guard against
        // the pre-6.0 bin-directory scan; that scan no longer exists. See the comment on the
        // UseWolverine call itself.
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

    // This host owns the open connections, so it owns the only implementation that can close
    // them; the identity module registers a no-op default for hosts that serve none.
    builder.Services.AddSingleton<RealtimeConnectionRegistry>();
    builder.Services.AddSingleton<IRealtimeAccessRevoker, RealtimeAccessRevoker>();

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
    IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews();

    // A disabled module must have no HTTP surface, not a broken one. Dropping its application part
    // here — while the part list is what AddControllersWithViews just populated, and before anything
    // reads a feature off it — keeps its controllers out of the ActionDescriptorCollection that
    // routing, Asp.Versioning's ApiExplorer and the OpenAPI document generator all read from.
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
    await Wallow.Api.WallowModules.InitializeWallowModulesAsync(app, enabledModules);
    await app.InitializeAppAuditingAsync();
    await app.InitializeAuthAuditingAsync();

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

    // Exception handling. Parameterless on purpose: GlobalExceptionHandler (IExceptionHandler)
    // handles everything, with the problem-details writer as the built-in fallback. The previous
    // "/error" re-execution path pointed at a route that never existed.
    app.UseExceptionHandler();

    // Any 4xx/5xx that reaches the client with an empty body gets a problem+json document
    // (405s, framework-issued 415s, ...). A body is load-bearing under the nosniff header:
    // browsers turn an empty, Content-Type-less error navigation into a file download.
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
            Version = appVersion,
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
    if (enabledModules.IsModuleEnabled<ApiKeysModule>())
    {
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }

    // Authentication (OpenIddict token validation)
    app.UseAuthentication();

    // Tenant resolution (reads org claim from JWT → populates ITenantContext)
    // Note: For API key auth, tenant is already set by ApiKeyAuthenticationMiddleware
    app.UseMiddleware<TenantResolutionMiddleware>();

    // Tenant observability (sets wallow.tenant_id on Activity tag + W3C Baggage for downstream propagation)
    app.UseMiddleware<TenantBaggageMiddleware>();

    // Permission expansion (reads roles → expands to PermissionType claims)
    app.UseMiddleware<PermissionExpansionMiddleware>();

    // Unmatched paths become a 404 problem before authorization runs. With no endpoint, the
    // authorization FallbackPolicy challenges the request, and under the nosniff header a
    // browser navigation to a typo'd URL downloads an empty file instead of showing an error.
    // Deliberately a middleware rather than a MapFallback endpoint: a catch-all endpoint joins
    // every routing DFA node, and ConsumesMatcherPolicy then drops controllers that declare
    // [Consumes] from the no-Content-Type edge — every bodyless GET on such a controller would
    // 404. It would also swallow 405s. Endpoint-less handlers that run earlier (OpenIddict's
    // request handlers inside UseAuthentication) are unaffected; the Hangfire dashboard runs
    // later and owns its path, so it is exempted here.
    app.Use(static async (context, next) =>
    {
        if (context.GetEndpoint() is null && !context.Request.Path.StartsWithSegments("/hangfire"))
        {
            // UseStatusCodePages turns the empty response into a problem+json body.
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });

    // Authorization (checks [HasPermission] attributes)
    app.UseAuthorization();

    // Session management (track activity on the active-session ledger)
    app.UseSessionActivity();

    // Module tagging (tags HTTP requests with wallow.module for observability)
    app.UseMiddleware<ModuleTaggingMiddleware>();

    // Service account usage tracking
    app.UseServiceAccountTracking();

    // Hangfire dashboard
    app.UseHangfireDashboard();

    // Endpoints
    app.MapControllers();

    app.MapHub<RealtimeHub>("/hubs/realtime");
    // Kept out of the API description: the stream is text/event-stream, so there is no response
    // body schema to publish and its untyped 200 would generate an SDK client method returning
    // unknown. Browsers consume it through EventSource, not the generated client.
    app.MapGet("/events", SseEndpoint.HandleSseConnection).RequireAuthorization().ExcludeFromDescription();
    app.MapAsyncApiEndpoints(handlerAssemblies);

    // API-level recurring jobs (use DI-based IRecurringJobManager, not static RecurringJob)
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
