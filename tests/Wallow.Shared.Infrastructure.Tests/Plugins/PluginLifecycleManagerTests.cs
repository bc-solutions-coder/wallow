using Wallow.Shared.Infrastructure.Plugins;
using Wallow.Shared.Kernel.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Wallow.Shared.Infrastructure.Tests.Plugins;

public class PluginLifecycleManagerTests
{
    private readonly PluginRegistry _registry = new();
    private readonly PluginLoader _loader;
    private readonly IPluginPermissionValidator _permissionValidator;
    private readonly PluginLifecycleManager _sut;

    public PluginLifecycleManagerTests()
    {
        _loader = new PluginLoader(_registry, Options.Create(new PluginOptions()), NullLogger<PluginLoader>.Instance);
        _permissionValidator = Substitute.For<IPluginPermissionValidator>();
        IOptions<PluginOptions> options = Options.Create(new PluginOptions
        {
            PluginsDirectory = Path.Combine(Path.GetTempPath(), "wallow-test-plugins")
        });

        _sut = new PluginLifecycleManager(
            _registry,
            _loader,
            _permissionValidator,
            options,
            NullLogger<PluginLifecycleManager>.Instance);
    }

    private static PluginManifest CreateManifest(
        string id = "test-plugin",
        IReadOnlyList<string>? requiredPermissions = null) =>
        new(id, "Test Plugin", "1.0.0", "A test plugin", "Test Author", "1.0.0",
            "TestPlugin.dll", [], requiredPermissions ?? [], []);

    // -- EnablePluginAsync --

    [Fact]
    public async Task EnablePluginAsync_PluginInInstalledState_TransitionsToEnabled()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        await _sut.EnablePluginAsync(manifest.Id, services, config);

