using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Wallow.Architecture.Tests;

public class AspirePackageVersionTests
{
    private static readonly string _directoryPackagesPropsPath = Path.Combine(
        GetRepositoryRoot(),
        "Directory.Packages.props");

    private static readonly string[] _aspirePackages =
    [
        "Aspire.Hosting",
        "Aspire.Hosting.AppHost",
        "Aspire.Hosting.PostgreSQL",
        "Aspire.Hosting.Redis",
        "Microsoft.Extensions.ServiceDiscovery",
    ];

    private readonly XDocument _propsDocument;

    public AspirePackageVersionTests()
    {
        _propsDocument = XDocument.Load(_directoryPackagesPropsPath);
    }

    [Theory]
    [MemberData(nameof(GetAspirePackageNames))]
    public void DirectoryPackagesProps_ShouldDeclare_AspirePackage(string packageName)
    {
        XElement? entry = FindPackageVersionElement(packageName);

        entry.Should().NotBeNull(
            $"Directory.Packages.props should declare a PackageVersion entry for '{packageName}'");
    }

    [Theory]
    [MemberData(nameof(GetAspirePackageNames))]
    public void AspirePackage_ShouldHaveNonEmptyVersion(string packageName)
    {
        XElement? entry = FindPackageVersionElement(packageName);

        // Skip assertion if the element itself is missing (covered by previous test)
        if (entry is null)
        {
            entry.Should().NotBeNull($"Cannot check version: '{packageName}' entry is missing");
            return;
        }

        string? version = entry.Attribute("Version")?.Value;

        version.Should().NotBeNullOrWhiteSpace(
            $"PackageVersion entry for '{packageName}' must have a non-empty Version attribute");
    }

    [Theory]
    [MemberData(nameof(GetAspirePackageNames))]
    public void AspirePackage_VersionShouldFollowSemver(string packageName)
    {
        XElement? entry = FindPackageVersionElement(packageName);

        if (entry is null)
        {
            entry.Should().NotBeNull($"Cannot check semver: '{packageName}' entry is missing");
            return;
        }

        string? version = entry.Attribute("Version")?.Value;

        if (string.IsNullOrWhiteSpace(version))
        {
            version.Should().NotBeNullOrWhiteSpace($"Cannot check semver: '{packageName}' version is empty");
            return;
        }

        // Semver pattern: major.minor.patch with optional pre-release suffix
        Regex semverPattern = new(@"^\d+\.\d+\.\d+(-[\w.]+)?$");
        bool isSemver = semverPattern.IsMatch(version);

        isSemver.Should().BeTrue(
            $"Version '{version}' for '{packageName}' should follow semver format (e.g. 9.1.0 or 9.1.0-preview.1)");
    }

    [Fact]
    public void MicrosoftExtensionsHttpResilience_ShouldRemainUnchanged()
    {
        XElement? entry = FindPackageVersionElement("Microsoft.Extensions.Http.Resilience");

        entry.Should().NotBeNull(
            "Microsoft.Extensions.Http.Resilience must remain in Directory.Packages.props");

        string? version = entry!.Attribute("Version")?.Value;

        version.Should().Be("10.4.0",
            "Microsoft.Extensions.Http.Resilience version should remain at 10.4.0");
    }

    public static TheoryData<string> GetAspirePackageNames()
    {
        TheoryData<string> data = new();
        foreach (string package in _aspirePackages)
        {
            data.Add(package);
        }

        return data;
    }

    private XElement? FindPackageVersionElement(string packageName)
    {
        return _propsDocument
            .Descendants("PackageVersion")
            .FirstOrDefault(e =>
                string.Equals(
                    e.Attribute("Include")?.Value,
                    packageName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRepositoryRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Directory.Packages.props")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find repository root containing Directory.Packages.props");
    }
}
