using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Wallow.Web;

internal sealed class ApiHealthCheck(IHttpClientFactory httpClientFactory, string httpClientName) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpClient client = httpClientFactory.CreateClient(httpClientName);
            using HttpResponseMessage response = await client.GetAsync("/health/ready", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("API is reachable")
                : HealthCheckResult.Unhealthy($"API returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("API is unreachable", ex);
        }
    }
}
