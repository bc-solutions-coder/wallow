using Wallow.Identity.Infrastructure.Extensions;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Checks that endpoint URI resolution preserves a path-prefixed base.
/// </summary>
public sealed class OpenIddictEndpointUriTests
{
    /// <summary>
    /// Path-prefixed request base used by the URI tests.
    /// </summary>
    private static readonly Uri _prefixedBase = new("https://auth.example.com/api/");

    /// <summary>
    /// Origin-root request base used by the URI tests.
    /// </summary>
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
        // A leading slash replaces the base path.
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
            OpenIddictEndpointUris.Revocation,
        ]);
    }
}
