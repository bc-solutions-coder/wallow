using System.Text.Json;
using Foundry.Identity.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Infrastructure.Services;

/// <summary>
/// Token service implementation that delegates to Keycloak's token endpoint.
/// </summary>
public sealed partial class KeycloakTokenService : ITokenService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _keycloakOptions;
    private readonly ILogger<KeycloakTokenService> _logger;

    public KeycloakTokenService(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakTokenService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("KeycloakTokenClient");
        _keycloakOptions = keycloakOptions.Value;
        _logger = logger;
    }

    public async Task<TokenResult> GetTokenAsync(string email, string password, CancellationToken ct = default)
    {
        LogRequestingToken(email);

        string tokenEndpoint = $"{_keycloakOptions.AuthorityUrl.TrimEnd('/')}/realms/{_keycloakOptions.Realm}/protocol/openid-connect/token";

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _keycloakOptions.AdminClientId,
            ["client_secret"] = _keycloakOptions.AdminClientSecret,
            ["username"] = email,
            ["password"] = password,
            ["scope"] = "openid profile email"
        });

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsync(tokenEndpoint, content, ct);
            string json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                LogTokenRequestFailed(email, response.StatusCode);
                return ParseErrorResponse(json);
            }

            LogTokenObtained(email);
            return ParseSuccessResponse(json);
        }
        catch (Exception ex)
        {
            LogGetTokenFailed(ex, email);
            return new TokenResult(
                Success: false,
                AccessToken: null,
                RefreshToken: null,
                TokenType: null,
                ExpiresIn: null,
                RefreshExpiresIn: null,
                Scope: null,
                Error: "server_error",
                ErrorDescription: "An error occurred while processing the token request");
        }
    }

    public async Task<TokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        LogRefreshingToken();

        string tokenEndpoint = $"{_keycloakOptions.AuthorityUrl.TrimEnd('/')}/realms/{_keycloakOptions.Realm}/protocol/openid-connect/token";

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _keycloakOptions.AdminClientId,
            ["client_secret"] = _keycloakOptions.AdminClientSecret,
            ["refresh_token"] = refreshToken
        });

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsync(tokenEndpoint, content, ct);
            string json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                LogTokenRefreshFailed(response.StatusCode);
                return ParseErrorResponse(json);
            }

            LogTokenRefreshed();
            return ParseSuccessResponse(json);
        }
        catch (Exception ex)
        {
            LogRefreshTokenFailed(ex);
            return new TokenResult(
                Success: false,
                AccessToken: null,
                RefreshToken: null,
                TokenType: null,
                ExpiresIn: null,
                RefreshExpiresIn: null,
                Scope: null,
                Error: "server_error",
                ErrorDescription: "An error occurred while refreshing the token");
        }
    }

    private static TokenResult ParseSuccessResponse(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        return new TokenResult(
            Success: true,
            AccessToken: root.GetProperty("access_token").GetString(),
            RefreshToken: root.TryGetProperty("refresh_token", out JsonElement rt) ? rt.GetString() : null,
            TokenType: root.TryGetProperty("token_type", out JsonElement tt) ? tt.GetString() : "Bearer",
            ExpiresIn: root.TryGetProperty("expires_in", out JsonElement ei) ? ei.GetInt32() : null,
            RefreshExpiresIn: root.TryGetProperty("refresh_expires_in", out JsonElement rei) ? rei.GetInt32() : null,
            Scope: root.TryGetProperty("scope", out JsonElement s) ? s.GetString() : null,
            Error: null,
            ErrorDescription: null);
    }

    private static TokenResult ParseErrorResponse(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            return new TokenResult(
                Success: false,
                AccessToken: null,
                RefreshToken: null,
                TokenType: null,
                ExpiresIn: null,
                RefreshExpiresIn: null,
                Scope: null,
                Error: root.TryGetProperty("error", out JsonElement e) ? e.GetString() : "unknown_error",
                ErrorDescription: root.TryGetProperty("error_description", out JsonElement ed) ? ed.GetString() : null);
        }
        catch
        {
            return new TokenResult(
                Success: false,
                AccessToken: null,
                RefreshToken: null,
                TokenType: null,
                ExpiresIn: null,
                RefreshExpiresIn: null,
                Scope: null,
                Error: "unknown_error",
                ErrorDescription: "Failed to parse error response");
        }
    }
}

public sealed partial class KeycloakTokenService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Requesting token for user {Email}")]
    private partial void LogRequestingToken(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token request failed for user {Email}: {StatusCode}")]
    private partial void LogTokenRequestFailed(string email, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token obtained successfully for user {Email}")]
    private partial void LogTokenObtained(string email);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to obtain token for user {Email}")]
    private partial void LogGetTokenFailed(Exception ex, string email);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Refreshing token")]
    private partial void LogRefreshingToken();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token refresh failed: {StatusCode}")]
    private partial void LogTokenRefreshFailed(System.Net.HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token refreshed successfully")]
    private partial void LogTokenRefreshed();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to refresh token")]
    private partial void LogRefreshTokenFailed(Exception ex);
}
