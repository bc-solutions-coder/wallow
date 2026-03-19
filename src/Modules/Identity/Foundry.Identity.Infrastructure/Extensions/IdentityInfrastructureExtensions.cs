using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Application.Settings;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Identity.Infrastructure.Repositories;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Infrastructure.Settings;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Identity.Infrastructure.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<FoundryUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<FoundryRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetLogoutEndpointUris("/connect/logout")
                    .SetUserInfoEndpointUris("/connect/userinfo");

                options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()
                    .AllowClientCredentialsFlow()
                    .AllowRefreshTokenFlow();

                options.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableLogoutEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough();

                options.RegisterScopes("openid", "profile", "email", "roles");
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddIdentityAuthorization();
        services.AddMultiTenancy();
        services.AddIdentityPersistence(configuration);
        services.AddSettings<IdentityDbContext, IdentitySettingKeys>("identity");
        services.AddIdentityServices();

        return services;
    }

    private static void AddIdentityPersistence(
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
        services.AddScoped<IServiceAccountUnfilteredRepository>(sp =>
            (IServiceAccountUnfilteredRepository)sp.GetRequiredService<IServiceAccountRepository>());
        services.AddScoped<IApiScopeRepository, ApiScopeRepository>();
        services.AddScoped<ISsoConfigurationRepository, SsoConfigurationRepository>();
        services.AddScoped<IScimConfigurationRepository, ScimConfigurationRepository>();
        services.AddScoped<IScimSyncLogRepository, ScimSyncLogRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
    }

    private static void AddIdentityAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ExternalScheme)
            .AddScheme<AuthenticationSchemeOptions, ScimBearerAuthenticationHandler>("ScimBearer", null);
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddSingleton<IRolePermissionLookup, RolePermissionLookup>();
    }

    private static void AddMultiTenancy(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
    }

    private static void AddIdentityServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<ITokenService, KeycloakTokenService>();
        services.AddScoped<IApiKeyService, RedisApiKeyService>();
        services.AddScoped<IServiceAccountService, KeycloakServiceAccountService>();
        services.AddScoped<SsoClaimsSyncService>();
        services.AddScoped<ISsoService, KeycloakSsoService>();
        services.AddScoped<ScimUserService>();
        services.AddScoped<ScimGroupService>();
        services.AddScoped<IScimService, ScimService>();
        services.AddScoped<IDeveloperAppService, KeycloakDeveloperAppService>();
        services.AddScoped<Foundry.Shared.Contracts.Identity.IUserService, UserService>();
        services.AddScoped<Foundry.Shared.Contracts.Identity.IUserQueryService, UserQueryService>();
    }
}
