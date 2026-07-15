using System.Text.Json;
using Wallow.Shared.Infrastructure.Plugins;
using Wallow.Shared.Kernel.Plugins;

namespace Wallow.Shared.Infrastructure.Tests.Plugins;

public sealed class PluginManifestLoaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"wallow-test-{Guid.NewGuid()}");

    public PluginManifestLoaderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

    }

    [Fact]
    public void LoadFromDirectory_NonExistentDirectory_ReturnsEmptyList()
    {
        string nonExistent = Path.Combine(_tempDir, "does-not-exist");

        IReadOnlyList<PluginManifest> result = PluginManifestLoader.LoadFromDirectory(nonExistent);

        result.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromDirectory_EmptyDirectory_ReturnsEmptyList()
    {
        IReadOnlyList<PluginManifest> result = PluginManifestLoader.LoadFromDirectory(_tempDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromDirectory_SubdirectoryWithoutManifest_IsSkipped()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "no-manifest-plugin"));

        IReadOnlyList<PluginManifest> result = PluginManifestLoader.LoadFromDirectory(_tempDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromDirectory_ValidManifest_DeserializesCorrectly()
    {
        string pluginDir = Path.Combine(_tempDir, "my-plugin");
        Directory.CreateDirectory(pluginDir);
        WriteManifest(pluginDir, new
        {
            Id = "my-plugin",
            Name = "My Plugin",
            Version = "1.0.0",
            Description = "A test plugin",
            Author = "Test Author",
            MinWallowVersion = "1.0.0",
            EntryAssembly = "MyPlugin.dll",
            Dependencies = Array.Empty<object>(),
            RequiredPermissions = Array.Empty<string>(),
            ExportedServices = Array.Empty<string>()
        });

        IReadOnlyList<PluginManifest> result = PluginManifestLoader.LoadFromDirectory(_tempDir);

        result.Should().ContainSingle();
        PluginManifest manifest = result[0];
        manifest.Id.Should().Be("my-plugin");
        manifest.Name.Should().Be("My Plugin");
        manifest.Version.Should().Be("1.0.0");
        manifest.Description.Should().Be("A test plugin");
        manifest.Author.Should().Be("Test Author");
        manifest.EntryAssembly.Should().Be("MyPlugin.dll");
    }

    [Fact]
    public void LoadFromDirectory_ManifestMissingId_ThrowsPluginLoadException()
    {
        string pluginDir = Path.Combine(_tempDir, "bad-plugin");
        Directory.CreateDirectory(pluginDir);
        WriteManifest(pluginDir, new
        {
            Name = "Bad Plugin",
            Version = "1.0.0",
            Description = "",
            Author = "",
            MinWallowVersion = "",
            EntryAssembly = "Bad.dll",
            Dependencies = Array.Empty<object>(),
            RequiredPermissions = Array.Empty<string>(),
            ExportedServices = Array.Empty<string>()
        });

        Action act = () => PluginManifestLoader.LoadFromDirectory(_tempDir);

        act.Should().Throw<PluginLoadException>()
            .Which.Message.Should().Contain("Id");
    }

    [Fact]
    public void LoadFromDirectory_ManifestMissingName_ThrowsPluginLoadException()
    {
        string pluginDir = Path.Combine(_tempDir, "no-name-plugin");
        Directory.CreateDirectory(pluginDir);
        WriteManifest(pluginDir, new
        {
            Id = "no-name",
            Version = "1.0.0",
            Description = "",
            Author = "",
            MinWallowVersion = "",
            EntryAssembly = "NoName.dll",
            Dependencies = Array.Empty<object>(),
            RequiredPermissions = Array.Empty<string>(),
            ExportedServices = Array.Empty<string>()
        });

        Action act = () => PluginManifestLoader.LoadFromDirectory(_tempDir);

        act.Should().Throw<PluginLoadException>()
            .Which.Message.Should().Contain("Name");
    }

    [Fact]
    public void LoadFromDirectory_ManifestMissingMultipleFields_ThrowsWithAllFieldNames()
    {
        string pluginDir = Path.Combine(_tempDir, "empty-plugin");
        Directory.CreateDirectory(pluginDir);
        WriteManifest(pluginDir, new
        {
            Description = "",
            Author = "",
            MinWallowVersion = "",
            Dependencies = Array.Empty<object>(),
            RequiredPermissions = Array.Empty<string>(),
            ExportedServices = Array.Empty<string>()
        });

        Action act = () => PluginManifestLoader.LoadFromDirectory(_tempDir);

        PluginLoadException ex = act.Should().Throw<PluginLoadException>().Which;
        ex.Message.Should().Contain("Id")
            .And.Contain("Name")
            .And.Contain("Version")
            .And.Contain("EntryAssembly");
    }

    [Fact]
    public void LoadFromDirectory_MultiplePlugins_LoadsAll()
    {
        CreateValidPlugin("plugin-a", "Plugin A");
        CreateValidPlugin("plugin-b", "Plugin B");

        IReadOnlyList<PluginManifest> result = PluginManifestLoader.LoadFromDirectory(_tempDir);

        result.Should().HaveCount(2);
        result.Select(m => m.Id).Should().BeEquivalentTo(["plugin-a", "plugin-b"]);
    }

    private void CreateValidPlugin(string id, string name)
    {
        string pluginDir = Path.Combine(_tempDir, id);
        Directory.CreateDirectory(pluginDir);
        WriteManifest(pluginDir, new
        {
            Id = id,
            Name = name,
            Version = "1.0.0",
            Description = "Test",
            Author = "Test",
            MinWallowVersion = "1.0.0",
            EntryAssembly = $"{id}.dll",
            Dependencies = Array.Empty<object>(),
            RequiredPermissions = Array.Empty<string>(),
            ExportedServices = Array.Empty<string>()
        });
    }

    private static void WriteManifest(string pluginDir, object manifest)
    {
        string json = JsonSerializer.Serialize(manifest);
        File.WriteAllText(Path.Combine(pluginDir, "wallow-plugin.json"), json);
    }
}
