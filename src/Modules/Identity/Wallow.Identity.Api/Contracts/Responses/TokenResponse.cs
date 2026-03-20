using System.Text.Json.Serialization;

namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// OAuth2 token response.
/// </summary>
public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")]
    string AccessToken,

    [property: JsonPropertyName("refresh_token")]
    string? RefreshToken,

    [property: JsonPropertyName("token_type")]
    string TokenType,

    [property: JsonPropertyName("expires_in")]
    int ExpiresIn,

    [property: JsonPropertyName("refresh_expires_in")]
    int? RefreshExpiresIn,

    [property: JsonPropertyName("scope")]
    string? Scope);
