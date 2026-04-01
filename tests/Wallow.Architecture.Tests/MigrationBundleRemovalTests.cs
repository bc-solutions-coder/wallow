using System.Xml.Linq;

namespace Wallow.Architecture.Tests;

public sealed class MigrationBundleRemovalTests
{
    private static readonly string _repoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void ApiCsproj_ShouldNotContain_BuildMigrationBundlesTarget()
    {
        // Arrange
        string csprojPath = Path.Combine(_repoRoot, "src", "Wallow.Api", "Wallow.Api.csproj");
        XDocument document = XDocument.Load(csprojPath);
        XNamespace ns = document.Root!.GetDefaultNamespace();

        // Act
        IEnumerable<XElement> buildMigrationBundleTargets = document.Descendants(ns + "Target")
            .Where(t => t.Attribute("Name")?.Value == "BuildMigrationBundles");

        // Assert
        buildMigrationBundleTargets.Should().BeEmpty(
            "the BuildMigrationBundles MSBuild target should be removed from Wallow.Api.csproj");
    }

    [Fact]
    public void Entrypoint_ShouldNotContain_EfBundleReferences()
    {
        // Arrange
        string entrypointPath = Path.Combine(_repoRoot, "docker", "images", "api", "entrypoint.sh");
        string content = File.ReadAllText(entrypointPath);

        // Assert
        content.Should().NotContain("efbundle",
            "entrypoint.sh should not reference efbundle after migration bundle removal");
    }

    [Fact]
    public void Entrypoint_ShouldNotContain_BundleDirReferences()
    {
        // Arrange
        string entrypointPath = Path.Combine(_repoRoot, "docker", "images", "api", "entrypoint.sh");
        string content = File.ReadAllText(entrypointPath);

        // Assert
        content.Should().NotContain("BUNDLE_DIR",
            "entrypoint.sh should not reference BUNDLE_DIR after migration bundle removal");
    }
}
