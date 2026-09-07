using Microsoft.Extensions.Configuration;
using Wallow.Modules.Registry;

namespace Wallow.Architecture.Tests.Modules;

/// <summary>
/// Compares module keys in merged settings with the non-core registry modules.
/// </summary>
public sealed class ModuleFeatureFlagTests
{
    /// <summary>
    /// Settings overlays included in the module-key comparison.
    /// </summary>
    public static TheoryData<string> EnvironmentOverlays =>
        new()
        {
            "appsettings.json",
            "appsettings.Development.json",
            "appsettings.Production.json",
            "appsettings.Staging.json",
            "appsettings.Testing.json",
        };

    [Theory]
    [MemberData(nameof(EnvironmentOverlays))]
    public void MergedConfiguration_DeclaresExactlyTheNonCoreRegistryModules(string overlay)
    {
        IConfiguration configuration = BuildMergedConfiguration(overlay);

        IReadOnlyList<string> declared =
        [
            .. configuration
                .GetSection("FeatureManagement")
                .GetChildren()
                .Select(section => section.Key)
                .Where(key => key.StartsWith("Modules.", StringComparison.Ordinal))
                .Select(key => key["Modules.".Length..])
        ];

        IReadOnlyList<string> expected =
        [
            .. WallowModuleRegistry.All
                .Where(module => !module.IsCore)
                .Select(module => module.Name)
        ];

        declared.Should().BeEquivalentTo(
            expected,
            $"{overlay}'s merged FeatureManagement Modules.* keys must name exactly the non-core "
            + "modules WallowModuleRegistry.All ships — a core module's flag is never evaluated "
            + "(ResolveEnabledModules short-circuits on IsCore) and a key naming no module at all "
            + "silently toggles nothing");
    }

    [Fact]
    public void BaseConfiguration_HasNoSecondModulesBlock()
    {
        IConfiguration configuration = BuildMergedConfiguration("appsettings.json");

        configuration.GetSection("Wallow").GetSection("Modules").GetChildren().Should().BeEmpty(
            "FeatureManagement is the only section AddFeatureManagement binds by convention, so a "
            + "second copy of the module list under Wallow:Modules is read by nothing and free to "
            + "disagree with the copy that is read");
    }

    private static IConfiguration BuildMergedConfiguration(string overlay)
    {
        string apiDirectory = Path.Combine(GetSolutionRoot(), "src", "Wallow.Api");

        ConfigurationBuilder builder = new();
        builder.AddJsonFile(Path.Combine(apiDirectory, "appsettings.json"), optional: false);
        if (!string.Equals(overlay, "appsettings.json", StringComparison.Ordinal))
        {
            builder.AddJsonFile(Path.Combine(apiDirectory, overlay), optional: false);
        }

        return builder.Build();
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
}
