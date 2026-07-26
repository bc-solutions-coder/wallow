using Microsoft.Extensions.Configuration;
using Wallow.Identity.Infrastructure.Extensions;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Verifies that the issuer OpenIddict advertises resolves to the unified auth origin the
/// browser reaches /connect/* through, not to the API's own origin.
/// </summary>
public sealed class OpenIddictIssuerResolverTests
{
    private const string UnifiedAuthOrigin = "https://auth.example.test";
    private const string ExplicitIssuer = "https://issuer.example.test";

    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    [Fact]
    public void Resolve_ExplicitIssuerConfigured_ReturnsExplicitIssuer()
    {
        IConfiguration configuration = BuildConfiguration((OpenIddictIssuerResolver.IssuerKey, ExplicitIssuer));

        Uri? issuer = OpenIddictIssuerResolver.Resolve(configuration);

        issuer.Should().Be(new Uri(ExplicitIssuer));
    }

    [Fact]
    public void Resolve_ExplicitIssuerConfigured_TakesPrecedenceOverAuthUrl()
    {
        IConfiguration configuration = BuildConfiguration(
            (OpenIddictIssuerResolver.IssuerKey, ExplicitIssuer),
            (OpenIddictIssuerResolver.AuthUrlKey, UnifiedAuthOrigin));

        Uri? issuer = OpenIddictIssuerResolver.Resolve(configuration);

        issuer.Should().Be(new Uri(ExplicitIssuer), "the deployment override must win over the derived origin");
    }

    [Fact]
    public void Resolve_NoExplicitIssuer_FallsBackToUnifiedAuthOrigin()
    {
        IConfiguration configuration = BuildConfiguration((OpenIddictIssuerResolver.AuthUrlKey, UnifiedAuthOrigin));

        Uri? issuer = OpenIddictIssuerResolver.Resolve(configuration);

        issuer.Should().Be(new Uri(UnifiedAuthOrigin));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankExplicitIssuer_FallsBackToUnifiedAuthOrigin(string blank)
    {
        IConfiguration configuration = BuildConfiguration(
            (OpenIddictIssuerResolver.IssuerKey, blank),
            (OpenIddictIssuerResolver.AuthUrlKey, UnifiedAuthOrigin));

        Uri? issuer = OpenIddictIssuerResolver.Resolve(configuration);

        issuer.Should().Be(
            new Uri(UnifiedAuthOrigin),
            "every checked-in appsettings leaves OpenIddict:Issuer blank, which must not defeat the fallback");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NothingConfigured_ReturnsNull(string blank)
    {
        IConfiguration configuration = BuildConfiguration(
            (OpenIddictIssuerResolver.IssuerKey, blank),
            (OpenIddictIssuerResolver.AuthUrlKey, blank));

        Uri? issuer = OpenIddictIssuerResolver.Resolve(configuration);

        issuer.Should().BeNull("OpenIddict must keep deriving the issuer from the request origin when unconfigured");
    }

    [Fact]
    public void Resolve_AuthOriginWithPathBase_PreservesPath()
    {
        IConfiguration configuration = BuildConfiguration(
            (OpenIddictIssuerResolver.AuthUrlKey, "https://wallow.dev/auth"));

        Uri? issuer = OpenIddictIssuerResolver.Resolve(configuration);

        issuer.Should().Be(new Uri("https://wallow.dev/auth"), "the proxy may mount the auth app under a path base");
    }

    [Fact]
    public void Resolve_RelativeExplicitIssuer_ThrowsWithConfigurationKey()
    {
        IConfiguration configuration = BuildConfiguration((OpenIddictIssuerResolver.IssuerKey, "/connect"));

        Action act = () => OpenIddictIssuerResolver.Resolve(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{OpenIddictIssuerResolver.IssuerKey}*");
    }

    [Fact]
    public void Resolve_MalformedAuthOrigin_ThrowsWithConfigurationKey()
    {
        IConfiguration configuration = BuildConfiguration((OpenIddictIssuerResolver.AuthUrlKey, "not a url"));

        Action act = () => OpenIddictIssuerResolver.Resolve(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{OpenIddictIssuerResolver.AuthUrlKey}*");
    }
}
