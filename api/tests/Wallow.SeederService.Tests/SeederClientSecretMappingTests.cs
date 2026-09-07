using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Checks client-ID-based secret overrides, blank values and malformed client entries.
/// </summary>
public class SeederClientSecretMappingTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=seeder_secret_test;Username=test;Password=test";

    [Fact]
    public void ClientSecrets_AttachesSecretToClientById()
    {
        PreRegisteredClientOptions options = BuildOptions(new Dictionary<string, string?>
        {
            ["Clients:0:ClientId"] = "wallow-web-client",
            ["ClientSecrets:wallow-web-client"] = "s3cret-value",
        });

        options.Clients.Should().ContainSingle()
            .Which.Secret.Should().Be("s3cret-value");
    }

    [Fact]
    public void ClientSecrets_MatchesClientIdCaseInsensitively()
    {
        PreRegisteredClientOptions options = BuildOptions(new Dictionary<string, string?>
        {
            ["Clients:0:ClientId"] = "wallow-web-client",
            ["ClientSecrets:WALLOW-WEB-CLIENT"] = "s3cret-value",
        });

        options.Clients.Should().ContainSingle()
            .Which.Secret.Should().Be("s3cret-value");
    }

    [Fact]
    public void ClientSecrets_BlankValueForUnknownClient_IsIgnored()
    {
        // Optional secret overrides may be present with empty values.
        PreRegisteredClientOptions options = BuildOptions(new Dictionary<string, string?>
        {
            ["Clients:0:ClientId"] = "wallow-web-client",
            ["Clients:0:Secret"] = "s3cret-value",
            ["ClientSecrets:optional-spa-client"] = "",
            ["ClientSecrets:sa-optional-worker"] = "",
        });

        options.Clients.Should().ContainSingle()
            .Which.ClientId.Should().Be("wallow-web-client");
    }

    [Fact]
    public void ClientSecrets_NonBlankValueForUnknownClient_ThrowsNamingTheKey()
    {
        // A nonempty secret for an unknown client must reveal the configuration mistake.
        Action resolve = () => BuildOptions(new Dictionary<string, string?>
        {
            ["Clients:0:ClientId"] = "wallow-web-client",
            ["Clients:0:Secret"] = "s3cret-value",
            ["ClientSecrets:ghost-client"] = "orphaned-secret",
        });

        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage("*ghost-client*");
    }

    [Fact]
    public void ClientSecrets_BlankValueForKnownClient_LeavesSecretUnset()
    {
        // An empty override must leave the client secret unset.
        PreRegisteredClientOptions options = BuildOptions(new Dictionary<string, string?>
        {
            ["Clients:0:ClientId"] = "wallow-web-client",
            ["ClientSecrets:wallow-web-client"] = "",
        });

        options.Clients.Should().ContainSingle()
            .Which.Secret.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Clients_EntryWithBlankClientId_ThrowsNamingTheIndex()
    {
        // An out-of-range indexed override creates an entry without a client ID.
        Action resolve = () => BuildOptions(new Dictionary<string, string?>
        {
            ["Clients:0:ClientId"] = "wallow-web-client",
            ["Clients:0:Secret"] = "s3cret-value",
            ["Clients:2:RedirectUris:0"] = "https://example.test/callback",
        });

        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage("*index 2*");
    }

    private static PreRegisteredClientOptions BuildOptions(Dictionary<string, string?> configValues)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSeederIdentityServices(configuration, ConnectionString);

        using ServiceProvider provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<PreRegisteredClientOptions>>().Value;
    }
}
