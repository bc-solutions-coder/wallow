using Wallow.Shared.Infrastructure.Plugins;
using Wallow.Shared.Kernel.Plugins;

namespace Wallow.Shared.Infrastructure.Tests.Plugins;

public class PluginRegistryTests
{
    private readonly PluginRegistry _sut = new();

    private static PluginManifest CreateManifest(string id = "test-plugin", string name = "Test Plugin") =>
        new(id, name, "1.0.0", "A test plugin", "Test Author", "1.0.0",
            "TestPlugin.dll", [], [], []);

    [Fact]
    public void Register_NewPlugin_CreatesEntryWithDiscoveredState()
    {
        PluginManifest manifest = CreateManifest();

        _sut.Register(manifest);

        PluginRegistryEntry? entry = _sut.GetEntry(manifest.Id);
        entry.Should().NotBeNull();
        entry.Manifest.Should().Be(manifest);
        entry.State.Should().Be(PluginLifecycleState.Discovered);
        entry.Instance.Should().BeNull();
        entry.LoadContext.Should().BeNull();
    }

    [Fact]
    public void Register_DuplicateId_DoesNotOverwriteExisting()
    {
        PluginManifest first = CreateManifest("dup", "First");
        PluginManifest second = CreateManifest("dup", "Second");

        _sut.Register(first);
        _sut.Register(second);

        PluginRegistryEntry? entry = _sut.GetEntry("dup");
        entry!.Manifest.Name.Should().Be("First");
    }

    [Fact]
    public void GetEntry_NonExistentId_ReturnsNull()
    {
        PluginRegistryEntry? result = _sut.GetEntry("does-not-exist");

        result.Should().BeNull();
    }

    [Fact]
    public void GetAll_Empty_ReturnsEmptyCollection()
    {
        IReadOnlyCollection<PluginRegistryEntry> result = _sut.GetAll();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_MultiplePlugins_ReturnsAll()
    {
        _sut.Register(CreateManifest("plugin-a"));
        _sut.Register(CreateManifest("plugin-b"));
        _sut.Register(CreateManifest("plugin-c"));

        IReadOnlyCollection<PluginRegistryEntry> result = _sut.GetAll();

        result.Should().HaveCount(3);
        result.Select(e => e.Manifest.Id).Should().BeEquivalentTo(["plugin-a", "plugin-b", "plugin-c"]);
    }

    [Fact]
    public void UpdateState_ExistingPlugin_ChangesState()
    {
        _sut.Register(CreateManifest());

        _sut.UpdateState("test-plugin", PluginLifecycleState.Enabled);

        _sut.GetEntry("test-plugin")!.State.Should().Be(PluginLifecycleState.Enabled);
    }

    [Fact]
    public void UpdateState_NonExistentPlugin_DoesNotThrow()
    {
        Action act = () => _sut.UpdateState("missing", PluginLifecycleState.Enabled);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetInstance_ExistingPlugin_SetsInstanceAndLoadContext()
    {
        _sut.Register(CreateManifest());
        IWallowPlugin instance = NSubstitute.Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new PluginAssemblyLoadContext(Path.GetTempPath());

        _sut.SetInstance("test-plugin", instance, loadContext);

        PluginRegistryEntry? entry = _sut.GetEntry("test-plugin");
        entry!.Instance.Should().BeSameAs(instance);
        entry.LoadContext.Should().BeSameAs(loadContext);
    }

    [Fact]
    public void SetInstance_NonExistentPlugin_DoesNotThrow()
    {
        IWallowPlugin instance = NSubstitute.Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new PluginAssemblyLoadContext(Path.GetTempPath());

        Action act = () => _sut.SetInstance("missing", instance, loadContext);

        act.Should().NotThrow();
    }

    [Fact]
    public void Remove_ExistingPlugin_RemovesFromRegistry()
    {
        _sut.Register(CreateManifest());

        _sut.Remove("test-plugin");

        _sut.GetEntry("test-plugin").Should().BeNull();
        _sut.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Remove_NonExistentPlugin_DoesNotThrow()
    {
        Action act = () => _sut.Remove("missing");

        act.Should().NotThrow();
    }

    [Fact]
    public void FullLifecycle_RegisterUpdateRemove_WorksCorrectly()
    {
        PluginManifest manifest = CreateManifest();
        _sut.Register(manifest);

        _sut.UpdateState(manifest.Id, PluginLifecycleState.Installed);
        _sut.UpdateState(manifest.Id, PluginLifecycleState.Enabled);
        _sut.UpdateState(manifest.Id, PluginLifecycleState.Disabled);
        _sut.UpdateState(manifest.Id, PluginLifecycleState.Uninstalled);

        _sut.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Uninstalled);

        _sut.Remove(manifest.Id);

        _sut.GetEntry(manifest.Id).Should().BeNull();
    }
}
