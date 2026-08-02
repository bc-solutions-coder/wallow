using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.SeederService;

/// <summary>
/// The seeder hand-picks a subset of the Identity Infrastructure DI graph instead of calling that
/// module's own <c>AddIdentityModule</c> extension — it has no HTTP pipeline and needs none of the
/// module's MFA/session/invitation services. Extracted out of Program.cs's top-level statements so
/// a test can build this exact container and prove every service <see cref="SeederWorker"/> resolves
/// is actually constructible. This hand-picking is exactly why the class of bug this guards against
/// happens: a constructor dependency added to a service (e.g. <see cref="ILastOwnerGuard"/> on
/// <see cref="OrganizationService"/>) gets registered in the module's own extension but silently
/// never makes it here (Wallow-smvc).
/// </summary>
internal static class SeederServiceCollectionExtensions
{
    internal static IServiceCollection AddSeederIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        // IdentityDbContext requires IDataProtectionProvider
        services.AddDataProtection();

        // Register IdentityDbContext
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

        // ASP.NET Identity
        services.AddIdentityCore<WallowUser>(opts =>
            {
                opts.Password.RequiredLength = 8;
                opts.User.RequireUniqueEmail = true;
                opts.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<WallowRole>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        // OpenIddict Core only (no Server — seeder just manages client/scope data)
        services.AddOpenIddict()
            .AddCore(opts =>
            {
                opts.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>()
                    .ReplaceDefaultEntities<Guid>();
            });

        // Multi-tenancy
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());

        // Identity services needed by seeders
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IMembershipRoleResolver, MembershipRoleResolver>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<ILastOwnerGuard, LastOwnerGuard>();
        services.AddMembershipAccessRevocation();
        services.AddScoped<PreRegisteredClientSyncService>();
        services.AddScoped<OrganizationSeedSyncService>();
        services.AddScoped<OpenIddictScopeSyncService>();
        services.AddScoped<IBootstrapAdminService, BootstrapAdminService>();
        services.AddScoped<ISetupStatusChecker, SetupStatusChecker>();
        services.AddScoped<DefaultRoleSeeder>();
        services.AddScoped<ApiScopeSeeder>();

        // TimeProvider
        services.AddSingleton(TimeProvider.System);

        // NullMessageBus — OrganizationService requires IMessageBus but the seeder never dispatches messages
        services.AddSingleton<IMessageBus>(new NullMessageBus());

        // Map SeedOptions.Clients into PreRegisteredClientOptions
        services.Configure<PreRegisteredClientOptions>(opts =>
        {
            SeedOptions? seed = configuration.Get<SeedOptions>();
            if (seed?.Clients is not null)
            {
                foreach (PreRegisteredClientDefinition client in seed.Clients)
                {
                    opts.Clients.Add(client);
                }
            }
        });

        // Map SeedOptions.Organizations into SeedOrganizationOptions
        services.Configure<SeedOrganizationOptions>(opts =>
        {
            SeedOptions? seed = configuration.Get<SeedOptions>();
            if (seed?.Organizations is not null)
            {
                foreach (SeedOrganizationDefinition organization in seed.Organizations)
                {
                    opts.Organizations.Add(organization);
                }
            }
        });

        return services;
    }
}
