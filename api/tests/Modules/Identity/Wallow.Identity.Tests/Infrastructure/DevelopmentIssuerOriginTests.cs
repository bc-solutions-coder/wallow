using Microsoft.Extensions.Configuration;
using Wallow.Identity.Infrastructure.Extensions;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Runs the issuer resolver against checked-in development settings and excludes the listed reserved ports.
/// </summary>
public sealed class DevelopmentIssuerOriginTests
{
    /// <summary>
    /// Ports excluded by the development issuer test.
    /// </summary>
    public static TheoryData<int, string> ReservedPorts =>
        new()
        {
            { 5001, "Wallow.Api serves the API on :5001; the issuer must be the auth origin, not the API's own" },
            { 5002, "the deleted legacy Blazor Wallow.Auth app owned :5002; nothing serves /connect/* there" },
            { 5003, "the Blazor Wallow.Web dashboard owns :5003" },
            { 5004, "the DocFX docs site owns :5004" },
            { 3000, "apps/wallow-web's dev server owns :3000" },
            { 3001, "Grafana owns :3001" },
        };

    private static Uri ResolveDevelopmentIssuer()
    {
        Uri? issuer = OpenIddictIssuerResolver.Resolve(BuildDevelopmentConfiguration());

        issuer.Should().NotBeNull(
            "Development must advertise an explicit issuer on the unified auth origin rather than "
            + "leaving OpenIddict to derive one from the API's request origin");

        return issuer!;
    }

    private static IConfiguration BuildDevelopmentConfiguration()
    {
        string settingsPath = Path.Combine(
            GetSolutionRoot(), "src", "Wallow.Api", "appsettings.Development.json");

        File.Exists(settingsPath).Should().BeTrue(
            $"the Development settings the API actually loads should exist at {settingsPath}");

        return new ConfigurationBuilder()
            .AddJsonFile(settingsPath, optional: false)
            .Build();
    }

    private static string GetSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Wallow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Solution root not found");
    }

    [Fact]
    public void DevelopmentIssuer_IsAnAbsoluteLoopbackHttpOrigin()
    {
        Uri issuer = ResolveDevelopmentIssuer();

        issuer.IsAbsoluteUri.Should().BeTrue("OpenIddict rejects a relative issuer at startup");
        issuer.Scheme.Should().Be(Uri.UriSchemeHttp, "the local dev auth origin is served over plain HTTP");
        issuer.Host.Should().Be("localhost", "the Development issuer must stay on the loopback host");
    }

    [Theory]
    [MemberData(nameof(ReservedPorts))]
    public void DevelopmentIssuer_DoesNotOccupyAPortOwnedByAnotherApp(int reservedPort, string owner)
    {
        Uri issuer = ResolveDevelopmentIssuer();

        issuer.Port.Should().NotBe(reservedPort, owner);
    }

    [Fact]
    public void DevelopmentIssuer_HasNoPathBase()
    {
        Uri issuer = ResolveDevelopmentIssuer();

        issuer.AbsolutePath.Should().Be(
            "/",
            "apps/wallow-auth's dev server mounts /connect/* at the root, so a path base would "
            + "make the advertised endpoints unreachable");
    }
}
