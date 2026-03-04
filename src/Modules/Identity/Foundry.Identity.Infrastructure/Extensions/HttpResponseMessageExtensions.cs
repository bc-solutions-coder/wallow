using Foundry.Shared.Kernel.Domain;

namespace Foundry.Identity.Infrastructure.Extensions;

internal static class HttpResponseMessageExtensions
{
    internal static async Task EnsureSuccessOrThrowAsync(this HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string responseBody = await response.Content.ReadAsStringAsync(ct);
        throw new ExternalServiceException(
            $"Keycloak request failed with status {(int)response.StatusCode}: {response.ReasonPhrase}",
            (int)response.StatusCode,
            responseBody);
    }
}
