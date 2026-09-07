namespace Wallow.Identity.Infrastructure.Extensions;

/// <summary>
/// OIDC endpoint paths relative to the request base URI. A leading slash would discard
/// PathBase when resolving discovery URLs.
/// </summary>
public static class OpenIddictEndpointUris
{
    /// <summary>The authorization endpoint URI.</summary>
    public const string Authorization = "connect/authorize";

    /// <summary>The token endpoint URI.</summary>
    public const string Token = "connect/token";

    /// <summary>The end-session (logout) endpoint URI.</summary>
    public const string EndSession = "connect/logout";

    /// <summary>The userinfo endpoint URI.</summary>
    public const string UserInfo = "connect/userinfo";

    /// <summary>The token revocation (RFC 7009) endpoint URI.</summary>
    public const string Revocation = "connect/revocation";

    /// <summary>
    /// All configured OIDC endpoint paths.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Authorization, Token, EndSession, UserInfo, Revocation];
}
