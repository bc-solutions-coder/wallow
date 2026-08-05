using Microsoft.Extensions.Configuration;
using Wallow.Modules.Registry;

namespace Wallow.Architecture.Tests.Modules;

/// <summary>
/// Guards the correspondence between each shipped appsettings file's <c>FeatureManagement</c>
/// <c>Modules.*</c> keys and the modules <see cref="WallowModuleRegistry.All"/> actually ships.
/// </summary>
/// <remarks>
/// <para>
/// The real, checked-in files are loaded through the same
/// <see cref="ConfigurationBuilder"/>/<c>AddJsonFile</c> pipeline the host uses, and the
/// assertions read <see cref="IConfiguration"/> keys — never file text. That makes this a
/// runtime-configuration assertion rather than a source-structure check; see
/// <c>Wallow.Identity.Tests/Infrastructure/DevelopmentIssuerOriginTests.cs</c> for the precedent.
/// </para>
/// <para>
/// The flag set is load-bearing in three ways at once: a module the host does not enable gets no
/// DI registration, no Wolverine handler discovery, and (since its <c>.Api</c> ApplicationPart is
/// pruned) no HTTP surface at all. A key naming a module that does not exist is therefore dead
/// weight, and a core module's key is inert because <c>ResolveEnabledModules</c> short-circuits on
/// <c>IsCore</c> before it ever asks the feature manager.
/// </para>
/// </remarks>
public sealed class ModuleFeatureFlagTests
{
    /// <summary>
    /// Gets every shipped environment overlay, each of which is merged over the base
    /// <c>appsettings.json</c> exactly as the host merges it.
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
