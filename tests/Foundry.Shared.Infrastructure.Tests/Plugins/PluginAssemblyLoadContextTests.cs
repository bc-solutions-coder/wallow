using System.Reflection;
using System.Runtime.Loader;
using Foundry.Shared.Infrastructure.Plugins;

namespace Foundry.Shared.Infrastructure.Tests.Plugins;

public sealed class PluginAssemblyLoadContextTests : IDisposable
{
    private readonly string _tempDirectory;

    public PluginAssemblyLoadContextTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"plugin-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Constructor_WithValidPath_SetsIsCollectibleToTrue()
    {
        PluginAssemblyLoadContext context = new(_tempDirectory);

        context.IsCollectible.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithValidPath_CreatesInstance()
    {
        PluginAssemblyLoadContext context = new(_tempDirectory);

        context.Should().NotBeNull();
        context.Should().BeAssignableTo<AssemblyLoadContext>();
    }

    [Fact]
    public void Load_WithEmptyAssemblyName_ThrowsArgumentException()
    {
        PluginAssemblyLoadContext context = new(_tempDirectory);
        AssemblyName assemblyName = new();

        // Runtime rejects empty assembly names before Load is invoked
        Action act = () => context.LoadFromAssemblyName(assemblyName);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Load_WhenDllNotInPluginDirectory_FallsBackToDefaultContext()
    {
        PluginAssemblyLoadContext context = new(_tempDirectory);
        AssemblyName assemblyName = new("NonExistent.Assembly");

        // When DLL doesn't exist in plugin dir, Load returns null which
        // causes fallback to Default context. Default context will throw
        // FileNotFoundException for truly unknown assemblies.
        Action act = () => context.LoadFromAssemblyName(assemblyName);

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Load_WhenDllExistsInPluginDirectory_LoadsAssembly()
    {
        // Copy a real assembly to the temp plugin directory
        string sourceAssembly = typeof(PluginAssemblyLoadContext).Assembly.Location;
        string targetPath = Path.Combine(_tempDirectory, Path.GetFileName(sourceAssembly));
        File.Copy(sourceAssembly, targetPath);

        PluginAssemblyLoadContext context = new(_tempDirectory);
        string assemblyName = Path.GetFileNameWithoutExtension(sourceAssembly);

        Assembly result = context.LoadFromAssemblyName(new AssemblyName(assemblyName));

        result.Should().NotBeNull();
        result.GetName().Name.Should().Be(assemblyName);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup
        }

    }
}
