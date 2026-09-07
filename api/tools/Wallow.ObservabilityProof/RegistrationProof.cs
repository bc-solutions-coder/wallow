extern alias wallowapi;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.IntegrationTests.OrganizationClients;
using Wallow.ServiceDefaults;
using Wallow.Tests.Common.Factories;

namespace Wallow.ObservabilityProof;

internal static class RegistrationProof
{
    public static async Task Main()
    {
        await using ProofApiFactory factory = new();
        await factory.InitializeAsync();
        RegistrationFixture fixture = new(factory);
        await fixture.InitializeAsync();
        try { await fixture.RegisterAsync(); }
        finally { await fixture.DisposeAsync(); }
    }
}

internal sealed class RegistrationFixture(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    private static readonly string[] _mapSources = ["src/operation.ts"];
    private static readonly string[] _mapNames = ["crash"];
    private static readonly string[] _mapContent = ["PRIVATE-SOURCE-CONTENT"];
    private static readonly string[] _redirects = ["https://external.example/callback"];

    public async Task RegisterAsync()
    {
        string control = Environment.GetEnvironmentVariable("PROOF_CONTROL_URL") ?? throw new InvalidOperationException("Missing control endpoint");
        string output = Environment.GetEnvironmentVariable("PROOF_CONFIGURATION") ?? throw new InvalidOperationException("Missing private output path");
        string secretFile = Environment.GetEnvironmentVariable("PROOF_MANAGEMENT_FILE") ?? throw new InvalidOperationException("Missing management file");
        string[] lines = await File.ReadAllLinesAsync(secretFile);
        string secret = lines.Single(line => line.StartsWith("GATEWAY_MANAGEMENT_SECRET=", StringComparison.Ordinal)).Split('=', 2)[1];
        Guid organization = await OrganizationOwnedBySomeoneElseAsync($"External browser proof {Guid.NewGuid():N}");
        await ActAsEnrolledAsync(organization, "manager");
        await using WebApplicationFactory<wallowapi::Program> application = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:ControlEndpoint"] = control,
                ["Telemetry:ManagementSecret"] = secret,
                ["Telemetry:IngestionEndpoint"] = "http://gateway:8080",
            })));
        using HttpClient caller = application.CreateClient();
        foreach (KeyValuePair<string, IEnumerable<string>> header in Client.DefaultRequestHeaders)
        {
            caller.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        async Task<(string ClientId, JsonElement Telemetry)> RegisterOneAsync(string name)
        {
            using HttpResponseMessage registered = await caller.PostAsJsonAsync($"/v1/identity/organizations/{organization}/clients", new
            {
                kind = "application",
                name = name,
                redirectUris = _redirects,
                postLogoutRedirectUris = Array.Empty<string>(),
                scopes = ApplicationScopes,
                enableObservability = true,
            });
            registered.EnsureSuccessStatusCode();
            JsonElement reveal = await registered.Content.ReadFromJsonAsync<JsonElement>();
            string registeredClientId = reveal.GetProperty("client").GetProperty("clientId").GetString()!;
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (true)
            {
                JsonElement current = await caller.GetFromJsonAsync<JsonElement>($"/v1/identity/organizations/{organization}/clients/{registeredClientId}");
                if (current.GetProperty("telemetry").GetProperty("status").GetString() == "active")
                {
                    break;
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new InvalidOperationException($"Registration did not become active: {current.GetProperty("telemetry")}");
                }

                await Task.Delay(200);
            }
            return (registeredClientId, reveal.GetProperty("telemetry").Clone());
        }
        (string clientId, JsonElement telemetry) = await RegisterOneAsync("External browser proof");
        (string apiClientId, JsonElement apiTelemetry) = await RegisterOneAsync("External API proof");
        await using AsyncServiceScope mapScope = application.Services.CreateAsyncScope();
        IdentityDbContext mapDb = mapScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        TelemetryRegistration mapRegistration = await mapDb.TelemetryRegistrations.AsNoTracking().SingleAsync(item => item.ClientId == clientId);
        using HttpClient maps = new() { BaseAddress = new Uri(control) };
        maps.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secret);
        string mapPath = $"/control/v1/source-maps/{mapRegistration.Id.Value}/external-browser-1/proof.js";
        using HttpResponseMessage upload = await maps.PutAsJsonAsync(mapPath, new
        {
            version = 3,
            sources = _mapSources,
            names = _mapNames,
            mappings = "AAAAA",
            sourcesContent = _mapContent,
        });
        upload.EnsureSuccessStatusCode();
        JsonElement sourceContext = await maps.GetFromJsonAsync<JsonElement>(mapPath + "?line=1&column=30");
        int apiPort = int.Parse(Environment.GetEnvironmentVariable("PROOF_API_PORT")!, System.Globalization.CultureInfo.InvariantCulture);
        Environment.SetEnvironmentVariable("Telemetry__Export__Endpoint", control);
        Environment.SetEnvironmentVariable("Telemetry__Export__Credential", apiTelemetry.GetProperty("credential").GetString());
        Environment.SetEnvironmentVariable("Telemetry__Export__Environment", "test");
        Environment.SetEnvironmentVariable("Telemetry__Export__Release", "external-browser-1");
        await using WebApplicationFactory<wallowapi::Program> observed = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(FailureController).Assembly)));
        observed.UseKestrel(options => options.ListenLocalhost(apiPort));
        using HttpClient observedClient = observed.CreateClient();
        using HttpResponseMessage handledFailure = await observedClient.GetAsync("/proof/failure/validation");
        if ((int)handledFailure.StatusCode != 400) { throw new InvalidOperationException("Handled failure route did not return 400"); }
        using HttpResponseMessage localFailure = await observedClient.GetAsync("/proof/failure");
        Console.WriteLine($"Local API proof status {(int)localFailure.StatusCode}; endpoint port {apiPort}");
        using ActivitySource source = new("Wallow.Proof");
        string serverTrace;
        using (Activity activity = source.StartActivity("background.failure") ?? throw new InvalidOperationException("Missing API tracing"))
        {
            serverTrace = activity.TraceId.ToHexString();
            activity.SetStatus(ActivityStatusCode.Error);
            ProofLog.BackgroundFailure(observed.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Wallow.Proof"), "fixture");
        }
        await observed.Services.GetRequiredService<IndependentTelemetry>().FlushAsync(CancellationToken.None);
        Console.WriteLine($"Independent export: {observed.Services.GetRequiredService<IndependentTelemetry>().Stats()}");
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
        {
            application = clientId,
            endpoint = telemetry.GetProperty("endpoint").GetString(),
            credential = telemetry.GetProperty("credential").GetString(),
            environment = "test",
            release = "external-browser-1",
            apiApplication = apiClientId,
            apiEndpoint = $"http://host.docker.internal:{apiPort}",
            serverTrace,
            sourceContext,
        }));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Console.WriteLine($"Registration checkbox workflow acknowledged for {clientId} and {apiClientId}; private configuration written.");
        string stop = Environment.GetEnvironmentVariable("PROOF_STOP_FILE")!;
        DateTimeOffset stopDeadline = DateTimeOffset.UtcNow.AddMinutes(10);
        while (!File.Exists(stop) && DateTimeOffset.UtcNow < stopDeadline) { await Task.Delay(200); }
        await observed.Services.GetRequiredService<IndependentTelemetry>().FlushAsync(CancellationToken.None);
    }
}

internal sealed class ProofApiFactory : WallowApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseContentRoot(Path.GetFullPath("api/src/Wallow.Api"));
    }
}

[ApiController]
[AllowAnonymous]
[Route("v1/proof/failure")]
public sealed class FailureController : ControllerBase
{
    [HttpGet("validation")]
    public IActionResult Validate(string? input) => throw new ArgumentException("PRIVATE-VALIDATION-FAILURE", nameof(input));

    [HttpGet]
    public IActionResult Fail()
    {
        if (Request.Headers.ContainsKey("baggage") || Request.Headers.ContainsKey("Authorization"))
        {
            return StatusCode(502);
        }
        throw new InvalidOperationException("PRIVATE-API-FAILURE");
    }
}

internal static partial class ProofLog
{
    public static void BackgroundFailure(ILogger logger, string input) => Failure(logger, new ArgumentException($"PRIVATE-SERVER-FAILURE: {input}", nameof(input)));

    [LoggerMessage(Level = LogLevel.Error, Message = "Background proof failed")]
    public static partial void Failure(ILogger logger, Exception error);
}
