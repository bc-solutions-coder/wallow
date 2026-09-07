using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;

namespace Wallow.TelemetryGateway.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public async Task IdenticalRetryReturnsPersistedAcknowledgementAfterRestart()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "registry.db");
        Guid registrationId = Guid.NewGuid();
        DesiredRegistration desired = new(1, "client-a", "application-a", "browser-a", "server-a",
            ["test"], RegistrationState.Disabled, [], null);
        string first;
        await using (WebApplication app = await GatewayControl.CreateAsync(database, "management-secret", TimeProvider.System))
        {
            await app.StartAsync();
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
            using HttpResponseMessage response = await client.PutAsJsonAsync($"/control/v1/registrations/{registrationId}", desired);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            first = await response.Content.ReadAsStringAsync();
        }

        await using (WebApplication app = await GatewayControl.CreateAsync(database, "management-secret", TimeProvider.System))
        {
            await app.StartAsync();
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
            using HttpResponseMessage response = await client.PutAsJsonAsync($"/control/v1/registrations/{registrationId}", desired);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(first, await response.Content.ReadAsStringAsync());
        }
    }
    [Fact]
    public async Task NewRevisionReplacesStateButDeletionCannotBeResurrected()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await using WebApplication app = await GatewayControl.CreateAsync(Path.Combine(directory, "registry.db"), "management-secret", TimeProvider.System);
        await app.StartAsync();
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        string path = $"/control/v1/registrations/{Guid.NewGuid()}";
        DesiredRegistration desired = new(1, "client-a", "application-a", "browser-a", "server-a",
            ["test"], RegistrationState.Disabled, [], null);
        using HttpResponseMessage first = await client.PutAsJsonAsync(path, desired);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using HttpResponseMessage conflict = await client.PutAsJsonAsync(path, desired with { ApplicationId = "forged" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using HttpResponseMessage deletion = await client.PutAsJsonAsync(path, desired with { Revision = 2, State = RegistrationState.Deleted });
        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        using HttpResponseMessage stale = await client.PutAsJsonAsync(path, desired);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using HttpResponseMessage resurrect = await client.PutAsJsonAsync(path, desired with { Revision = 3 });
        Assert.Equal(HttpStatusCode.Conflict, resurrect.StatusCode);
        await app.StopAsync();
        await using WebApplication restarted = await GatewayControl.CreateAsync(Path.Combine(directory, "registry.db"), "management-secret", TimeProvider.System);
        await restarted.StartAsync();
        using HttpClient afterRestart = new() { BaseAddress = new Uri(restarted.Urls.Single()) };
        afterRestart.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        using HttpResponseMessage deletedRetry = await afterRestart.PutAsJsonAsync(path, desired with { Revision = 2, State = RegistrationState.Deleted });
        Assert.Equal(await deletion.Content.ReadAsStringAsync(), await deletedRetry.Content.ReadAsStringAsync());
        using HttpResponseMessage resurrectAfterRestart = await afterRestart.PutAsJsonAsync(path, desired with { Revision = 4 });
        Assert.Equal(HttpStatusCode.Conflict, resurrectAfterRestart.StatusCode);
    }

    [Fact]
    public async Task ConcurrentIdenticalUpdatesAllReceiveTheSameAcknowledgement()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await using WebApplication app = await GatewayControl.CreateAsync(Path.Combine(directory, "registry.db"), "management-secret", TimeProvider.System);
        await app.StartAsync();
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        string path = $"/control/v1/registrations/{Guid.NewGuid()}";
        DesiredRegistration desired = new(1, "client-a", "application-a", "browser-a", "server-a",
            ["test"], RegistrationState.Disabled, [], null);
        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PutAsJsonAsync(path, desired)));
        foreach (HttpResponseMessage response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        string[] acknowledgements = await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()));
        Assert.Single(acknowledgements.Distinct());
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidRevisionCannotBeAcknowledged(long revision)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await using WebApplication app = await GatewayControl.CreateAsync(Path.Combine(directory, "registry.db"), "management-secret", TimeProvider.System);
        await app.StartAsync();
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management-secret");
        DesiredRegistration desired = new(revision, "client-a", "application-a", "browser-a", "server-a",
            ["test"], RegistrationState.Disabled, [], null);
        using HttpResponseMessage response = await client.PutAsJsonAsync($"/control/v1/registrations/{Guid.NewGuid()}", desired);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

}
