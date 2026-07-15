using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;
using Wallow.SeederService;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Load seed.json: prefer SEED_FILE_PATH env var, fall back to bundled file
string seedFilePath = Environment.GetEnvironmentVariable("SEED_FILE_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "seed.json");

builder.Configuration.AddJsonFile(seedFilePath, optional: false, reloadOnChange: false);

// Load environment-specific overrides (e.g. seed.Development.json)
string seedDir = Path.GetDirectoryName(seedFilePath) ?? AppContext.BaseDirectory;
string seedEnvPath = Path.Combine(seedDir, $"seed.{builder.Environment.EnvironmentName}.json");
builder.Configuration.AddJsonFile(seedEnvPath, optional: true, reloadOnChange: false);

// Re-add environment variables so they take precedence over seed.json values.
// Host.CreateApplicationBuilder adds env vars early, but seed.json (added above) overrides them.
// This ensures Docker/CI overrides like Clients__1__RedirectUris__0 are applied.
builder.Configuration.AddEnvironmentVariables();

// Bind SeedOptions from config root
builder.Services.Configure<SeedOptions>(builder.Configuration);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

// IdentityDbContext requires IDataProtectionProvider
builder.Services.AddDataProtection();

// Register IdentityDbContext
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

// ASP.NET Identity
builder.Services.AddIdentityCore<WallowUser>(opts =>
    {
        opts.Password.RequiredLength = 8;
        opts.User.RequireUniqueEmail = true;
        opts.SignIn.RequireConfirmedEmail = true;
    })
    .AddRoles<WallowRole>()
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

// OpenIddict Core only (no Server — seeder just manages client/scope data)
builder.Services.AddOpenIddict()
    .AddCore(opts =>
    {
        opts.UseEntityFrameworkCore()
            .UseDbContext<IdentityDbContext>()
            .ReplaceDefaultEntities<Guid>();
    });

// Multi-tenancy
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());

// Identity services needed by seeders
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<PreRegisteredClientSyncService>();
builder.Services.AddScoped<IBootstrapAdminService, BootstrapAdminService>();
builder.Services.AddScoped<ISetupStatusChecker, SetupStatusChecker>();
builder.Services.AddScoped<DefaultRoleSeeder>();
builder.Services.AddScoped<ApiScopeSeeder>();

// TimeProvider
builder.Services.AddSingleton(TimeProvider.System);

// NullMessageBus — OrganizationService requires IMessageBus but the seeder never dispatches messages
builder.Services.AddSingleton<IMessageBus>(new NullMessageBus());

// Map SeedOptions.Clients into PreRegisteredClientOptions
builder.Services.Configure<PreRegisteredClientOptions>(opts =>
{
    SeedOptions? seed = builder.Configuration.Get<SeedOptions>();
    if (seed?.Clients is not null)
    {
        foreach (PreRegisteredClientDefinition client in seed.Clients)
        {
            opts.Clients.Add(client);
        }
    }
});

builder.Services.AddHostedService<SeederWorker>();

IHost host = builder.Build();
await host.RunAsync();
