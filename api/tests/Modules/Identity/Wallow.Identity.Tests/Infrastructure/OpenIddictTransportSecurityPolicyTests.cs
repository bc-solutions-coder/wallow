using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Wallow.Identity.Infrastructure.Extensions;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Checks environment and explicit-override rules for allowing HTTP at the OpenIddict transport boundary.
/// </summary>
public sealed class OpenIddictTransportSecurityPolicyTests
{
    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    private static HostingEnvironment EnvironmentNamed(string environmentName) =>
        new() { EnvironmentName = environmentName };

    [Fact]
    public void AllowPlainHttpKey_IsScopedToTheOpenIddictSection()
    {
        OpenIddictTransportSecurityPolicy.AllowPlainHttpKey
            .Should().Be("OpenIddict:AllowPlainHttpEndpoints");
    }

    [Fact]
    public void ShouldDisable_Development_ReturnsTrue()
    {
        bool disable = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            EnvironmentNamed(Environments.Development), BuildConfiguration());

        disable.Should().BeTrue("local development runs Kestrel on plain http://localhost");
    }

    [Fact]
    public void ShouldDisable_TestingHost_ReturnsTrue()
    {
        bool disable = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            EnvironmentNamed(OpenIddictTransportSecurityPolicy.TestingEnvironmentName), BuildConfiguration());

        disable.Should().BeTrue(
            "WallowApiFactory boots the API as \"Testing\" and drives the OIDC endpoints over plain-http TestServer requests");
    }

    [Fact]
    public void ShouldDisable_Development_OptedOutExplicitly_StillReturnsTrue()
    {
        bool disable = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            EnvironmentNamed(Environments.Development),
            BuildConfiguration((OpenIddictTransportSecurityPolicy.AllowPlainHttpKey, "false")));

        disable.Should().BeTrue("development has no certificate to serve, so the flag cannot make it require HTTPS");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Preview")]
    public void ShouldDisable_DeployedEnvironmentWithoutOptIn_ReturnsFalse(string environmentName)
    {
        bool disable = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            EnvironmentNamed(environmentName), BuildConfiguration());

        disable.Should().BeFalse(
            $"'{environmentName}' is a deployed environment and must require HTTPS on the OIDC endpoints by default");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldDisable_ProductionWithBlankOptIn_ReturnsFalse(string blank)
    {
        bool disable = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            EnvironmentNamed(Environments.Production),
            BuildConfiguration((OpenIddictTransportSecurityPolicy.AllowPlainHttpKey, blank)));

        disable.Should().BeFalse("an unset or blank flag is not an opt-in");
    }

    [Fact]
    public void ShouldDisable_ProductionWithOptInDisabled_ReturnsFalse()
    {
        bool disable = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            EnvironmentNamed(Environments.Production),
            BuildConfiguration((OpenIddictTransportSecurityPolicy.AllowPlainHttpKey, "false")));

        disable.Should().BeFalse();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void ShouldDisable_ProductionWithExplicitOptIn_ReturnsTrue(string optIn)
    {
        bool disable = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            EnvironmentNamed(Environments.Production),
            BuildConfiguration((OpenIddictTransportSecurityPolicy.AllowPlainHttpKey, optIn)));

        disable.Should().BeTrue(
            "docker-compose.production.yml points the BFF at http://wallow-api:8080/.well-known/openid-configuration, "
            + "a private-network discovery call the operator declares safe by setting the flag");
    }

    [Fact]
    public void ShouldDisable_MalformedOptIn_ThrowsNamingTheConfigurationKey()
    {
        Action act = () => OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            EnvironmentNamed(Environments.Production),
            BuildConfiguration((OpenIddictTransportSecurityPolicy.AllowPlainHttpKey, "yes")));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{OpenIddictTransportSecurityPolicy.AllowPlainHttpKey}*",
                "a typo in the flag must fail loudly rather than silently resolving to plain HTTP or to HTTPS");
    }

    [Fact]
    public void ShouldDisable_NullEnvironment_Throws()
    {
        Action act = () => OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            null!, BuildConfiguration());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ShouldDisable_NullConfiguration_Throws()
    {
        Action act = () => OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            EnvironmentNamed(Environments.Development), null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
