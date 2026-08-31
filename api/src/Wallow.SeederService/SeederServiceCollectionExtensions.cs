using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Modules;
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
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityModule.Schema)));

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
        // AccessRevoker (behind AddAccessRevocation) walks an org's registered clients.
        services.AddScoped<IRegisteredClientRepository, RegisteredClientRepository>();
        services.AddScoped<IOrganizationAdminEmailResolver, OrganizationAdminEmailResolver>();
        services.AddScoped<IMembershipRoleResolver, MembershipRoleResolver>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<ILastOwnerGuard, LastOwnerGuard>();
        services.AddAccessRevocation();
        services.AddScoped<PreRegisteredClientSyncService>();
        services.AddScoped<OrganizationSeedSyncService>();
        services.AddScoped<OpenIddictScopeSyncService>();
        services.AddScoped<IBootstrapAdminService, BootstrapAdminService>();
        // The exact handler POST /v1/identity/setup/admin invokes, called directly (the seeder
        // has no Wolverine runtime): bootstrap must mean the same thing on both paths.
        services.AddScoped<BootstrapAdminHandler>();
        services.AddScoped<ISetupStatusChecker, SetupStatusChecker>();
        services.AddScoped<DefaultRoleSeeder>();
        services.AddScoped<ApiScopeSeeder>();

        // TimeProvider
        services.AddSingleton(TimeProvider.System);

        // NullMessageBus — OrganizationService requires IMessageBus and IDbContextOutbox, but the
        // seeder never dispatches messages and never reaches the deletion outbox path.
        NullMessageBus nullBus = new();
        services.AddSingleton<IMessageBus>(nullBus);
        services.AddSingleton<Wolverine.EntityFrameworkCore.IDbContextOutbox>(nullBus);

        // Map SeedOptions.Clients into PreRegisteredClientOptions, attaching environment-supplied
        // secrets by clientId (see SeedOptions.ClientSecrets for why the contract is name-keyed).
        services.Configure<PreRegisteredClientOptions>(opts =>
        {
            SeedOptions? seed = configuration.Get<SeedOptions>();
            if (seed is null)
            {
                return;
            }

            HashSet<string> attachedSecretIds = new(StringComparer.OrdinalIgnoreCase);

            // The binder compacts sparse indices (Clients:0 + Clients:2 bind to positions 0 and 1),
            // so the raw section keys are the only way to name the index a stray override used.
            List<string> clientSectionKeys = configuration.GetSection("Clients").GetChildren()
                .Select(section => section.Key)
                .ToList();

            for (int index = 0; index < seed.Clients.Count; index++)
            {
                PreRegisteredClientDefinition client = seed.Clients[index];

                if (string.IsNullOrWhiteSpace(client.ClientId))
                {
                    string position = index < clientSectionKeys.Count
                        ? clientSectionKeys[index]
                        : index.ToString(CultureInfo.InvariantCulture);
                    throw new InvalidOperationException(
                        "Seed client entry at index " + position + " has a blank clientId. An index-based "
                        + "override (e.g. Clients__" + position + "__RedirectUris__0) points past the end of "
                        + "the seed file's \"clients\" array and materialised a phantom client. Align the "
                        + "override indices with the seed file, or remove the stray override.");
                }

                if (seed.ClientSecrets.TryGetValue(client.ClientId, out string? secret)
                    && !string.IsNullOrWhiteSpace(secret))
                {
                    client = client with { Secret = secret };
                    attachedSecretIds.Add(client.ClientId);
                }

                opts.Clients.Add(client);
            }

            List<string> orphanedSecretIds = seed.ClientSecrets
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value) && !attachedSecretIds.Contains(pair.Key))
                .Select(pair => pair.Key)
                .ToList();

            if (orphanedSecretIds.Count > 0)
            {
                throw new InvalidOperationException(
                    "ClientSecrets entries for " + string.Join(", ", orphanedSecretIds) + " match no client "
                    + "in the seed file. A secret aimed at an undefined client is a misconfiguration (typo'd "
                    + "clientId, or a client removed from the seed without removing its secret variable), so "
                    + "the seeder fails closed instead of dropping it.");
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
