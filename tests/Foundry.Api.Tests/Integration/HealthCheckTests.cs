using System.Net;
using System.Net.Http.Json;
using Foundry.Tests.Common.Factories;

namespace Foundry.Api.Tests.Integration;

[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class HealthCheckTests(FoundryApiFactory factory) : IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Status()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        HealthResponse? healthReport = await response.Content.ReadFromJsonAsync<HealthResponse>();
        healthReport.Should().NotBeNull();
        healthReport.Status.Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
    }

    [Fact]
    public async Task HealthReady_Endpoint_Returns_Status()
    {
        HttpResponseMessage response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task HealthLive_Endpoint_Returns_Status()
    {
        HttpResponseMessage response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Root_Endpoint_Returns_Api_Info()
    {
        HttpResponseMessage response = await _client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        ApiInfo? info = await response.Content.ReadFromJsonAsync<ApiInfo>();
        info.Should().NotBeNull();
        info.Name.Should().Be("Foundry API");
        info.Version.Should().NotBeNullOrEmpty();
        info.Health.Should().NotBeNullOrEmpty();
    }

    private sealed record HealthResponse(string Status, double Duration);
    private sealed record ApiInfo(string Name, string Version, string Health);
}
