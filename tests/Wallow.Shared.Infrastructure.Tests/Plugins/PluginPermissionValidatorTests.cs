using Wallow.Shared.Infrastructure.Plugins;
using Wallow.Shared.Kernel.Plugins;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Wallow.Shared.Infrastructure.Tests.Plugins;

public class PluginPermissionValidatorTests
{
    private readonly PluginRegistry _registry = new();
    private readonly PluginOptions _pluginOptions = new();
    private readonly PluginPermissionValidator _sut;

    public PluginPermissionValidatorTests()
    {
        IOptions<PluginOptions> options = Substitute.For<IOptions<PluginOptions>>();
        options.Value.Returns(_pluginOptions);
        _sut = new PluginPermissionValidator(_registry, options);
    }

    private static PluginManifest CreateManifest(
        string id = "test-plugin",
        IReadOnlyList<string>? requiredPermissions = null) =>
        new(id, "Test Plugin", "1.0.0", "A test plugin", "Test Author", "1.0.0",
            "TestPlugin.dll", [], requiredPermissions ?? [], []);

    private void RegisterPluginWithPermissions(
        string pluginId,
        List<string> manifestPermissions,
        List<string> configuredPermissions)
    {
        _registry.Register(CreateManifest(pluginId, manifestPermissions));
        _pluginOptions.Permissions[pluginId] = configuredPermissions;
    }

    // --- HasPermission ---

    [Fact]
    public void HasPermission_PermissionGrantedAndRequested_ReturnsTrue()
    {
        RegisterPluginWithPermissions("test-plugin",
            ["storage.read", "storage.write"],
            ["storage.read", "storage.write"]);

        bool result = _sut.HasPermission("test-plugin", "storage.read");

        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_PermissionNotConfigured_ReturnsFalse()
    {
        RegisterPluginWithPermissions("test-plugin",
            ["storage.read", "storage.write"],
            ["storage.read"]);

        bool result = _sut.HasPermission("test-plugin", "storage.write");

        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_PermissionNotInManifest_ReturnsFalse()
    {
        RegisterPluginWithPermissions("test-plugin",
            ["storage.read"],
            ["storage.read", "storage.write"]);

        bool result = _sut.HasPermission("test-plugin", "storage.write");

        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_NoConfiguredPermissions_ReturnsFalse()
    {
        _registry.Register(CreateManifest("test-plugin", ["storage.read"]));

        bool result = _sut.HasPermission("test-plugin", "storage.read");

        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_NoRegistryEntry_ReturnsFalse()
    {
        _pluginOptions.Permissions["unknown-plugin"] = ["storage.read"];

        bool result = _sut.HasPermission("unknown-plugin", "storage.read");

        result.Should().BeFalse();
    }

    // --- GetGrantedPermissions ---

    [Fact]
    public void GetGrantedPermissions_IntersectsManifestAndConfigured_ReturnsOnlyCommon()
    {
        RegisterPluginWithPermissions("test-plugin",
            ["storage.read", "storage.write", "email.send"],
            ["storage.read", "email.send", "admin.manage"]);

        IReadOnlyList<string> result = _sut.GetGrantedPermissions("test-plugin");

        result.Should().BeEquivalentTo(["storage.read", "email.send"]);
    }

    [Fact]
    public void GetGrantedPermissions_NoRegistryEntry_ReturnsEmpty()
    {
        _pluginOptions.Permissions["unknown-plugin"] = ["storage.read"];

        IReadOnlyList<string> result = _sut.GetGrantedPermissions("unknown-plugin");

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetGrantedPermissions_NoConfiguredPermissions_ReturnsEmpty()
    {
        _registry.Register(CreateManifest("test-plugin", ["storage.read"]));

        IReadOnlyList<string> result = _sut.GetGrantedPermissions("test-plugin");

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetGrantedPermissions_NoOverlap_ReturnsEmpty()
    {
        RegisterPluginWithPermissions("test-plugin",
            ["storage.read"],
            ["email.send"]);

        IReadOnlyList<string> result = _sut.GetGrantedPermissions("test-plugin");

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetGrantedPermissions_AllMatch_ReturnsAll()
    {
        RegisterPluginWithPermissions("test-plugin",
            ["storage.read", "email.send"],
            ["storage.read", "email.send"]);

        IReadOnlyList<string> result = _sut.GetGrantedPermissions("test-plugin");

        result.Should().BeEquivalentTo(["storage.read", "email.send"]);
    }
}
