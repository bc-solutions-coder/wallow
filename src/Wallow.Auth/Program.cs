using System.Text.Json;
using BlazorBlueprint.Components;
using Polly;
using Wallow.Auth.Configuration;

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

if (branding.Theme.DefaultMode is not ("light" or "dark"))
{
    branding.Theme.DefaultMode = "dark";
}

builder.Services.AddSingleton(branding);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(30);
        options.MaxBufferedUnacknowledgedRenderBatches = 5;
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });

builder.Services.AddBlazorBlueprintComponents();

string apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl must be configured");

builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 5;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<Wallow.Auth.Services.IAuthApiClient, Wallow.Auth.Services.AuthApiClient>();
builder.Services.AddScoped<Wallow.Auth.Services.IClientBrandingClient, Wallow.Auth.Services.ClientBrandingApiClient>();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Wallow.Auth.Components.App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
