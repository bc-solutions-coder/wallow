using System.Text.Json;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Wallow.Web.Configuration;
using Wallow.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(1);
        options.MaxBufferedUnacknowledgedRenderBatches = 10;
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });

builder.Services.AddBlazorBlueprintComponents();

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
        // OIDC uses response_mode=form_post, which means the authorization server sends a POST back
        // to the callback URL. SameSite=Lax blocks cookies on cross-origin POSTs (even same-site),
        // so use None for the correlation/nonce cookies. Chromium allows SameSite=None without Secure
        // on localhost, and production should use HTTPS where Secure is naturally satisfied.
        // OIDC uses response_mode=form_post, which means the authorization server sends a POST back
        // to the callback URL. SameSite=Lax blocks cookies on cross-origin POSTs (even same-site),
        // so use None + Secure for the correlation/nonce cookies. Browsers treat localhost as a
        // secure context, so Secure cookies work over HTTP on localhost. In production, HTTPS
        // satisfies the Secure requirement naturally.
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.NonceCookie.SameSite = SameSiteMode.None;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Authority = builder.Configuration["Oidc:Authority"]
            ?? builder.Configuration["ServiceUrls:AuthUrl"]
            ?? "http://localhost:5001";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        // When MetadataAddress differs from Authority (e.g. containers that can't reach the browser-facing
        // authority), fetch OIDC discovery from MetadataAddress but rewrite endpoint URLs to use Authority
        // so browser redirects go to the same-site authority (avoids SameSite cookie issues).
        string? metadataAddress = builder.Configuration["Oidc:MetadataAddress"];
        if (!string.IsNullOrEmpty(metadataAddress))
        {
            options.MetadataAddress = metadataAddress;
            string authority = options.Authority!.TrimEnd('/');

            options.Events.OnRedirectToIdentityProvider = context =>
            {
                // Rewrite authorization_endpoint from discovery to use the browser-facing authority
                UriBuilder uri = new(context.ProtocolMessage.IssuerAddress);
                UriBuilder authorityUri = new(authority);
                uri.Scheme = authorityUri.Scheme;
                uri.Host = authorityUri.Host;
                uri.Port = authorityUri.Port;
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
            Console.WriteLine($"OIDC Auth Failed: {context.Exception}");
            return Task.CompletedTask;
        };
        options.Events.OnRemoteFailure = context =>
        {
            Console.WriteLine($"OIDC Remote Failure: {context.Failure}");
            return Task.CompletedTask;
        };
    });

string apiBaseUrl = builder.Configuration["ServiceUrls:ApiUrl"]
    ?? throw new InvalidOperationException("ServiceUrls:ApiUrl must be configured");

builder.Services.AddHttpClient("WallowApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 5;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAppRegistrationService, AppRegistrationService>();
builder.Services.AddScoped<IOrganizationApiService, OrganizationApiService>();
builder.Services.AddScoped<IInquiryService, InquiryService>();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
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
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok("Healthy"));

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

app.MapGet("/authentication/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
});

app.MapRazorComponents<Wallow.Web.Components.App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