        PluginRegistryEntry? entry = _registry.GetEntry(manifest.Id);
        entry!.State.Should().Be(PluginLifecycleState.Enabled);
        plugin.Received(1).AddServices(services, config);
    }

    [Fact]
    public async Task EnablePluginAsync_MissingPermissions_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest(requiredPermissions: ["storage:read", "storage:write"]);
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        _permissionValidator.HasPermission("test-plugin", "storage:read").Returns(true);
        _permissionValidator.HasPermission("test-plugin", "storage:write").Returns(false);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        Func<Task> act = () => _sut.EnablePluginAsync(manifest.Id, services, config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*storage:write*");
    }

    [Fact]
    public async Task EnablePluginAsync_AllPermissionsGranted_Succeeds()
    {
        PluginManifest manifest = CreateManifest(requiredPermissions: ["db:read"]);
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        _permissionValidator.HasPermission("test-plugin", "db:read").Returns(true);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        await _sut.EnablePluginAsync(manifest.Id, services, config);

        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Enabled);
    }

    [Fact]
    public async Task EnablePluginAsync_InvalidStateTransition_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        Func<Task> act = () => _sut.EnablePluginAsync(manifest.Id, services, config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Discovered*Enabled*");
    }

    // -- DisablePluginAsync --

    [Fact]
    public async Task DisablePluginAsync_PluginInEnabledState_TransitionsToDisabled()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Enabled);

        await _sut.DisablePluginAsync(manifest.Id);

        PluginRegistryEntry? entry = _registry.GetEntry(manifest.Id);
        entry!.State.Should().Be(PluginLifecycleState.Disabled);
        await plugin.Received(1).ShutdownAsync();
    }

    [Fact]
    public async Task DisablePluginAsync_PluginNotEnabled_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        Func<Task> act = () => _sut.DisablePluginAsync(manifest.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Installed*Disabled*");
    }

    // -- InitializePluginAsync --

    [Fact]
    public async Task InitializePluginAsync_PluginInEnabledState_CallsInitializeOnPlugin()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Enabled);

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ILogger<PluginContext>>(NullLogger<PluginContext>.Instance);
        ServiceProvider sp = services.BuildServiceProvider();

        await _sut.InitializePluginAsync(manifest.Id, sp);

        await plugin.Received(1).InitializeAsync(Arg.Any<PluginContext>());
    }

    [Fact]
    public async Task InitializePluginAsync_PluginNotEnabled_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        Func<Task> act = () => _sut.InitializePluginAsync(manifest.Id, sp);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be Enabled*");
    }

    // -- DiscoverPluginsAsync --

    [Fact]
    public async Task DiscoverPluginsAsync_WithManifestFiles_RegistersPluginsInRegistry()
    {
        string pluginsDir = Path.Combine(Path.GetTempPath(), $"wallow-discover-{Guid.NewGuid()}");
        string pluginDir = Path.Combine(pluginsDir, "my-plugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            string manifestJson = """
                {
                    "Id": "my-plugin",
                    "Name": "My Plugin",
                    "Version": "2.0.0",
                    "Description": "A discovered plugin",
                    "Author": "Tester",
                    "MinWallowVersion": "1.0.0",
                    "EntryAssembly": "MyPlugin.dll",
                    "Dependencies": [],
                    "RequiredPermissions": [],
                    "ExportedServices": []
                }
                """;
            await File.WriteAllTextAsync(Path.Combine(pluginDir, "wallow-plugin.json"), manifestJson);

            IReadOnlyList<PluginManifest> result = await _sut.DiscoverPluginsAsync(pluginsDir);

            result.Should().ContainSingle();
            result[0].Id.Should().Be("my-plugin");
            result[0].Version.Should().Be("2.0.0");

            PluginRegistryEntry? entry = _registry.GetEntry("my-plugin");
            entry.Should().NotBeNull();
            entry.State.Should().Be(PluginLifecycleState.Discovered);
        }
        finally
        {
            Directory.Delete(pluginsDir, recursive: true);
        }
    }

    [Fact]
    public async Task DiscoverPluginsAsync_EmptyDirectory_ReturnsEmptyList()
    {
        string pluginsDir = Path.Combine(Path.GetTempPath(), $"wallow-discover-empty-{Guid.NewGuid()}");
        Directory.CreateDirectory(pluginsDir);

        try
        {
            IReadOnlyList<PluginManifest> result = await _sut.DiscoverPluginsAsync(pluginsDir);

            result.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(pluginsDir, recursive: true);
        }
    }

    [Fact]
    public async Task DiscoverPluginsAsync_NonExistentDirectory_ReturnsEmptyList()
    {
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"wallow-no-exist-{Guid.NewGuid()}");

        IReadOnlyList<PluginManifest> result = await _sut.DiscoverPluginsAsync(nonExistentPath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverPluginsAsync_MultiplePlugins_RegistersAllInRegistry()
    {
        string pluginsDir = Path.Combine(Path.GetTempPath(), $"wallow-discover-multi-{Guid.NewGuid()}");

        string pluginADir = Path.Combine(pluginsDir, "plugin-a");
        string pluginBDir = Path.Combine(pluginsDir, "plugin-b");
        Directory.CreateDirectory(pluginADir);
        Directory.CreateDirectory(pluginBDir);

        try
        {
            string manifestA = """
                {
                    "Id": "plugin-a",
                    "Name": "Plugin A",
                    "Version": "1.0.0",
                    "Description": "First plugin",
                    "Author": "Tester",
                    "MinWallowVersion": "1.0.0",
                    "EntryAssembly": "PluginA.dll",
                    "Dependencies": [],
                    "RequiredPermissions": [],
                    "ExportedServices": []
                }
                """;
            string manifestB = """
                {
                    "Id": "plugin-b",
                    "Name": "Plugin B",
                    "Version": "3.0.0",
                    "Description": "Second plugin",
                    "Author": "Tester",
                    "MinWallowVersion": "1.0.0",
                    "EntryAssembly": "PluginB.dll",
                    "Dependencies": [],
                    "RequiredPermissions": [],
                    "ExportedServices": []
                }
                """;
            await File.WriteAllTextAsync(Path.Combine(pluginADir, "wallow-plugin.json"), manifestA);
            await File.WriteAllTextAsync(Path.Combine(pluginBDir, "wallow-plugin.json"), manifestB);

            IReadOnlyList<PluginManifest> result = await _sut.DiscoverPluginsAsync(pluginsDir);

            result.Should().HaveCount(2);
            _registry.GetEntry("plugin-a").Should().NotBeNull();
            _registry.GetEntry("plugin-b").Should().NotBeNull();
        }
        finally
        {
            Directory.Delete(pluginsDir, recursive: true);
        }
    }

    // -- LoadPluginAsync --

    [Fact]
    public async Task LoadPluginAsync_NonExistentPlugin_ThrowsInvalidOperationException()
    {
        Func<Task> act = () => _sut.LoadPluginAsync("non-existent");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public async Task LoadPluginAsync_InvalidStateTransition_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Enabled);

        Func<Task> act = () => _sut.LoadPluginAsync(manifest.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Enabled*Installed*");
    }

    [Fact]
    public async Task LoadPluginAsync_AlreadyInstalledPlugin_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        Func<Task> act = () => _sut.LoadPluginAsync(manifest.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Installed*Installed*");
    }

    [Fact]
    public async Task LoadPluginAsync_DisabledPlugin_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Disabled);

        Func<Task> act = () => _sut.LoadPluginAsync(manifest.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Disabled*Installed*");
    }

    // -- Lifecycle ordering --

    [Fact]
    public async Task EnableThenDisable_FullLifecycle_CallsAddServicesThenShutdown()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        await _sut.EnablePluginAsync(manifest.Id, services, config);
        await _sut.DisablePluginAsync(manifest.Id);

        Received.InOrder(() =>
        {
            plugin.AddServices(services, config);
            plugin.ShutdownAsync();
        });

        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Disabled);
    }

    [Fact]
    public async Task DisabledPlugin_CanBeReEnabled()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Disabled);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        await _sut.EnablePluginAsync(manifest.Id, services, config);

        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Enabled);
    }

    [Fact]
    public async Task InitializePluginAsync_PluginInDiscoveredState_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);

        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        Func<Task> act = () => _sut.InitializePluginAsync(manifest.Id, sp);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be Enabled*");
    }

    [Fact]
    public async Task InitializePluginAsync_PluginInDisabledState_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Disabled);

        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        Func<Task> act = () => _sut.InitializePluginAsync(manifest.Id, sp);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be Enabled*");
    }

    [Fact]
    public async Task InitializePluginAsync_UnregisteredPlugin_ThrowsInvalidOperationException()
    {
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        Func<Task> act = () => _sut.InitializePluginAsync("not-registered", sp);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public async Task EnablePluginAsync_UnregisteredPlugin_ThrowsInvalidOperationException()
    {
        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        Func<Task> act = () => _sut.EnablePluginAsync("not-registered", services, config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public async Task DisablePluginAsync_UnregisteredPlugin_ThrowsInvalidOperationException()
    {
        Func<Task> act = () => _sut.DisablePluginAsync("not-registered");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public async Task EnablePluginAsync_NoRequiredPermissions_SkipsValidation()
    {
        PluginManifest manifest = CreateManifest(requiredPermissions: []);
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        await _sut.EnablePluginAsync(manifest.Id, services, config);

        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Enabled);
        _permissionValidator.DidNotReceive().HasPermission(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EnablePluginAsync_MultiplePermissionsMissing_ThrowsWithAllMissing()
    {
        PluginManifest manifest = CreateManifest(requiredPermissions: ["perm-a", "perm-b", "perm-c"]);
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        _permissionValidator.HasPermission("test-plugin", Arg.Any<string>()).Returns(false);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        Func<Task> act = () => _sut.EnablePluginAsync(manifest.Id, services, config);

        InvalidOperationException ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Contain("perm-a");
        ex.Message.Should().Contain("perm-b");
        ex.Message.Should().Contain("perm-c");
    }

    [Fact]
    public async Task DisablePluginAsync_AlreadyDisabled_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Disabled);

        Func<Task> act = () => _sut.DisablePluginAsync(manifest.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Disabled*Disabled*");
    }

    [Fact]
    public async Task DisablePluginAsync_InstalledState_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        Func<Task> act = () => _sut.DisablePluginAsync(manifest.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Installed*Disabled*");
    }

    [Fact]
    public async Task EnablePluginAsync_PluginAlreadyEnabled_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Enabled);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        Func<Task> act = () => _sut.EnablePluginAsync(manifest.Id, services, config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Enabled*Enabled*");
    }

    [Fact]
    public async Task DisablePluginAsync_PluginInDiscoveredState_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);

        Func<Task> act = () => _sut.DisablePluginAsync(manifest.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Discovered*Disabled*");
    }

    // -- Uninstalled state transitions --

    [Fact]
    public async Task LoadPluginAsync_UninstalledState_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Uninstalled);

        Func<Task> act = () => _sut.LoadPluginAsync(manifest.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Uninstalled*Installed*");
    }

    [Fact]
    public async Task EnablePluginAsync_UninstalledState_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Uninstalled);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        Func<Task> act = () => _sut.EnablePluginAsync(manifest.Id, services, config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Uninstalled*Enabled*");
    }

    [Fact]
    public async Task DisablePluginAsync_UninstalledState_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Uninstalled);

        Func<Task> act = () => _sut.DisablePluginAsync(manifest.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid state transition*Uninstalled*Disabled*");
    }

    [Fact]
    public async Task InitializePluginAsync_UninstalledState_ThrowsInvalidOperationException()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Uninstalled);

        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        Func<Task> act = () => _sut.InitializePluginAsync(manifest.Id, sp);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be Enabled*");
    }

    // -- Re-enable with permissions --

    [Fact]
    public async Task EnablePluginAsync_DisabledStateWithPermissions_ValidatesPermissions()
    {
        PluginManifest manifest = CreateManifest(requiredPermissions: ["api:read"]);
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Disabled);

        _permissionValidator.HasPermission("test-plugin", "api:read").Returns(true);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        await _sut.EnablePluginAsync(manifest.Id, services, config);

        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Enabled);
        _permissionValidator.Received(1).HasPermission("test-plugin", "api:read");
    }

    [Fact]
    public async Task EnablePluginAsync_DisabledStateWithMissingPermissions_Throws()
    {
        PluginManifest manifest = CreateManifest(requiredPermissions: ["api:write"]);
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Disabled);

        _permissionValidator.HasPermission("test-plugin", "api:write").Returns(false);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        Func<Task> act = () => _sut.EnablePluginAsync(manifest.Id, services, config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*api:write*");
    }

    [Fact]
    public async Task EnableInitializeDisable_FullLifecycle_TransitionsCorrectly()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();
        services.AddSingleton(config);
        services.AddSingleton<ILogger<PluginContext>>(NullLogger<PluginContext>.Instance);
        ServiceProvider sp = services.BuildServiceProvider();

        await _sut.EnablePluginAsync(manifest.Id, services, config);
        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Enabled);

        await _sut.InitializePluginAsync(manifest.Id, sp);
        await plugin.Received(1).InitializeAsync(Arg.Any<PluginContext>());

        await _sut.DisablePluginAsync(manifest.Id);
        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Disabled);

        Received.InOrder(() =>
        {
            plugin.AddServices(services, config);
            plugin.InitializeAsync(Arg.Any<PluginContext>());
            plugin.ShutdownAsync();
        });
    }
}

/// <summary>
/// Tests that exercise LoggerMessage-generated code paths by using a logger
/// where IsEnabled returns true, causing the generated formatting code to execute.
/// </summary>
public sealed class PluginLifecycleManagerLoggingTests : IDisposable
{
    private readonly PluginRegistry _registry = new();
    private readonly PluginLoader _loader;
    private readonly IPluginPermissionValidator _permissionValidator;
    private readonly LoggerFactory _loggerFactory;
    private readonly PluginLifecycleManager _sut;

    public PluginLifecycleManagerLoggingTests()
    {
        _loader = new PluginLoader(_registry, Options.Create(new PluginOptions()), NullLogger<PluginLoader>.Instance);
        _permissionValidator = Substitute.For<IPluginPermissionValidator>();
        IOptions<PluginOptions> options = Options.Create(new PluginOptions
        {
            PluginsDirectory = Path.Combine(Path.GetTempPath(), "wallow-test-plugins")
        });

#pragma warning disable CA2000 // LoggerFactory takes ownership of the provider and disposes it
        _loggerFactory = new LoggerFactory([new ListLoggerProvider()]);
#pragma warning restore CA2000
        ILogger<PluginLifecycleManager> logger = _loggerFactory.CreateLogger<PluginLifecycleManager>();

        _sut = new PluginLifecycleManager(
            _registry,
            _loader,
            _permissionValidator,
            options,
            logger);
    }

    public void Dispose() => _loggerFactory.Dispose();

    private static PluginManifest CreateManifest(
        string id = "test-plugin",
        IReadOnlyList<string>? requiredPermissions = null) =>
        new(id, "Test Plugin", "1.0.0", "A test plugin", "Test Author", "1.0.0",
            "TestPlugin.dll", [], requiredPermissions ?? [], []);

    [Fact]
    public async Task DiscoverPluginsAsync_WithManifests_LogsDiscoveryMessages()
    {
        string pluginsDir = Path.Combine(Path.GetTempPath(), $"wallow-log-discover-{Guid.NewGuid()}");
        string pluginDir = Path.Combine(pluginsDir, "log-plugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            string manifestJson = """
                {
                    "Id": "log-plugin",
                    "Name": "Log Plugin",
                    "Version": "1.0.0",
                    "Description": "Plugin for log tests",
                    "Author": "Tester",
                    "MinWallowVersion": "1.0.0",
                    "EntryAssembly": "LogPlugin.dll",
                    "Dependencies": [],
                    "RequiredPermissions": [],
                    "ExportedServices": []
                }
                """;
            await File.WriteAllTextAsync(Path.Combine(pluginDir, "wallow-plugin.json"), manifestJson);

            IReadOnlyList<PluginManifest> result = await _sut.DiscoverPluginsAsync(pluginsDir);

            result.Should().ContainSingle();
            result[0].Id.Should().Be("log-plugin");
        }
        finally
        {
            Directory.Delete(pluginsDir, recursive: true);
        }
    }

    [Fact]
    public async Task DiscoverPluginsAsync_EmptyDirectory_LogsZeroCount()
    {
        string pluginsDir = Path.Combine(Path.GetTempPath(), $"wallow-log-empty-{Guid.NewGuid()}");
        Directory.CreateDirectory(pluginsDir);

        try
        {
            IReadOnlyList<PluginManifest> result = await _sut.DiscoverPluginsAsync(pluginsDir);

            result.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(pluginsDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnablePluginAsync_WithLogging_LogsEnableMessages()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        await _sut.EnablePluginAsync(manifest.Id, services, config);

        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Enabled);
    }

    [Fact]
    public async Task InitializePluginAsync_WithLogging_LogsInitializeMessages()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Enabled);

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ILogger<PluginContext>>(NullLogger<PluginContext>.Instance);
        ServiceProvider sp = services.BuildServiceProvider();

        await _sut.InitializePluginAsync(manifest.Id, sp);

        await plugin.Received(1).InitializeAsync(Arg.Any<PluginContext>());
    }

    [Fact]
    public async Task DisablePluginAsync_WithLogging_LogsDisableMessages()
    {
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Enabled);

        await _sut.DisablePluginAsync(manifest.Id);

        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Disabled);
    }

    [Fact]
    public async Task DiscoverPluginsAsync_MultiplePlugins_LogsEachPluginDiscovery()
    {
        string pluginsDir = Path.Combine(Path.GetTempPath(), $"wallow-log-multi-{Guid.NewGuid()}");

        string pluginADir = Path.Combine(pluginsDir, "log-a");
        string pluginBDir = Path.Combine(pluginsDir, "log-b");
        Directory.CreateDirectory(pluginADir);
        Directory.CreateDirectory(pluginBDir);

        try
        {
            string manifestA = """
                {
                    "Id": "log-a",
                    "Name": "Log A",
                    "Version": "1.0.0",
                    "Description": "A",
                    "Author": "Tester",
                    "MinWallowVersion": "1.0.0",
                    "EntryAssembly": "A.dll",
                    "Dependencies": [],
                    "RequiredPermissions": [],
                    "ExportedServices": []
                }
                """;
            string manifestB = """
                {
                    "Id": "log-b",
                    "Name": "Log B",
                    "Version": "2.0.0",
                    "Description": "B",
                    "Author": "Tester",
                    "MinWallowVersion": "1.0.0",
                    "EntryAssembly": "B.dll",
                    "Dependencies": [],
                    "RequiredPermissions": [],
                    "ExportedServices": []
                }
                """;
            await File.WriteAllTextAsync(Path.Combine(pluginADir, "wallow-plugin.json"), manifestA);
            await File.WriteAllTextAsync(Path.Combine(pluginBDir, "wallow-plugin.json"), manifestB);

            IReadOnlyList<PluginManifest> result = await _sut.DiscoverPluginsAsync(pluginsDir);

            result.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(pluginsDir, recursive: true);
        }
    }

    [Fact]
    public async Task FullLifecycle_WithLogging_ExercisesAllLogMessages()
    {
        // This test exercises the full Enable -> Initialize -> Disable lifecycle
        // with a real logger to cover all LoggerMessage generated code paths
        PluginManifest manifest = CreateManifest();
        _registry.Register(manifest);
        IWallowPlugin plugin = Substitute.For<IWallowPlugin>();
        PluginAssemblyLoadContext loadContext = new(Path.GetTempPath());
        _registry.SetInstance(manifest.Id, plugin, loadContext);
        _registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();
        services.AddSingleton(config);
        services.AddSingleton<ILogger<PluginContext>>(NullLogger<PluginContext>.Instance);
        ServiceProvider sp = services.BuildServiceProvider();

        await _sut.EnablePluginAsync(manifest.Id, services, config);
        await _sut.InitializePluginAsync(manifest.Id, sp);
        await _sut.DisablePluginAsync(manifest.Id);

        _registry.GetEntry(manifest.Id)!.State.Should().Be(PluginLifecycleState.Disabled);
    }
}

internal sealed class ListLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ListLogger();

    public void Dispose() { }

    private sealed class ListLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Force formatter execution to exercise LoggerMessage generated code
            _ = formatter(state, exception);
        }
    }
}
