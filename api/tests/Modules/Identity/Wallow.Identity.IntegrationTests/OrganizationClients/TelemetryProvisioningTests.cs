extern alias telemetry;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using telemetry::Wallow.TelemetryGateway;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OrganizationClients;

[Trait("Category", "Integration")]
public sealed class TelemetryProvisioningTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    private static readonly string[] _redirects = ["https://observed.example/callback"];

    [Fact]
    public async Task RegistrationDuringGatewayOutage_RecoversAutomatically_AndAcceptsTheRevealedCredential()
    {
        FakeTimeProvider clock = new(DateTimeOffset.UtcNow);
        string directory = Directory.CreateTempSubdirectory("wallow-telemetry-registration-").FullName;
        string database = Path.Combine(directory, "registry.db");
        const string managementSecret = "local-integration-management";
        await using WebApplication stopped = await GatewayControl.CreateAsync(database, managementSecret, clock);
        await stopped.StartAsync();
        string controlUrl = stopped.Urls.Single();
        await stopped.StopAsync();
        Guid organizationId = await OrganizationOwnedBySomeoneElseAsync("Gateway recovery");
        await ActAsEnrolledAsync(organizationId, "manager");
        using WebApplicationFactory<Program> application = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:ControlEndpoint"] = controlUrl,
                ["Telemetry:ManagementSecret"] = managementSecret,
                ["Telemetry:IngestionEndpoint"] = "https://telemetry.example",
            })));
        using HttpClient caller = application.CreateClient();
        foreach (KeyValuePair<string, IEnumerable<string>> header in Client.DefaultRequestHeaders)
        {
            caller.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        using HttpResponseMessage registered = await caller.PostAsJsonAsync($"/v1/identity/organizations/{organizationId}/clients", new
        {
            kind = "application",
            name = "Observed",
            redirectUris = _redirects,
            postLogoutRedirectUris = Array.Empty<string>(),
            scopes = ApplicationScopes,
            enableObservability = true,
        });
        registered.StatusCode.Should().Be(HttpStatusCode.Created, await registered.Content.ReadAsStringAsync());
        JsonElement reveal = await registered.Content.ReadFromJsonAsync<JsonElement>();
        string clientId = reveal.GetProperty("client").GetProperty("clientId").GetString()!;
        string credential = reveal.GetProperty("telemetry").GetProperty("credential").GetString()!;
        reveal.GetProperty("client").GetProperty("telemetry").GetProperty("status").GetString().Should().Be("pending");

        await using WebApplication recovered = await GatewayControl.CreateAsync(database, managementSecret, clock);
        recovered.Urls.Clear();
        recovered.Urls.Add(controlUrl);
        await recovered.StartAsync();
        string clientPath = $"/v1/identity/organizations/{organizationId}/clients/{clientId}";
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(35);
        JsonElement current;
        do
        {
            await Task.Delay(200);
            current = await caller.GetFromJsonAsync<JsonElement>(clientPath);
        }
        while (current.GetProperty("telemetry").GetProperty("status").GetString() != "active" && DateTimeOffset.UtcNow < deadline);
        current.GetProperty("telemetry").GetProperty("status").GetString().Should().Be("active");
        current.ToString().Should().NotContain(credential).And.NotContain("verifier");

        await using WebApplication ingress = GatewayIngress.Create(database, new Uri("http://127.0.0.1:1"), clock);
        await ingress.StartAsync();
        using HttpClient producer = new() { BaseAddress = new Uri(ingress.Urls.Single()) };
        producer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        producer.DefaultRequestHeaders.Add("X-Wallow-Environment", "production");
        using HttpResponseMessage accepted = await producer.PostAsJsonAsync("/v1/logs", new { });
        accepted.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "authentication succeeds and only the deliberately absent collector fails");

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        TelemetryProvisioner provisioner = scope.ServiceProvider.GetRequiredService<TelemetryProvisioner>();
        IdentityDbContext registry = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        TelemetryRegistration persisted = await registry.TelemetryRegistrations.SingleAsync(e => e.ClientId == clientId);
        persisted.Verifier.Should().MatchRegex("^[a-f0-9]{64}$");
        JsonSerializer.Serialize(persisted).Should().NotContain(credential.Split('.')[1]);
        await provisioner.ReconcileAsync(persisted.Id.Value, CancellationToken.None);
        await provisioner.ReconcileAsync(persisted.Id.Value, CancellationToken.None);
        JsonElement replayed = await caller.GetFromJsonAsync<JsonElement>(clientPath);
        replayed.GetProperty("telemetry").GetProperty("status").GetString().Should().Be("active");
        using HttpResponseMessage duplicateEnable = await caller.PostAsync($"{clientPath}/observability", null);
        duplicateEnable.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await recovered.StopAsync();
        using HttpResponseMessage rotated = await caller.PostAsync($"{clientPath}/observability/rotate", null);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK, await rotated.Content.ReadAsStringAsync());
        JsonElement replacement = await rotated.Content.ReadFromJsonAsync<JsonElement>();
        string newCredential = replacement.GetProperty("configuration").GetProperty("credential").GetString()!;
        replacement.GetProperty("status").GetProperty("status").GetString().Should().Be("pending-rotation");
        await CredentialStatusAsync(ingress.Urls.Single(), credential, HttpStatusCode.ServiceUnavailable);
        await CredentialStatusAsync(ingress.Urls.Single(), newCredential, HttpStatusCode.Unauthorized);

        await using WebApplication rotationControl = await GatewayControl.CreateAsync(database, managementSecret, clock);
        rotationControl.Urls.Clear();
        rotationControl.Urls.Add(controlUrl);
        await rotationControl.StartAsync();
        JsonElement activated = await WaitForTelemetryAsync(caller, clientPath, "active");
        DateTimeOffset expiry = activated.GetProperty("telemetry").GetProperty("previousCredentialExpiresAt").GetDateTimeOffset();
        using HttpResponseMessage overlappingRotation = await caller.PostAsync($"{clientPath}/observability/rotate", null);
        overlappingRotation.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        registry.ChangeTracker.Clear();
        await provisioner.ReconcileAsync(persisted.Id.Value, CancellationToken.None);
        await provisioner.ReconcileAsync(persisted.Id.Value, CancellationToken.None);
        JsonElement unchanged = await caller.GetFromJsonAsync<JsonElement>(clientPath);
        unchanged.GetProperty("telemetry").GetProperty("previousCredentialExpiresAt").GetDateTimeOffset().Should().Be(expiry);

        await ingress.StopAsync();
        await using WebApplication restartedIngress = GatewayIngress.Create(database, new Uri("http://127.0.0.1:1"), clock);
        await restartedIngress.StartAsync();
        string ingestionUrl = restartedIngress.Urls.Single();
        clock.SetUtcNow(expiry.AddMilliseconds(-1));
        await CredentialStatusAsync(ingestionUrl, credential, HttpStatusCode.ServiceUnavailable);
        await CredentialStatusAsync(ingestionUrl, newCredential, HttpStatusCode.ServiceUnavailable);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await CredentialStatusAsync(ingestionUrl, credential, HttpStatusCode.Unauthorized);
        await CredentialStatusAsync(ingestionUrl, newCredential, HttpStatusCode.ServiceUnavailable);
        using HttpResponseMessage suspended = await caller.PostAsync($"{clientPath}/suspend", null);
        suspended.EnsureSuccessStatusCode();
        await CredentialStatusAsync(ingestionUrl, newCredential, HttpStatusCode.ServiceUnavailable);

        await rotationControl.StopAsync();
        using HttpResponseMessage revocation = await caller.PostAsync($"{clientPath}/observability/revoke", null);
        revocation.EnsureSuccessStatusCode();
        JsonElement pendingRevocation = await revocation.Content.ReadFromJsonAsync<JsonElement>();
        pendingRevocation.GetProperty("status").GetString().Should().Be("pending-revocation");
        await CredentialStatusAsync(ingestionUrl, newCredential, HttpStatusCode.ServiceUnavailable);
        await using WebApplication revocationControl = await GatewayControl.CreateAsync(database, managementSecret, clock);
        revocationControl.Urls.Clear();
        revocationControl.Urls.Add(controlUrl);
        await revocationControl.StartAsync();
        await WaitForTelemetryAsync(caller, clientPath, "disabled");
        await CredentialStatusAsync(ingestionUrl, newCredential, HttpStatusCode.Unauthorized);

        using HttpResponseMessage reenabled = await caller.PostAsync($"{clientPath}/observability", null);
        reenabled.EnsureSuccessStatusCode();
        JsonElement renewed = await reenabled.Content.ReadFromJsonAsync<JsonElement>();
        string renewedCredential = renewed.GetProperty("configuration").GetProperty("credential").GetString()!;
        await WaitForTelemetryAsync(caller, clientPath, "active");
        await CredentialStatusAsync(ingestionUrl, renewedCredential, HttpStatusCode.ServiceUnavailable);
        using HttpResponseMessage disabled = await caller.PostAsync($"{clientPath}/observability/disable", null);
        disabled.EnsureSuccessStatusCode();
        await WaitForTelemetryAsync(caller, clientPath, "disabled");
        await CredentialStatusAsync(ingestionUrl, renewedCredential, HttpStatusCode.Unauthorized);
        using HttpResponseMessage deletedClient = await caller.DeleteAsync(clientPath);
        deletedClient.EnsureSuccessStatusCode();
        DateTimeOffset deletionDeadline = DateTimeOffset.UtcNow.AddSeconds(35);
        TelemetryRegistration tombstone;
        do
        {
            await Task.Delay(200);
            registry.ChangeTracker.Clear();
            tombstone = await registry.TelemetryRegistrations.SingleAsync(e => e.ClientId == clientId);
        } while (tombstone.AcknowledgedRevision != tombstone.Revision && DateTimeOffset.UtcNow < deletionDeadline);
        tombstone.AccessState.Should().Be(TelemetryAccessState.Deleted);
        tombstone.AcknowledgedRevision.Should().Be(tombstone.Revision);
        using HttpClient controlCaller = new() { BaseAddress = new Uri(controlUrl) };
        controlCaller.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managementSecret);
        using HttpResponseMessage resurrection = await controlCaller.PutAsJsonAsync($"/control/v1/registrations/{tombstone.Id.Value}",
            new DesiredRegistration(tombstone.Revision + 1, clientId, clientId, $"{clientId}-browser", $"{clientId}-server", ["production"], RegistrationState.Disabled, [], null));
        resurrection.StatusCode.Should().Be(HttpStatusCode.Conflict);


    }
    private static async Task CredentialStatusAsync(string endpoint, string credential, HttpStatusCode expected)
    {
        using HttpClient producer = new() { BaseAddress = new Uri(endpoint) };
        producer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        producer.DefaultRequestHeaders.Add("X-Wallow-Environment", "production");
        using HttpResponseMessage result = await producer.PostAsJsonAsync("/v1/logs", new { });
        result.StatusCode.Should().Be(expected);
    }

    private static async Task<JsonElement> WaitForTelemetryAsync(HttpClient caller, string path, string expected)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(35);
        JsonElement current;
        do
        {
            await Task.Delay(200);
            current = await caller.GetFromJsonAsync<JsonElement>(path);
        }
        while (current.GetProperty("telemetry").GetProperty("status").GetString() != expected && DateTimeOffset.UtcNow < deadline);
        current.GetProperty("telemetry").GetProperty("status").GetString().Should().Be(expected);
        return current;
    }

}
