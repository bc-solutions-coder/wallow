using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Identity.Infrastructure.Repositories;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.MultiTenancy;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Sdk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Foundry.Shared.Infrastructure.Core.Resilience;

namespace Foundry.Identity.Infrastructure.Extensions;

public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeycloakWebApiAuthentication(configuration, options =>
            {
                options.RequireHttpsMetadata = !Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                    ?.Equals("Development", StringComparison.OrdinalIgnoreCase) ?? true;
                options.Audience = "foundry-api";
            }, "Keycloak");

        services.AddIdentityAuthorization();
        services.AddMultiTenancy();
        services.AddIdentityPersistence(configuration);
        services.AddKeycloakAdmin(configuration);

        return services;
    }

    private static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services, IConfiguration _)
    {
        services.AddDataProtection()
            .SetApplicationName("Foundry");

        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            string? connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(30);
            });
            options.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IServiceAccountRepository, ServiceAccountRepository>();
        services.AddScoped<IApiScopeRepository, ApiScopeRepository>();
        services.AddScoped<ISsoConfigurationRepository, SsoConfigurationRepository>();
        services.AddScoped<IScimConfigurationRepository, ScimConfigurationRepository>();
        services.AddScoped<IScimSyncLogRepository, ScimSyncLogRepository>();

        return services;
    }

    private static IServiceCollection AddIdentityAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ScimBearerAuthenticationHandler>("ScimBearer", null);
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddSingleton<IRolePermissionLookup, RolePermissionLookup>();
        return services;
    }

    private static IServiceCollection AddMultiTenancy(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        return services;
    }

    private static IServiceCollection AddKeycloakAdmin(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));

        services.AddKeycloakAdminHttpClient(configuration);

        services.AddHttpClient("KeycloakAdminClient", (sp, client) =>
        {
            KeycloakOptions options = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
            client.BaseAddress = new Uri(options.AuthorityUrl);
        }).AddFoundryResilienceHandler("identity-provider");

        // Token service for auth proxy (no auth required on this client)
        services.AddHttpClient("KeycloakTokenClient").AddFoundryResilienceHandler("identity-provider");

        services.AddMemoryCache();
        services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();
        services.AddScoped<IKeycloakOrganizationService, KeycloakOrganizationService>();
        services.AddScoped<ITokenService, KeycloakTokenService>();
        services.AddScoped<IApiKeyService, RedisApiKeyService>();
        services.AddScoped<IServiceAccountService, KeycloakServiceAccountService>();
        services.AddScoped<SsoClaimsSyncService>();
        services.AddScoped<KeycloakIdpService>();
        services.AddScoped<ISsoService, KeycloakSsoService>();
        services.AddScoped<ScimUserService>();
        services.AddScoped<ScimGroupService>();
        services.AddScoped<IScimService, ScimService>();
        services.AddScoped<Foundry.Shared.Contracts.Identity.IUserService, UserService>();
        services.AddScoped<Foundry.Shared.Contracts.Identity.IUserQueryService, UserQueryService>();
        return services;
    }
}
