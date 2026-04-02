using System.Text.Json;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using StackExchange.Redis;
using Wallow.ServiceDefaults;
using Wallow.Web;
using Wallow.Web.Configuration;
using Wallow.Web.Middleware;
using Wallow.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Load branding configuration
BrandingOptions branding = new();
string brandingPath = Path.Combine(builder.Environment.ContentRootPath, "branding.json");
if (!File.Exists(brandingPath))
{
    brandingPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "branding.json");
}

if (File.Exists(brandingPath))
{
    try
    {
        string json = await File.ReadAllTextAsync(brandingPath);
        branding = JsonSerializer.Deserialize<BrandingOptions>(json, JsonSerializerOptions.Web) ?? new();
    }
    catch (JsonException)
    {
        // Malformed branding.json — fall back to defaults
    }
}

builder.Services.AddSingleton(branding);

// DataProtection — persist keys to Valkey so antiforgery/OIDC correlation cookies survive restarts.
// Application name and Redis key must match the API (IdentityInfrastructureExtensions.cs:187-189).
// Uses abortConnect=true so startup fails fast if Redis is unreachable (prevents silent fallback
// to ephemeral keys, which causes login loops when correlation cookies can't be decrypted).
string? redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    ConfigurationOptions redisOptions = ConfigurationOptions.Parse(redisConnection);
    redisOptions.AbortOnConnectFail = true;
    IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(redisOptions);
    builder.Services.AddDataProtection()
        .SetApplicationName("Wallow")
        .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
}
else if (builder.Environment.IsDevelopment())
{
    DirectoryInfo keysDir = new(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".keys"));
    builder.Services.AddDataProtection()
        .SetApplicationName("Wallow")
        .PersistKeysToFileSystem(keysDir);
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(1);
        options.MaxBufferedUnacknowledgedRenderBatches = 10;
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });

builder.Services.AddBlazorBlueprintComponents();

