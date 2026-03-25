using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Commands.RegisterSetupClient;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Identity.Infrastructure.Services.ExtensionPoints;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Setup;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;


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

                // Token lifetimes (configurable via OpenIddict section)
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(configuration.GetValue("OpenIddict:AccessTokenLifetimeMinutes", 15)));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(configuration.GetValue("OpenIddict:RefreshTokenLifetimeDays", 7)));
                options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(configuration.GetValue("OpenIddict:IdentityTokenLifetimeMinutes", 10)));

                // OpenIddict uses rolling refresh tokens by default (old token is revoked
                // when a new one is issued), providing theft detection out of the box.
                options.DisableSlidingRefreshTokenExpiration();

                if (environment.IsDevelopment())
                {
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }
                else
                {
                    string signingCertPath = configuration["OpenIddict:SigningCertPath"]
                        ?? throw new InvalidOperationException("OpenIddict:SigningCertPath is required in non-development environments.");
                    string signingCertPassword = configuration["OpenIddict:SigningCertPassword"]
                        ?? throw new InvalidOperationException("OpenIddict:SigningCertPassword is required in non-development environments.");
                    string encryptionCertPath = configuration["OpenIddict:EncryptionCertPath"]
                        ?? throw new InvalidOperationException("OpenIddict:EncryptionCertPath is required in non-development environments.");
                    string encryptionCertPassword = configuration["OpenIddict:EncryptionCertPassword"]
                        ?? throw new InvalidOperationException("OpenIddict:EncryptionCertPassword is required in non-development environments.");

                    options.AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(signingCertPath, signingCertPassword))
                        .AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(encryptionCertPath, encryptionCertPassword));
                }

                // Disable access token encryption so tokens are standard JWTs
                // that can be validated by resource servers and inspected in tests.
                options.DisableAccessTokenEncryption();

                OpenIddictServerAspNetCoreBuilder aspNetCoreBuilder = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough();

                if (!environment.IsProduction())
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

        services.AddIdentityAuthorization(configuration);
        services.AddMultiTenancy();
        services.AddIdentityPersistence(configuration);
        services.AddReadDbContext<IdentityDbContext>(configuration);
        // Settings registration skipped: IdentityDbContext inherits ASP.NET Identity's IdentityDbContext,
        // not TenantAwareDbContext<T>, so the generic AddSettings<T> constraint cannot be satisfied.
        // TODO: Implement a non-generic settings registration path for Identity module.
        services.AddIdentityServices(configuration);

        return services;
    }

    private static void AddIdentityPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        IConnectionMultiplexer connectionMultiplexer = services
            .BuildServiceProvider()
            .GetRequiredService<IConnectionMultiplexer>();

        services.AddDataProtection()
            .SetApplicationName("Wallow")
            .PersistKeysToStackExchangeRedis(connectionMultiplexer, "DataProtection-Keys");

        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<IdentityDbContext>((_, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsqlOptions =>
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

        services.AddScoped<IdentityDbContext>(sp =>
        {
            IDbContextFactory<IdentityDbContext> factory = sp.GetRequiredService<IDbContextFactory<IdentityDbContext>>();
            IdentityDbContext ctx = factory.CreateDbContext();
            ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
            ctx.SetTenant(tenant.TenantId);
            return ctx;
        });

        services.AddScoped<IServiceAccountRepository, ServiceAccountRepository>();
        services.AddScoped<IServiceAccountUnfilteredRepository>(sp =>
            (IServiceAccountUnfilteredRepository)sp.GetRequiredService<IServiceAccountRepository>());
        services.AddScoped<IApiScopeRepository, ApiScopeRepository>();
        services.AddScoped<ISsoConfigurationRepository, SsoConfigurationRepository>();
        services.AddScoped<IScimConfigurationRepository, ScimConfigurationRepository>();
        services.AddScoped<IScimSyncLogRepository, ScimSyncLogRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IInitialAccessTokenRepository, InitialAccessTokenRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IOrganizationDomainRepository, OrganizationDomainRepository>();
        services.AddScoped<IMembershipRequestRepository, MembershipRequestRepository>();
    }

    private static void AddIdentityAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        AuthenticationBuilder authBuilder = services.AddAuthentication(options =>
            {
                options.DefaultScheme = "SmartScheme";
                options.DefaultChallengeScheme = "SmartScheme";
            })
            .AddCookie(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ExternalScheme);

        // External auth providers — only registered when credentials are configured
        string? googleClientId = configuration["Authentication:Google:ClientId"];
        if (!string.IsNullOrEmpty(googleClientId))
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }

        string? microsoftClientId = configuration["Authentication:Microsoft:ClientId"];
        if (!string.IsNullOrEmpty(microsoftClientId))
        {
            authBuilder.AddMicrosoftAccount(options =>
            {
                options.ClientId = microsoftClientId;
                options.ClientSecret = configuration["Authentication:Microsoft:ClientSecret"]!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }

        string? githubClientId = configuration["Authentication:GitHub:ClientId"];
        if (!string.IsNullOrEmpty(githubClientId))
        {
            authBuilder.AddGitHub(options =>
            {
                options.ClientId = githubClientId;
                options.ClientSecret = configuration["Authentication:GitHub:ClientSecret"]!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.Scope.Add("user:email");
            });
        }

        string? appleServiceId = configuration["Authentication:Apple:ServiceId"];
        if (!string.IsNullOrEmpty(appleServiceId))
        {
            authBuilder.AddApple(options =>
            {
                options.ClientId = appleServiceId;
                options.TeamId = configuration["Authentication:Apple:TeamId"]!;
                options.KeyId = configuration["Authentication:Apple:KeyId"]!;
                options.GenerateClientSecret = true;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }

        authBuilder
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

    private static void AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PreRegisteredClientOptions>(configuration.GetSection(PreRegisteredClientOptions.SectionName));
        services.Configure<AdminBootstrapOptions>(configuration.GetSection(AdminBootstrapOptions.SectionName));
        services.Configure<PasswordlessOptions>(configuration.GetSection(PasswordlessOptions.SectionName));

        services.AddMemoryCache();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IBootstrapAdminService, BootstrapAdminService>();
        services.AddScoped<ISetupStatusChecker, SetupStatusChecker>();
        services.AddScoped<ISetupClientService, SetupClientService>();
        services.AddScoped<ISetupStatusProvider, SetupStatusProvider>();
        services.AddScoped<PreRegisteredClientSyncService>();
        services.AddScoped<DefaultRoleSeeder>();

        services.AddScoped<IServiceAccountService, OpenIddictServiceAccountService>();
        services.AddScoped<ISsoService, OidcFederationService>();
        services.AddScoped<ScimUserService>();
        services.AddScoped<ScimGroupService>();
        services.AddScoped<IScimService, ScimService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IDeveloperAppService, OpenIddictDeveloperAppService>();
        services.AddScoped<IClientTenantResolver, ClientTenantResolver>();
        services.AddScoped<IRedirectUriValidator, OpenIddictRedirectUriValidator>();
        services.TryAddScoped<Wallow.Shared.Contracts.Identity.IScopeSubsetValidator, ScopeSubsetValidator>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserQueryService, UserQueryService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IDomainAssignmentService, DomainAssignmentService>();

        // Fork extension points — TryAddScoped allows forks to register their own implementations
        // before calling AddIdentityModule, which will skip these defaults.
        services.TryAddScoped<IClaimsEnricher, NoOpClaimsEnricher>();
        services.TryAddScoped<IRegistrationValidator, NoOpRegistrationValidator>();
        services.TryAddScoped<IExternalClaimsMapper, NoOpExternalClaimsMapper>();
        services.TryAddScoped<IMfaChallengeHandler, NoOpMfaChallengeHandler>();
        services.AddScoped<IMfaExemptionChecker, MfaExemptionChecker>();
        services.TryAddScoped<IMfaService, MfaService>();
        services.AddScoped<IPasswordlessService, PasswordlessService>();

        services.AddSingleton<ServiceAccountUsageBuffer>();
        services.AddHostedService<ServiceAccountTrackingBackgroundService>();
    }
}
