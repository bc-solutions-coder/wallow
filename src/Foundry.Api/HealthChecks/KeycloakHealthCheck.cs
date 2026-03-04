using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Foundry.Api.HealthChecks;

internal sealed class KeycloakHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string baseUrl = (configuration["Keycloak:auth-server-url"] ?? "").TrimEnd('/');
        string realm = configuration["Keycloak:realm"] ?? "foundry";

        if (string.IsNullOrEmpty(baseUrl))
        {
            return HealthCheckResult.Unhealthy("Keycloak base URL is not configured.");
        }

        try
        {
            HttpClient client = httpClientFactory.CreateClient("HealthChecks");
            string url = $"{baseUrl}/realms/{realm}/.well-known/openid-configuration";
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Keycloak is reachable.")
                : HealthCheckResult.Unhealthy($"Keycloak returned {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Keycloak is unreachable.", ex);
        }
    }
}
