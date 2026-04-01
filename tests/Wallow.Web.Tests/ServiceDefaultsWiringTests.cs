using System.Reflection;
using Wallow.Web.Configuration;

namespace Wallow.Web.Tests;

public sealed class ServiceDefaultsWiringTests
{
    [Fact]
    public void WebAssembly_ShouldReference_ServiceDefaultsAssembly()
    {
        // Arrange
        Assembly webAssembly = typeof(BrandingOptions).Assembly;

        // Act
        IReadOnlyList<string> referencedAssemblyNames = webAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        // Assert
        referencedAssemblyNames.Should().Contain("Wallow.ServiceDefaults",
            "Wallow.Web must reference Wallow.ServiceDefaults for shared resilience and observability configuration");
    }

    [Fact]
    public void ProgramCs_ShouldNotContain_PollyResilienceHandler()
    {
        // Arrange — locate Program.cs relative to the Web project assembly
        string assemblyLocation = Path.GetDirectoryName(typeof(BrandingOptions).Assembly.Location)!;

        // Walk up from bin/Debug/net10.0 to the project root, then read Program.cs
        string projectRoot = Path.GetFullPath(Path.Combine(assemblyLocation, "..", "..", ".."));
        string programCsPath = Path.Combine(projectRoot, "Program.cs");

        // Fallback: search from repo root
        if (!File.Exists(programCsPath))
        {
            string repoRoot = FindRepoRoot(assemblyLocation);
            programCsPath = Path.Combine(repoRoot, "src", "Wallow.Web", "Program.cs");
        }

        File.Exists(programCsPath).Should().BeTrue($"Program.cs should exist at {programCsPath}");

        // Act
        string programContent = File.ReadAllText(programCsPath);

        // Assert
        programContent.Should().NotContain("AddStandardResilienceHandler",
            "Wallow.Web should use ServiceDefaults for resilience instead of inline Polly configuration");
    }

    private static string FindRepoRoot(string startPath)
    {
        string? directory = startPath;
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                return directory;
            }
            directory = Path.GetDirectoryName(directory);
        }

        return startPath;
    }
}
