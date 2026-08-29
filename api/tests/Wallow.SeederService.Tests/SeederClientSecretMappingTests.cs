using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Production injects OIDC client secrets over a secret-less seed file. The contract is
/// name-keyed: a <c>ClientSecrets__&lt;clientId&gt;</c> environment variable attaches its value to
/// the seed client with that id. The old index-keyed contract (<c>Clients__N__Secret</c>) had two
/// failure modes these tests pin the replacements for: Compose renders an unset optional variable
/// as an EMPTY env var, which the binder materialised as a phantom secret-less client with a blank
/// ClientId (aborting the seeder with an offender list of blank names), and a seed file whose
/// client order drifted from the compose file silently attached secrets to the wrong clients.
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
        // Compose interpolates an unset optional variable as an EMPTY string, so a deployment
        // that defines only the dashboard client still ships blank entries for any optional
        // clients it never declared. Those must be a non-event, not a startup failure.
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
        // A real secret aimed at a client the seed file does not define is a misconfiguration
        // (typo'd id, or a client removed from the seed without removing its secret) and must
        // fail closed rather than be dropped silently.
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
        // A blank value must not count as "secret supplied": the client stays secret-less so the
        // existing fail-closed Validate() aborts a confidential client whose variable was unset.
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
        // An index-based override pointing past the end of the seed file's clients array
        // (e.g. Clients__2__RedirectUris__0 against a one-client seed) materialises a phantom
        // client with a blank id. Name the index so the misalignment diagnoses itself.
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
