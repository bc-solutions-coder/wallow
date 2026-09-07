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
/// Registers the Identity services used by the seeder without an HTTP or Wolverine host.
/// </summary>
internal static class SeederServiceCollectionExtensions
{
    internal static IServiceCollection AddSeederIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {

        services.AddDataProtection();


        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityModule.Schema)));


        services.AddIdentityCore<WallowUser>(opts =>
            {
                opts.Password.RequiredLength = 8;
                opts.User.RequireUniqueEmail = true;
                opts.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<WallowRole>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        // Only OpenIddict persistence is needed for seed data.
        services.AddOpenIddict()
            .AddCore(opts =>
            {
                opts.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>()
                    .ReplaceDefaultEntities<Guid>();
            });


        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());


        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        // AccessRevoker resolves organization client registrations.
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
        // Reuse the setup endpoint bootstrap handler without a Wolverine runtime.
        services.AddScoped<BootstrapAdminHandler>();
        services.AddScoped<ISetupStatusChecker, SetupStatusChecker>();
        services.AddScoped<DefaultRoleSeeder>();
        services.AddScoped<ApiScopeSeeder>();


        services.AddSingleton(TimeProvider.System);

        // Seed service messages are discarded; outbox saves still persist the enrolled context.
        NullMessageBus nullBus = new();
        services.AddSingleton<IMessageBus>(nullBus);
        services.AddSingleton<Wolverine.EntityFrameworkCore.IDbContextOutbox>(nullBus);

        // Apply secrets by client id so overrides survive seed-array reordering.
        services.Configure<PreRegisteredClientOptions>(opts =>
        {
            SeedOptions? seed = configuration.Get<SeedOptions>();
            if (seed is null)
            {
                return;
            }

            HashSet<string> attachedSecretIds = new(StringComparer.OrdinalIgnoreCase);

            // Preserve raw index keys in errors because binding compacts sparse arrays.
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
