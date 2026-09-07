using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Wallow.TelemetryGateway.Tests;

public sealed class GatewayHealthTests
{
    [Fact]
    public async Task UnhandledIngressFailures_AreReportedAsFailuresInPrivateMetrics()
    {
        GatewayHealth health = new();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using WebApplication ingress = builder.Build();
        health.Observe(ingress);
        ingress.Run(_ => throw new InvalidOperationException("Dependency failed"));
        await ingress.StartAsync();
        await using WebApplication metrics = health.Create(Path.Combine(Path.GetTempPath(), "registry.db"));
        await metrics.StartAsync();
        using HttpClient client = new();
        using HttpResponseMessage failed = await client.GetAsync(ingress.Urls.Single());
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        string report = await client.GetStringAsync(metrics.Urls.Single() + "/metrics");
        Assert.Contains("wallow_gateway_requests_total{status=\"500\"} 1", report, StringComparison.Ordinal);
        Assert.DoesNotContain("status=\"200\"", report, StringComparison.Ordinal);
        Assert.Contains("wallow_gateway_active_requests 0", report, StringComparison.Ordinal);
    }
}
