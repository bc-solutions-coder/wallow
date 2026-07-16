using Microsoft.Extensions.Configuration;
using Wallow.Web.Configuration;

namespace Wallow.Web.Tests.Configuration;

/// <summary>
/// Spec (Wallow-vec7.4.2): the Blazor Web app's OIDC client must point at the unified auth
/// origin that proxies the OIDC endpoints, not at the API's own origin. The authority has to
/// match the issuer OpenIddictIssuerResolver advertises (Wallow-vec7.4.1) or ID token
/// validation fails on the iss claim.
/// </summary>
public class WebOidcAuthorityResolverTests
{
    private const string AuthOrigin = "https://auth.example.test";
    private const string ApiOrigin = "https://api.example.test";

    [Fact]
    public void Resolve_ExplicitAuthority_ReturnsIt()
    {
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            [WebOidcAuthorityResolver.AuthorityKey] = AuthOrigin,
        });

        WebOidcAuthorityResolver.Resolve(configuration).Should().Be(AuthOrigin);
    }

    [Fact]
    public void Resolve_ExplicitAuthority_TakesPrecedenceOverBothAuthUrlKeys()
    {
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            [WebOidcAuthorityResolver.AuthorityKey] = AuthOrigin,
            [WebOidcAuthorityResolver.AuthPublicUrlKey] = "https://public.example.test",
            [WebOidcAuthorityResolver.AuthUrlKey] = ApiOrigin,
        });

        WebOidcAuthorityResolver.Resolve(configuration).Should().Be(AuthOrigin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NoExplicitAuthority_FallsBackToAuthPublicUrl(string? authority)
    {
        // Blank is the checked-in appsettings.json default, so a null-only check would defeat
        // the fallback entirely.
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            [WebOidcAuthorityResolver.AuthorityKey] = authority,
            [WebOidcAuthorityResolver.AuthPublicUrlKey] = AuthOrigin,
        });

        WebOidcAuthorityResolver.Resolve(configuration).Should().Be(AuthOrigin);
    }

    [Fact]
    public void Resolve_AuthPublicUrl_TakesPrecedenceOverAuthUrl()
    {
        // The repoint: ServiceUrls:AuthUrl is the API's own origin in Development and a
        // container-internal hostname in the compose stacks, so it must never win over the
        // browser-facing public origin.
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            [WebOidcAuthorityResolver.AuthPublicUrlKey] = AuthOrigin,
            [WebOidcAuthorityResolver.AuthUrlKey] = ApiOrigin,
        });

        WebOidcAuthorityResolver.Resolve(configuration).Should().Be(AuthOrigin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_NoAuthPublicUrl_FallsBackToAuthUrl(string? authPublicUrl)
    {
        // Preserves today's behaviour for forks that never set the public origin.
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            [WebOidcAuthorityResolver.AuthPublicUrlKey] = authPublicUrl,
            [WebOidcAuthorityResolver.AuthUrlKey] = AuthOrigin,
        });

        WebOidcAuthorityResolver.Resolve(configuration).Should().Be(AuthOrigin);
    }

    [Fact]
    public void Resolve_PathBaseOrigin_IsPreserved()
    {
        // Production serves the auth app under a path prefix (AUTH_PUBLIC_URL defaults to
        // https://wallow.dev/auth); reducing it to scheme+host breaks every redirect.
        const string pathBaseOrigin = "https://wallow.dev/auth";
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            [WebOidcAuthorityResolver.AuthPublicUrlKey] = pathBaseOrigin,
        });

        WebOidcAuthorityResolver.Resolve(configuration).Should().Be(pathBaseOrigin);
    }

    [Fact]
    public void Resolve_NothingConfigured_ThrowsNamingTheKeys()
    {
        // Replaces the silent "http://localhost:5001" fallback, which pointed a production
        // deployment at a dev-only API origin rather than failing loudly.
        IConfiguration configuration = Build([]);

        Action act = () => WebOidcAuthorityResolver.Resolve(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{WebOidcAuthorityResolver.AuthPublicUrlKey}*");
    }

    [Fact]
    public void Resolve_RelativeAuthority_ThrowsWithConfigurationKey()
    {
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            [WebOidcAuthorityResolver.AuthorityKey] = "/connect",
        });

        Action act = () => WebOidcAuthorityResolver.Resolve(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{WebOidcAuthorityResolver.AuthorityKey}*");
    }

    [Fact]
    public void Resolve_MalformedAuthPublicUrl_ThrowsWithConfigurationKey()
    {
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            [WebOidcAuthorityResolver.AuthPublicUrlKey] = "not a url",
        });

        Action act = () => WebOidcAuthorityResolver.Resolve(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{WebOidcAuthorityResolver.AuthPublicUrlKey}*");
    }

    [Fact]
    public void Resolve_NullConfiguration_Throws()
    {
        Action act = () => WebOidcAuthorityResolver.Resolve(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static IConfiguration Build(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
