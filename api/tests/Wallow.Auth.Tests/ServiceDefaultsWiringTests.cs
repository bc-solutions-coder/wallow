using System.Reflection;

namespace Wallow.Auth.Tests;

public sealed class ServiceDefaultsWiringTests
{
    private static readonly Assembly _authAssembly = typeof(Wallow.Auth.Configuration.BrandingOptions).Assembly;

    [Fact]
    public void AuthAssembly_References_ServiceDefaults()
    {
        AssemblyName[] referencedAssemblies = _authAssembly.GetReferencedAssemblies();

        bool referencesServiceDefaults = referencedAssemblies
            .Any(a => a.Name == "Wallow.ServiceDefaults");

        referencesServiceDefaults.Should().BeTrue(
            "Wallow.Auth should reference Wallow.ServiceDefaults for shared resilience and telemetry configuration");
    }

    [Fact]
    public void AuthProgramCs_DoesNotContain_AddStandardResilienceHandler()
    {
        // Read Program.cs from the Auth project source to verify Polly resilience has been replaced
        string authProjectDir = FindAuthProjectDirectory();
        string programCsPath = Path.Combine(authProjectDir, "Program.cs");

        string programContents = File.ReadAllText(programCsPath);

        programContents.Should().NotContain("AddStandardResilienceHandler",
            "Wallow.Auth Program.cs should not use AddStandardResilienceHandler directly; " +
            "resilience should come from Wallow.ServiceDefaults");
    }

    [Fact]
    public void AuthProgramCs_DoesNotImport_PollyNamespace()
    {
        string authProjectDir = FindAuthProjectDirectory();
        string programCsPath = Path.Combine(authProjectDir, "Program.cs");

        string programContents = File.ReadAllText(programCsPath);

        programContents.Should().NotContain("using Polly",
            "Wallow.Auth Program.cs should not directly reference Polly; " +
            "resilience should be configured via Wallow.ServiceDefaults");
    }

    private static string FindAuthProjectDirectory()
    {
        // Walk up from the test assembly output directory to find the repo root,
        // then navigate to the Auth project
        string assemblyLocation = Path.GetDirectoryName(_authAssembly.Location)!;
        DirectoryInfo? directory = new DirectoryInfo(assemblyLocation);

        while (directory is not null)
        {
            string candidatePath = Path.Combine(directory.FullName, "src", "Wallow.Auth");
            if (Directory.Exists(candidatePath))
            {
                return candidatePath;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not find src/Wallow.Auth directory. Ensure tests run from within the repository.");
    }
}
