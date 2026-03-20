using Wallow.Identity.Application.Interfaces;

using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;

using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wallow.Identity.Infrastructure.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddIdentityCore<WallowUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<WallowRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>()
                    .ReplaceDefaultEntities<Guid>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetEndSessionEndpointUris("/connect/logout")
                    .SetUserInfoEndpointUris("/connect/userinfo");

                options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()
                    .AllowClientCredentialsFlow()
                    .AllowRefreshTokenFlow();

                options.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

                OpenIddictServerAspNetCoreBuilder aspNetCoreBuilder = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough();

                if (environment.IsDevelopment())
                {
                    aspNetCoreBuilder.DisableTransportSecurityRequirement();
                }

                options.RegisterScopes(
                    "openid", "profile", "email", "roles", "offline_access",
                    "billing.read", "billing.manage",
                    "invoices.read", "invoices.write",
                    "payments.read", "payments.write",
                    "subscriptions.read", "subscriptions.write",
                    "users.read", "users.write", "users.manage",
                    "roles.read", "roles.write", "roles.manage",
                    "organizations.read", "organizations.write", "organizations.manage",
                    "apikeys.read", "apikeys.write", "apikeys.manage",
                    "sso.read", "sso.manage", "scim.manage",
                    "storage.read", "storage.write",
                    "messaging.access",
                    "announcements.read", "announcements.manage",
                    "changelog.manage",
                    "notifications.read", "notifications.write",
                    "configuration.read", "configuration.manage",
                    "inquiries.read", "inquiries.write",
                    "serviceaccounts.read", "serviceaccounts.write", "serviceaccounts.manage",
                    "webhooks.manage");
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.ConfigureApplicationCookie(options =>
        {
            string? cookieDomain = configuration["Authentication:CookieDomain"];
            if (!string.IsNullOrEmpty(cookieDomain))
            {
                options.Cookie.Domain = cookieDomain;
            }
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        });

        services.AddIdentityAuthorization();
        services.AddMultiTenancy();
        services.AddIdentityPersistence(configuration);
        // Settings registration skipped: IdentityDbContext inherits ASP.NET Identity's IdentityDbContext,
        // not TenantAwareDbContext<T>, so the generic AddSettings<T> constraint cannot be satisfied.
        // TODO: Implement a non-generic settings registration path for Identity module.
        services.AddIdentityServices();

        return services;
    }

    private static void AddIdentityPersistence(
        this IServiceCollection services, IConfiguration _)
    {
        services.AddDataProtection()
            .SetApplicationName("Wallow");

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
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
    }

    private static void AddIdentityAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "SmartScheme";
                options.DefaultChallengeScheme = "SmartScheme";
            })
            .AddCookie(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ExternalScheme)
            .AddScheme<AuthenticationSchemeOptions, ScimBearerAuthenticationHandler>("ScimBearer", null)
            .AddPolicyScheme("SmartScheme", "Smart cookie/bearer selector", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    string? authorization = context.Request.Headers.Authorization.FirstOrDefault();
                    if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                    }

                    return IdentityConstants.ApplicationScheme;
                };
            });
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
        services.AddScoped<IUserManagementService, UserManagementService>();

        services.AddScoped<IApiKeyService, RedisApiKeyService>();
        services.AddScoped<IServiceAccountService, OpenIddictServiceAccountService>();
        services.AddScoped<ISsoService, OidcFederationService>();
        services.AddScoped<ScimUserService>();
        services.AddScoped<ScimGroupService>();
        services.AddScoped<IScimService, ScimService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IDeveloperAppService, OpenIddictDeveloperAppService>();
        services.AddScoped<Wallow.Shared.Contracts.Identity.IUserService, UserService>();
        services.AddScoped<Wallow.Shared.Contracts.Identity.IUserQueryService, UserQueryService>();
    }
}
