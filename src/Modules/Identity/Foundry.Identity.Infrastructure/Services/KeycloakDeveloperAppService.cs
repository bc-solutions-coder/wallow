using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class KeycloakDeveloperAppService(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<KeycloakDeveloperAppService> logger) : IDeveloperAppService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("KeycloakDcrClient");
    private readonly KeycloakOptions _keycloakOptions = keycloakOptions.Value;

    public async Task<DeveloperAppRegistrationResult> RegisterClientAsync(
        string clientId,
        string clientName,
        CancellationToken cancellationToken = default)
    {
        LogRegisteringClient(clientId);

        string registrationUrl =
            $"{_keycloakOptions.AuthorityUrl.TrimEnd('/')}/realms/{_keycloakOptions.Realm}/clients-registrations/openid-connect";

        var payload = new
        {
            client_id = clientId,
            client_name = clientName,
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_basic"
        };

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(registrationUrl, payload, cancellationToken);
        await response.EnsureSuccessOrThrowAsync();

        DcrResponse? dcr = await response.Content.ReadFromJsonAsync<DcrResponse>(cancellationToken);
        if (dcr is null || dcr.ClientId is null || dcr.ClientSecret is null || dcr.RegistrationAccessToken is null)
        {
            throw new InvalidOperationException($"Keycloak DCR returned an incomplete response for client '{clientId}'");
        }

        LogClientRegistered(dcr.ClientId);

        return new DeveloperAppRegistrationResult(
            dcr.ClientId,
            dcr.ClientSecret,
            dcr.RegistrationAccessToken);
    }

    private sealed record DcrResponse(
        string? ClientId,
        string? ClientSecret,
        string? RegistrationAccessToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registering developer app client {ClientId} via Keycloak DCR")]
    private partial void LogRegisteringClient(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Developer app client {ClientId} registered successfully")]
    private partial void LogClientRegistered(string clientId);
}
