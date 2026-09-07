using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenIddict.Server;
using StackExchange.Redis;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Application.Settings;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Modules;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Identity.Infrastructure.Services.ExtensionPoints;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Contracts.Setup;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Infrastructure.RateLimiting;
using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.Identity;
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
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<WallowUserClaimsPrincipalFactory>();

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>()
                    .ReplaceDefaultEntities<Guid>();
            })
            .AddServer(options =>
            {
                // Relative paths preserve the reverse proxy PathBase.
                options.SetAuthorizationEndpointUris(OpenIddictEndpointUris.Authorization)
                    .SetTokenEndpointUris(OpenIddictEndpointUris.Token)
                    .SetEndSessionEndpointUris(OpenIddictEndpointUris.EndSession)
                    .SetUserInfoEndpointUris(OpenIddictEndpointUris.UserInfo)
                    // OpenIddict handles revocation without a passthrough controller.
                    .SetRevocationEndpointUris(OpenIddictEndpointUris.Revocation);

                // Advertise the configured public issuer when requests arrive through a proxy.
                Uri? issuer = OpenIddictIssuerResolver.Resolve(configuration);
                if (issuer is not null)
                {
                    options.SetIssuer(issuer);
                }

                options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()
                    .AllowClientCredentialsFlow()
                    .AllowRefreshTokenFlow();


                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(configuration.GetValue("OpenIddict:AccessTokenLifetimeMinutes", 15)));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(configuration.GetValue("OpenIddict:RefreshTokenLifetimeDays", 7)));
                options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(configuration.GetValue("OpenIddict:IdentityTokenLifetimeMinutes", 10)));

                // Rotate refresh tokens so redemption records can detect reuse.
                options.Configure(o => o.DisableRollingRefreshTokens = false);

                // Preserve the original expiry across refreshes.
                options.DisableSlidingRefreshTokenExpiration();

                // Allow concurrent refresh retries within the configured reuse window.
                options.SetRefreshTokenReuseLeeway(TimeSpan.FromSeconds(
                    configuration.GetValue("OpenIddict:RefreshTokenReuseLeewaySeconds", 30)));

                if (environment.IsDevelopment() || environment.EnvironmentName == "Testing")
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

                    EnsureCertificateExists(signingCertPath, signingCertPassword, "CN=Wallow Signing Certificate");
                    EnsureCertificateExists(encryptionCertPath, encryptionCertPassword, "CN=Wallow Encryption Certificate");

                    options.AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(signingCertPath, signingCertPassword))
                        .AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(encryptionCertPath, encryptionCertPassword));
                }

                // Resource servers validate signed access tokens without a decryption key.
                options.DisableAccessTokenEncryption();

                OpenIddictServerAspNetCoreBuilder aspNetCoreBuilder = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough();

                // Allow HTTP only in development/testing or through the explicit configuration opt-in.
                if (OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(environment, configuration))
                {
                    aspNetCoreBuilder.DisableTransportSecurityRequirement();
                }

                options.RegisterScopes(
                    "openid", "profile", "email", "roles", "offline_access",
                    "users.read", "users.write", "users.manage",
                    "roles.read", "roles.write", "roles.manage",
                    "organizations.read", "organizations.write", "organizations.manage",
                    "apikeys.read", "apikeys.write", "apikeys.manage",
                    "storage.read", "storage.write",
                    "announcements.read", "announcements.manage",
                    "changelog.manage",
                    "notifications.read", "notifications.write",
                    "configuration.read", "configuration.manage",
                    "inquiries.read", "inquiries.write",
                    "webhooks.manage");

                // Advertise Wallow front- and back-channel logout with session identifiers.
                options.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(builder =>
                    builder.UseInlineHandler(context =>
                    {
                        context.Metadata["frontchannel_logout_supported"] = true;
                        context.Metadata["frontchannel_logout_session_supported"] = true;
                        context.Metadata["backchannel_logout_supported"] = true;
                        context.Metadata["backchannel_logout_session_supported"] = true;
                        return default;
                    }));

                options.AddEventHandler(RefuseUnserviceableClientTokenRequests.Descriptor);
                options.AddEventHandler(RejectLockedOutClientTokenRequests.Descriptor);
                options.AddEventHandler(AuditInvalidClientTokenResponses.Descriptor);
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();

                // Accept access tokens addressed to this API.
                options.AddAudiences("wallow-api");

                // Check token storage so revoked JWTs can be rejected before expiry.
                options.EnableTokenEntryValidation();

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

            // Send the auth cookie across both prefixed API and unprefixed auth routes.
            options.Cookie.Path = "/";

            // Return API problems instead of redirecting to Identity login/access-denied pages.
            options.Events.OnRedirectToLogin = context =>
                AuthProblemResponse.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context =>
                AuthProblemResponse.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden);
        });

        services.AddIdentityAuthorization(configuration);
        services.AddMultiTenancy();
        services.AddIdentityPersistence(configuration);
        services.AddReadDbContext<IdentityDbContext>(configuration);
        services.AddSettings<IdentityDbContext, IdentitySettingKeys>("identity");
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
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", IdentityModule.Schema);
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
            TenantId tenantId = tenant.IsResolved ? tenant.TenantId : AmbientTenant.Current;
            ctx.SetTenant(tenantId);
            return ctx;
        });

        services.AddScoped<IApiScopeRepository, ApiScopeRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IRegisteredClientRepository, RegisteredClientRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IMembershipRoleResolver, MembershipRoleResolver>();
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

        // Register external providers when their client identifier is configured.
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
        services.AddScoped<IAuthorizationHandler, MfaPartialAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            // Require authentication on endpoints without explicit authorization metadata.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy("MfaPartial", policy =>
                policy.AddRequirements(new MfaPartialRequirement()));
        });
    }

    private static void AddMultiTenancy(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
    }

    /// <summary>
    /// Registers access-revocation services for the API and seeder.
    /// </summary>
    public static IServiceCollection AddAccessRevocation(this IServiceCollection services)
    {
        services.AddScoped<IAccessRevoker, AccessRevoker>();
        services.AddScoped<IClientAccessPolicy, ClientAccessPolicy>();
        services.AddScoped<IConnectedApplicationService, ConnectedApplicationService>();

        // Preserve a host-provided realtime revoker; other hosts use the no-op default.
        services.TryAddSingleton<IRealtimeAccessRevoker, NoOpRealtimeAccessRevoker>();

        return services;
    }

    private static void AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PreRegisteredClientOptions>(configuration.GetSection(PreRegisteredClientOptions.SectionName));
        services.Configure<AdminBootstrapOptions>(configuration.GetSection(AdminBootstrapOptions.SectionName));
        // Positive windows keep fallback Retry-After values valid.
        services.AddOptions<PasswordlessOptions>()
            .Bind(configuration.GetSection(PasswordlessOptions.SectionName))
            .Validate(
                options => options.RateLimitMaxRequests >= 0
                    && options.RateLimitWindow > TimeSpan.Zero
                    && options.MagicLinkTtl > TimeSpan.Zero
                    && options.OtpTtl > TimeSpan.Zero,
                "Passwordless:RateLimitMaxRequests must be non-negative and every Passwordless window and TTL must be positive.")
            .ValidateOnStart();
        services.Configure<InvalidClientLockoutOptions>(configuration.GetSection(InvalidClientLockoutOptions.SectionName));
        services.AddOptions<EmailChangeOptions>()
            .Bind(configuration.GetSection(EmailChangeOptions.SectionName))
            .Validate(
                options => options.RateLimitMaxRequests >= 0 && options.RateLimitWindow > TimeSpan.Zero,
                "Identity:EmailChange requires a non-negative cap and a positive window.")
            .ValidateOnStart();
        services.AddScoped<IEmailChangeRateLimiter, EmailChangeRateLimiter>();
        services.AddFixedWindowCounter();
        services.AddScoped<IInvalidClientLockout, InvalidClientLockout>();

        services.AddMemoryCache();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IBootstrapAdminService, BootstrapAdminService>();
        services.AddScoped<ISetupStatusChecker, SetupStatusChecker>();
        services.AddScoped<ISetupStatusProvider, SetupStatusProvider>();
        services.AddScoped<PreRegisteredClientSyncService>();
        services.AddScoped<OpenIddictScopeSyncService>();
        services.AddScoped<DefaultRoleSeeder>();

        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IOrganizationAccessPolicy, OrganizationAccessPolicy>();
        services.AddScoped<IOrganizationClientService, OrganizationClientService>();
        services.AddScoped<ITestSupportService, TestSupportService>();
        services.AddScoped<IAuthorizeContextService, AuthorizeContextService>();
        services.AddScoped<IClientTenantResolver, ClientTenantResolver>();
        services.AddScoped<IRedirectUriValidator, OpenIddictRedirectUriValidator>();
        services.TryAddScoped<Wallow.Shared.Contracts.Identity.IScopeSubsetValidator, ScopeSubsetValidator>();
        services.TryAddScoped<Wallow.Shared.Contracts.Identity.IOrganizationClientDirectory, OrganizationClientDirectory>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserQueryService, UserQueryService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IDefaultMemberRoleResolver, DefaultMemberRoleResolver>();
        services.AddScoped<IAccessRequestRecipientResolver, AccessRequestRecipientResolver>();
        services.AddScoped<IOrganizationAdminEmailResolver, OrganizationAdminEmailResolver>();
        services.AddScoped<IUserEnrollmentService, UserEnrollmentService>();
        services.AddScoped<IMembershipReviewService, MembershipReviewService>();
        services.AddScoped<ILastOwnerGuard, LastOwnerGuard>();
        services.AddAccessRevocation();

        // Preserve extension implementations registered before module setup.
        services.TryAddScoped<IClaimsEnricher, NoOpClaimsEnricher>();
        services.TryAddScoped<IRegistrationValidator, NoOpRegistrationValidator>();
        services.TryAddScoped<IExternalClaimsMapper, NoOpExternalClaimsMapper>();
        services.AddScoped<IMfaExemptionChecker, MfaExemptionChecker>();
        services.TryAddScoped<IMfaService, MfaService>();
        services.AddScoped<IMfaPartialAuthService, MfaPartialAuthService>();
        services.AddScoped<IOrganizationMfaPolicyService, OrganizationMfaPolicyService>();
        services.AddScoped<IMfaLockoutService, MfaLockoutService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ISsoClientSessionService, SsoClientSessionService>();

        // The notifier owns retries and timeouts; remove the host defaults to avoid duplicate retries.
        services.Configure<BackchannelLogoutOptions>(configuration.GetSection(BackchannelLogoutOptions.SectionName));
#pragma warning disable EXTEXP0001 // Experimental API removes inherited resilience handlers.
        services.AddHttpClient<IBackchannelLogoutNotifier, BackchannelLogoutNotifier>()
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        services.AddScoped<IPasswordlessService, PasswordlessService>();
        services.AddScoped<IConsentTokenService, ConsentTokenService>();

        services.AddSingleton<ServiceAccountUsageBuffer>();
        services.AddHostedService<ServiceAccountTrackingBackgroundService>();
    }

    private static void EnsureCertificateExists(string certPath, string certPassword, string subjectName)
    {
        if (File.Exists(certPath))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(certPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 cert = request.CreateSelfSigned(now, now.AddYears(10));

        byte[] pfxBytes = cert.Export(X509ContentType.Pfx, certPassword);
        File.WriteAllBytes(certPath, pfxBytes);
    }
}
