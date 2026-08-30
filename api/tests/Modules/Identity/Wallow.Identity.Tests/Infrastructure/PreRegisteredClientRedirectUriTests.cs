using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Seed sync applies the same redirect-URI rule as the organization and admin surfaces: an
/// absolute, fragment-free HTTPS URI, or HTTP only on loopback. A seed that breaks it fails
/// before any client is written, naming the client and the URI.
/// </summary>
public sealed class PreRegisteredClientRedirectUriTests
{
    [Theory]
    [InlineData("http://app.example.com/callback")]
    [InlineData("https://app.example.com/callback#fragment")]
    [InlineData("callback")]
    public void Validate_ClientWithARefusedRedirectUri_ThrowsNamingClientAndUri(string redirectUri)
    {
        PreRegisteredClientOptions options = Seed(redirectUris: [redirectUri]);

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*acme-portal*").WithMessage($"*{redirectUri}*");
    }

    [Fact]
    public void Validate_ClientWithARefusedPostLogoutUri_Throws()
    {
        PreRegisteredClientOptions options = Seed(postLogoutRedirectUris: ["http://app.example.com/"]);

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*acme-portal*");
    }

    [Theory]
    [InlineData("https://app.example.com/callback")]
    [InlineData("http://localhost:3000/callback")]
    [InlineData("http://127.0.0.1:3000/callback")]
    public void Validate_ClientWithAnAcceptedRedirectUri_Passes(string redirectUri)
    {
        PreRegisteredClientOptions options = Seed(redirectUris: [redirectUri]);

        Action validate = options.Validate;

        validate.Should().NotThrow();
    }

    private static PreRegisteredClientOptions Seed(
        string[]? redirectUris = null,
        string[]? postLogoutRedirectUris = null)
    {
        PreRegisteredClientOptions options = new();
        PreRegisteredClientDefinition client = new()
        {
            ClientId = "acme-portal",
            DisplayName = "Acme Portal",
            Secret = "s",
            TenantName = "Acme"
        };
        foreach (string uri in redirectUris ?? [])
        {
            client.RedirectUris.Add(uri);
        }

        foreach (string uri in postLogoutRedirectUris ?? [])
        {
            client.PostLogoutRedirectUris.Add(uri);
        }

        options.Clients.Add(client);
        return options;
    }
}
