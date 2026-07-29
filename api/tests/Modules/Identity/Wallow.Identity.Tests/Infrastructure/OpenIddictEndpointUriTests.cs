using Wallow.Identity.Infrastructure.Extensions;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Pins the resolution semantics of the OIDC endpoint URIs handed to OpenIddict.
/// </summary>
/// <remarks>
/// These were once root-relative ("/connect/authorize"), which silently broke the whole
/// path-based reverse-proxy topology: OpenIddict resolves a relative endpoint URI against the
/// request's base URI (scheme + host + PathBase, with a trailing slash), and an absolute-path
/// reference replaces that base's path outright. Endpoint matching then never recognised
/// "/api/connect/authorize", so AuthorizationController threw "The OpenID Connect request
/// cannot be retrieved", and discovery advertised "/connect/*" URLs the proxy did not route.
/// The tests below assert the URI arithmetic itself, so restoring a leading slash fails here
/// rather than only in a live Caddy smoke test.
/// </remarks>
public sealed class OpenIddictEndpointUriTests
{
    /// <summary>The base URI OpenIddict builds for a request served under PathBase "/api".</summary>
    private static readonly Uri _prefixedBase = new("https://auth.example.com/api/");

    /// <summary>The base URI OpenIddict builds when the API is served at the origin root.</summary>
    private static readonly Uri _rootBase = new("https://auth.example.com/");

    public static TheoryData<string> AllEndpointUris()
    {
        TheoryData<string> data = new();
        foreach (string endpointUri in OpenIddictEndpointUris.All)
        {
            data.Add(endpointUri);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllEndpointUris))]
    public void EndpointUri_IsRelative_NotRootRelative(string endpointUri)
    {
        endpointUri.Should().NotStartWith("/",
            "a leading slash is an absolute-path reference that discards the request's PathBase");
        Uri.TryCreate(endpointUri, UriKind.Relative, out _).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(AllEndpointUris))]
    public void EndpointUri_ResolvedUnderAPathBase_KeepsThePrefix(string endpointUri)
    {
        Uri resolved = new(_prefixedBase, endpointUri);

        resolved.AbsoluteUri.Should().Be($"https://auth.example.com/api/{endpointUri}",
            "the reverse proxy does not strip the /api prefix, so both discovery and endpoint "
            + "matching must resolve to the prefixed URL");
    }

    [Theory]
    [MemberData(nameof(AllEndpointUris))]
    public void EndpointUri_ResolvedAtTheOriginRoot_IsUnprefixed(string endpointUri)
    {
        Uri resolved = new(_rootBase, endpointUri);

        resolved.AbsoluteUri.Should().Be($"https://auth.example.com/{endpointUri}",
            "the default topology serves the API at the root and must be unaffected");
    }

    [Fact]
    public void RootRelativeEndpointUri_LosesThePathBase()
    {
        // The regression itself, stated as arithmetic: this is what "/connect/authorize" did.
        Uri resolved = new(_prefixedBase, "/" + OpenIddictEndpointUris.Authorization);

        resolved.AbsoluteUri.Should().Be("https://auth.example.com/connect/authorize");
    }

    [Fact]
    public void All_CoversEveryConfiguredEndpoint()
    {
        OpenIddictEndpointUris.All.Should().BeEquivalentTo([
            OpenIddictEndpointUris.Authorization,
            OpenIddictEndpointUris.Token,
            OpenIddictEndpointUris.EndSession,
            OpenIddictEndpointUris.UserInfo,
        ]);
    }
}