// On non-HTTPS (local dev), downgrade SameSite=None → Unspecified so the browser defaults to
// Lax instead of rejecting the cookie (browsers require Secure with SameSite=None).
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.OnAppendCookie = context =>
    {
        if (context.CookieOptions.SameSite == SameSiteMode.None && !context.Context.Request.IsHttps)
        {
            context.CookieOptions.SameSite = SameSiteMode.Unspecified;
            context.CookieOptions.Secure = false;
        }
    };
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    })
    .AddOpenIdConnect(options =>
    {
        // OIDC uses response_mode=form_post: the auth server POSTs back to the callback URL.
        // In production (HTTPS, separate domains), this is a cross-site POST requiring
        // SameSite=None + Secure. On localhost HTTP, browsers reject that combination, so
        // CookiePolicyMiddleware (below) downgrades None → Unspecified on non-HTTPS requests,
        // letting the browser default to Lax (which works for same-site localhost).
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.NonceCookie.SameSite = SameSiteMode.None;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Authority = builder.Configuration["Oidc:Authority"]
            ?? builder.Configuration["ServiceUrls:AuthUrl"]
            ?? "http://localhost:5001";
        // When MetadataAddress is configured, discovery happens over the internal network (HTTP)
        // while Authority remains HTTPS for browser-facing redirects — safe to skip HTTPS check.
        bool hasInternalMetadata = !string.IsNullOrEmpty(builder.Configuration["Oidc:MetadataAddress"]);
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment() && !hasInternalMetadata;

        // When MetadataAddress differs from Authority (e.g. containers that can't reach the browser-facing
        // authority), fetch OIDC discovery from MetadataAddress but rewrite endpoint URLs to use Authority
        // so browser redirects go to the same-site authority (avoids SameSite cookie issues).
        string? metadataAddress = builder.Configuration["Oidc:MetadataAddress"];
        if (!string.IsNullOrEmpty(metadataAddress))
        {
            options.MetadataAddress = metadataAddress;
            string authority = options.Authority!.TrimEnd('/');

            // The authority URL may include a path prefix (e.g. "https://wallow.dev/api").
            // Discovery endpoints from the internal MetadataAddress lack this prefix
            // (e.g. "http://wallow-api:8080/connect/authorize"). The rewrite must prepend
            // the authority's path so browser redirects hit the correct reverse-proxy route.
            string authorityPath = new UriBuilder(authority).Path.TrimEnd('/');

            options.Events.OnRedirectToIdentityProvider = context =>
            {
                UriBuilder uri = new(context.ProtocolMessage.IssuerAddress);
                UriBuilder authorityUri = new(authority);
                uri.Scheme = authorityUri.Scheme;
                uri.Host = authorityUri.Host;
                uri.Port = authorityUri.Port;
                if (!string.IsNullOrEmpty(authorityPath) && !uri.Path.StartsWith(authorityPath, StringComparison.OrdinalIgnoreCase))
                {
                    uri.Path = authorityPath + uri.Path;
                }
                context.ProtocolMessage.IssuerAddress = uri.ToString();
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToIdentityProviderForSignOut = context =>
            {
                if (context.ProtocolMessage.IssuerAddress is not null)
                {
                    UriBuilder uri = new(context.ProtocolMessage.IssuerAddress);
                    UriBuilder authorityUri = new(authority);
                    uri.Scheme = authorityUri.Scheme;
                    uri.Host = authorityUri.Host;
                    uri.Port = authorityUri.Port;
                    if (!string.IsNullOrEmpty(authorityPath) && !uri.Path.StartsWith(authorityPath, StringComparison.OrdinalIgnoreCase))
                    {
                        uri.Path = authorityPath + uri.Path;
                    }
                    context.ProtocolMessage.IssuerAddress = uri.ToString();
                }
                return Task.CompletedTask;
            };
        }
        options.ClientId = builder.Configuration["Oidc:ClientId"] ?? "wallow-web-client";
        options.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.CallbackPath = "/signin-oidc";

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("roles");

        options.Events.OnAuthenticationFailed = context =>
        {
            ILogger logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Wallow.Web.OidcAuth");
            OidcLogMessages.AuthenticationFailed(logger, context.Exception);
            return Task.CompletedTask;
        };
        options.Events.OnRemoteFailure = context =>
        {
            ILogger logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Wallow.Web.OidcAuth");
            OidcLogMessages.RemoteFailure(logger, context.Failure);
            return Task.CompletedTask;
        };
        options.Events.OnAuthorizationCodeReceived = context =>
        {
            ILogger logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Wallow.Web.OidcAuth");
            OidcLogMessages.OnAuthorizationCodeReceived(logger, context.Scheme.Name);
            return Task.CompletedTask;
        };
        options.Events.OnTokenValidated = context =>
        {
            ILogger logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Wallow.Web.OidcAuth");
            string subject = context.Principal?.FindFirst("sub")?.Value ?? "unknown";
            string issuer = context.Options.Authority ?? "unknown";
            OidcLogMessages.OnTokenValidated(logger, subject, issuer);
            return Task.CompletedTask;
        };
        options.Events.OnTokenResponseReceived = context =>
        {
            ILogger logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Wallow.Web.OidcAuth");
            OidcLogMessages.OnTokenResponseReceived(logger, context.Scheme.Name);
            return Task.CompletedTask;
        };
    });

builder.Services.AddHttpClient("WallowApi", client =>
{
    client.BaseAddress = new Uri("https+http://wallow-api");
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddScoped<IAppRegistrationService, AppRegistrationService>();
builder.Services.AddScoped<IOrganizationApiService, OrganizationApiService>();
builder.Services.AddScoped<IInquiryService, InquiryService>();
builder.Services.AddScoped<IMfaApiClient, MfaApiClient>();

builder.Services.AddHealthChecks()
    .Add(new HealthCheckRegistration(
        "api",
        sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new Wallow.Shared.Api.ApiHealthCheck(httpClientFactory, "WallowApi");
        },
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]));

WebApplication app = builder.Build();

// Opt-in PathBase for reverse-proxy path-based routing (e.g. /app)
string? pathBase = app.Configuration["PathBase"];
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

if (!app.Environment.IsDevelopment())
{
    ForwardedHeadersOptions forwardedHeadersOptions = new()
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
    };
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);

    app.UseHsts();
}

app.UseHttpsRedirection();

// Blazor Server requires 'unsafe-inline' for scripts (SignalR reconnection UI, error boundaries)
// and WebSocket connections for the SignalR circuit.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "connect-src 'self' ws: wss:; " +
        "img-src 'self' data:";

    await next();
});

app.UseStaticFiles();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TokenCaptureMiddleware>();
app.UseAntiforgery();

app.MapDefaultEndpoints();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        object response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                error = e.Value.Exception?.Message
            })
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.MapGet("/authentication/login", (string? returnUrl) =>
{
    // Only allow local/relative URLs to prevent open redirect attacks
    string redirectUri = "/dashboard/apps";
    if (!string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative) && returnUrl.StartsWith('/'))
    {
        redirectUri = returnUrl;
    }

    return TypedResults.Challenge(new AuthenticationProperties
    {
        RedirectUri = redirectUri
    }, [OpenIdConnectDefaults.AuthenticationScheme]);
});

app.MapGet("/authentication/logout", () =>
    TypedResults.SignOut(new AuthenticationProperties { RedirectUri = "/authentication/login" },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

app.MapRazorComponents<Wallow.Web.Components.App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();

