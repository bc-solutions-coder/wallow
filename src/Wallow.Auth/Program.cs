using System.Text.Json;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using Wallow.Auth.Configuration;
using Wallow.ServiceDefaults;

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

if (branding.Theme.DefaultMode is not ("light" or "dark"))
{
    branding.Theme.DefaultMode = "dark";
}

builder.Services.AddSingleton(branding);

// DataProtection — persist keys to Valkey so antiforgery tokens survive restarts.
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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(30);
        options.MaxBufferedUnacknowledgedRenderBatches = 5;
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });

builder.Services.AddBlazorBlueprintComponents();

string apiPublicUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl must be configured (public URL for browser redirects)");

// Server-to-server URL: use service discovery when available, fall back to configured URL
string apiInternalUrl = builder.Configuration["ServiceUrls:ApiUrl"] ?? "https+http://wallow-api";

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<Wallow.Auth.Services.CookieRelayStore>();
builder.Services.AddScoped<Wallow.Auth.Services.ApiCookieJar>();
builder.Services.AddTransient<Wallow.Auth.Services.CookieForwardingHandler>();

builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(apiInternalUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    // Disable built-in cookie handling — CookieForwardingHandler manages cookies manually
    // to relay partial auth cookies (MFA flow) between API calls within the same Blazor circuit
    UseCookies = false
})
.AddHttpMessageHandler<Wallow.Auth.Services.CookieForwardingHandler>();

builder.Services.AddScoped<Wallow.Auth.Services.IAuthApiClient, Wallow.Auth.Services.AuthApiClient>();
builder.Services.AddScoped<Wallow.Auth.Services.IClientBrandingClient, Wallow.Auth.Services.ClientBrandingApiClient>();

builder.Services.AddHealthChecks()
    .Add(new HealthCheckRegistration(
        "api",
        sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new Wallow.Shared.Api.ApiHealthCheck(httpClientFactory, "AuthApi");
        },
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]));

WebApplication app = builder.Build();

// Opt-in PathBase for reverse-proxy path-based routing (e.g. /auth)
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
app.UseStaticFiles();
app.UseMiddleware<Wallow.Auth.Services.CookieRelayMiddleware>();
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

app.MapRazorComponents<Wallow.Auth.Components.App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
