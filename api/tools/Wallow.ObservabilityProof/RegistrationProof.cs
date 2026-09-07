extern alias wallowapi;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Wallow.Identity.IntegrationTests.OrganizationClients;
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

        using HttpResponseMessage registered = await caller.PostAsJsonAsync($"/v1/identity/organizations/{organization}/clients", new
        {
            kind = "application",
            name = "External browser proof",
            redirectUris = _redirects,
            postLogoutRedirectUris = Array.Empty<string>(),
            scopes = ApplicationScopes,
            enableObservability = true,
        });
        registered.EnsureSuccessStatusCode();
        JsonElement reveal = await registered.Content.ReadFromJsonAsync<JsonElement>();
        string clientId = reveal.GetProperty("client").GetProperty("clientId").GetString()!;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (true)
        {
            JsonElement current = await caller.GetFromJsonAsync<JsonElement>($"/v1/identity/organizations/{organization}/clients/{clientId}");
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
        JsonElement telemetry = reveal.GetProperty("telemetry");
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
        {
            application = clientId,
            endpoint = telemetry.GetProperty("endpoint").GetString(),
            credential = telemetry.GetProperty("credential").GetString(),
            environment = "test",
            release = "external-browser-1",
        }));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Console.WriteLine($"Registration checkbox workflow acknowledged for {clientId}; private configuration written.");
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
