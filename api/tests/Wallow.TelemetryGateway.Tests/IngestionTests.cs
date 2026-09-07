using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;

namespace Wallow.TelemetryGateway.Tests;

public sealed class IngestionTests
{
    [Fact]
    public async Task OnlyAcknowledgedCredentialsCanForwardAndDeletionRevokesThem()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "registry.db");
        WebApplicationBuilder collectorBuilder = WebApplication.CreateSlimBuilder();
        await using WebApplication collector = collectorBuilder.Build();
        collector.Urls.Add("http://127.0.0.1:0");
        collector.MapPost("/v1/logs", () => Results.NoContent());
        await collector.StartAsync();
        await using WebApplication control = await GatewayControl.CreateAsync(database, "management-secret", TimeProvider.System);
        await control.StartAsync();
        await using WebApplication ingress = GatewayIngress.Create(database, new Uri(collector.Urls.Single()), TimeProvider.System);
        await ingress.StartAsync();
        using HttpClient client = new() { BaseAddress = new Uri(ingress.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "credential-a.application-secret");
        using HttpResponseMessage unknown = await client.PostAsJsonAsync("/v1/logs", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        using HttpClient management = new() { BaseAddress = new Uri(control.Urls.Single()) };
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        DesiredRegistration desired = new(1, "client-a", "application-a", "browser-a", "server-a",
            ["test"], RegistrationState.Enabled,
            [new DesiredCredential("credential-a", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("application-secret"))))], null);
        string path = $"/control/v1/registrations/{Guid.NewGuid()}";
        using HttpResponseMessage provisioned = await management.PutAsJsonAsync(path, desired);
        Assert.Equal(HttpStatusCode.OK, provisioned.StatusCode);
        client.DefaultRequestHeaders.Add("X-Wallow-Environment", "test");
        using HttpResponseMessage accepted = await client.PostAsJsonAsync("/v1/logs", new { });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        using ByteArrayContent oversized = new(new byte[1024 * 1024 + 1]);
        oversized.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using HttpResponseMessage tooLarge = await client.PostAsync("/v1/logs", oversized);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        using HttpResponseMessage managementIngress = await client.PostAsJsonAsync("/v1/logs", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, managementIngress.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "credential-a.application-secret");
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "credential-a.application-secret");
        using HttpResponseMessage ingressControl = await management.PutAsJsonAsync(path, desired);
        Assert.Equal(HttpStatusCode.Unauthorized, ingressControl.StatusCode);
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");

        using HttpResponseMessage deleted = await management.PutAsJsonAsync(path, desired with { Revision = 2, State = RegistrationState.Deleted, Credentials = [] });
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        using HttpResponseMessage rejected = await client.PostAsJsonAsync("/v1/logs", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }
    [Fact]
    public async Task RotationDeadlineSurvivesRetryAndExpiresWithoutWallow()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "registry.db");
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        WebApplicationBuilder collectorBuilder = WebApplication.CreateSlimBuilder();
        await using WebApplication collector = collectorBuilder.Build();
        collector.Urls.Add("http://127.0.0.1:0");
        collector.MapPost("/v1/logs", () => Results.NoContent());
        await collector.StartAsync();
        await using WebApplication control = await GatewayControl.CreateAsync(database, "management-secret", clock);
        await control.StartAsync();
        await using WebApplication ingress = GatewayIngress.Create(database, new Uri(collector.Urls.Single()), clock);
        await ingress.StartAsync();
        using HttpClient management = new() { BaseAddress = new Uri(control.Urls.Single()) };
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        string verifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("application-secret")));
        DesiredRegistration desired = new(1, "client-a", "application-a", "browser-a", "server-a",
            ["test"], RegistrationState.Enabled, [new DesiredCredential("old", verifier)], null);
        string path = $"/control/v1/registrations/{Guid.NewGuid()}";
        using HttpResponseMessage first = await management.PutAsJsonAsync(path, desired);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        DesiredRegistration rotation = desired with
        {
            Revision = 2,
            Credentials = [new DesiredCredential("old", verifier), new DesiredCredential("new", verifier)],
            Rotation = new CredentialRotation(Guid.NewGuid(), "old", "new")
        };
        using HttpResponseMessage rotated = await management.PutAsJsonAsync(path, rotation);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        string acknowledgement = await rotated.Content.ReadAsStringAsync();
        clock.Advance(TimeSpan.FromHours(23));
        using HttpResponseMessage retry = await management.PutAsJsonAsync(path, rotation);
        Assert.Equal(acknowledgement, await retry.Content.ReadAsStringAsync());
        using HttpClient client = new() { BaseAddress = new Uri(ingress.Urls.Single()) };
        client.DefaultRequestHeaders.Add("X-Wallow-Environment", "test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "old.application-secret");
        using HttpResponseMessage overlap = await client.PostAsJsonAsync("/v1/logs", new { });
        Assert.Equal(HttpStatusCode.NoContent, overlap.StatusCode);
        await control.StopAsync();
        await using WebApplication restartedControl = await GatewayControl.CreateAsync(database, "management-secret", clock);
        await restartedControl.StartAsync();
        using HttpClient restartedManagement = new() { BaseAddress = new Uri(restartedControl.Urls.Single()) };
        restartedManagement.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        using HttpResponseMessage persistedRetry = await restartedManagement.PutAsJsonAsync(path, rotation);
        Assert.Equal(acknowledgement, await persistedRetry.Content.ReadAsStringAsync());
        using HttpResponseMessage nextRevision = await restartedManagement.PutAsJsonAsync(path, rotation with { Revision = 3 });
        Assert.Equal(HttpStatusCode.OK, nextRevision.StatusCode);
        RegistrationAcknowledgement? persisted = await nextRevision.Content.ReadFromJsonAsync<RegistrationAcknowledgement>();
        Assert.Equal(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero), persisted?.PreviousCredentialExpiresAt);
        await restartedControl.StopAsync();
        clock.Advance(TimeSpan.FromHours(1));
        using HttpResponseMessage expired = await client.PostAsJsonAsync("/v1/logs", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "new.application-secret");
        using HttpResponseMessage replacement = await client.PostAsJsonAsync("/v1/logs", new { });
        Assert.Equal(HttpStatusCode.NoContent, replacement.StatusCode);
    }

    [Theory]
    [InlineData("/v1/traces", "application/json", null, 204)]
    [InlineData("/v1/metrics", "application/x-protobuf", null, 204)]
    [InlineData("/v1/logs", "text/plain", null, 415)]
    [InlineData("/v1/logs", "application/json", "gzip", 415)]
    public async Task IngressEnforcesFixedProtocolContract(string route, string mediaType, string? encoding, int status)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "registry.db");
        WebApplicationBuilder collectorBuilder = WebApplication.CreateSlimBuilder();
        await using WebApplication collector = collectorBuilder.Build();
        collector.Urls.Add("http://127.0.0.1:0");
        collector.MapPost("/v1/{signal}", () => Results.NoContent());
        await collector.StartAsync();
        await using WebApplication control = await GatewayControl.CreateAsync(database, "management-secret", TimeProvider.System);
        await control.StartAsync();
        await using WebApplication ingress = GatewayIngress.Create(database, new Uri(collector.Urls.Single()), TimeProvider.System);
        await ingress.StartAsync();
        using HttpClient management = new() { BaseAddress = new Uri(control.Urls.Single()) };
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        DesiredRegistration desired = new(1, "client-a", "application-a", "browser-a", "server-a",
            ["test"], RegistrationState.Enabled,
            [new DesiredCredential("credential-a", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("application-secret"))))], null);
        using HttpResponseMessage provisioned = await management.PutAsJsonAsync($"/control/v1/registrations/{Guid.NewGuid()}", desired);
        Assert.Equal(HttpStatusCode.OK, provisioned.StatusCode);
        using HttpClient client = new() { BaseAddress = new Uri(ingress.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "credential-a.application-secret");
        client.DefaultRequestHeaders.Add("X-Wallow-Environment", "test");
        using ByteArrayContent body = new("{}"u8.ToArray());
        body.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        if (encoding is not null)
        {
            body.Headers.ContentEncoding.Add(encoding);
        }
        using HttpResponseMessage response = await client.PostAsync(route, body);
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Fact]
    public async Task RateLimitedRegistrationDoesNotConsumeAnotherRegistrationsAllowance()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "registry.db");
        WebApplicationBuilder collectorBuilder = WebApplication.CreateSlimBuilder();
        await using WebApplication collector = collectorBuilder.Build();
        collector.Urls.Add("http://127.0.0.1:0");
        collector.MapPost("/v1/logs", () => Results.NoContent());
        await collector.StartAsync();
        await using WebApplication control = await GatewayControl.CreateAsync(database, "management-secret", TimeProvider.System);
        await control.StartAsync();
        await using WebApplication ingress = GatewayIngress.Create(database, new Uri(collector.Urls.Single()), TimeProvider.System);
        await ingress.StartAsync();
        using HttpClient management = new() { BaseAddress = new Uri(control.Urls.Single()) };
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        string verifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("application-secret")));
        foreach (string credential in new[] { "noisy", "quiet" })
        {
            DesiredRegistration desired = new(1, credential, credential, "browser", "server", ["test"], RegistrationState.Enabled,
                [new DesiredCredential(credential, verifier)], null);
            using HttpResponseMessage provisioned = await management.PutAsJsonAsync($"/control/v1/registrations/{Guid.NewGuid()}", desired);
            Assert.Equal(HttpStatusCode.OK, provisioned.StatusCode);
        }
        using HttpClient client = new() { BaseAddress = new Uri(ingress.Urls.Single()) };
        client.DefaultRequestHeaders.Add("X-Wallow-Environment", "test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "noisy.application-secret");
        bool rejected = false;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/v1/logs", new { });
            rejected |= response.StatusCode == HttpStatusCode.TooManyRequests;
        }
        Assert.True(rejected);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "quiet.application-secret");
        using HttpResponseMessage quiet = await client.PostAsJsonAsync("/v1/logs", new { });
        Assert.Equal(HttpStatusCode.NoContent, quiet.StatusCode);
    }

}
