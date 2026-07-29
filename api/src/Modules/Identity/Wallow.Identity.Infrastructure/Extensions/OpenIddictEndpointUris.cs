namespace Wallow.Identity.Infrastructure.Extensions;

/// <summary>
/// The OIDC endpoint URIs OpenIddict advertises in its discovery document and matches
/// incoming requests against.
/// </summary>
/// <remarks>
/// <para>
/// Every value here is <b>relative</b> — "connect/authorize", never "/connect/authorize" —
/// matching OpenIddict's own built-in defaults (".well-known/openid-configuration",
/// ".well-known/jwks").
/// </para>
/// <para>
/// OpenIddict resolves a relative endpoint URI against the request's base URI, which carries
/// the ASP.NET Core PathBase and a trailing slash. A leading slash instead makes the value an
/// absolute-path reference, which by RFC 3986 replaces the base URI's whole path and so
/// discards the PathBase. Under the path-based reverse-proxy topology (PathBase "/api") that
/// broke OIDC login outright: "/api/connect/authorize" was never recognised as the
/// authorization endpoint, so the passthrough controller received no OpenIddict request and
/// threw, and discovery advertised unprefixed endpoint URLs no proxy route served.
/// </para>
/// </remarks>
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

    /// <summary>Every configured endpoint URI, for tests that assert the shared invariant.</summary>
    public static IReadOnlyList<string> All { get; } = [Authorization, Token, EndSession, UserInfo];
}
