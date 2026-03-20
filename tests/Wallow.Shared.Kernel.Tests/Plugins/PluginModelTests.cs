using Wallow.Shared.Kernel.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Wallow.Shared.Kernel.Tests.Plugins;

public class PluginManifestTests
{
    [Fact]
    public void Constructor_WithValidData_SetsAllProperties()
    {
        List<PluginDependency> deps = [new PluginDependency("dep-1", ">=1.0.0")];
        List<string> perms = [PluginPermission.BillingRead];
        List<string> services = ["IMyService"];

        PluginManifest manifest = new(
            "my-plugin", "My Plugin", "1.0.0", "A test plugin",
            "Author", "1.0.0", "MyPlugin.dll", deps, perms, services);

        manifest.Id.Should().Be("my-plugin");
        manifest.Name.Should().Be("My Plugin");
        manifest.Version.Should().Be("1.0.0");
        manifest.Description.Should().Be("A test plugin");
        manifest.Author.Should().Be("Author");
        manifest.MinWallowVersion.Should().Be("1.0.0");
        manifest.EntryAssembly.Should().Be("MyPlugin.dll");
        manifest.Dependencies.Should().ContainSingle().Which.Id.Should().Be("dep-1");
        manifest.RequiredPermissions.Should().ContainSingle().Which.Should().Be(PluginPermission.BillingRead);
        manifest.ExportedServices.Should().ContainSingle().Which.Should().Be("IMyService");
    }

    [Fact]
    public void Constructor_WithEmptyCollections_SetsEmptyLists()
    {
        PluginManifest manifest = new(
            "id", "Name", "1.0.0", "Desc", "Auth", "1.0.0", "Entry.dll",
            [], [], []);

        manifest.Dependencies.Should().BeEmpty();
        manifest.RequiredPermissions.Should().BeEmpty();
        manifest.ExportedServices.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        PluginDependency[] deps = [];
        string[] perms = [];
        string[] services = [];

        PluginManifest manifest1 = new("id", "Name", "1.0.0", "Desc", "Auth", "1.0.0", "Entry.dll", deps, perms, services);
        PluginManifest manifest2 = new("id", "Name", "1.0.0", "Desc", "Auth", "1.0.0", "Entry.dll", deps, perms, services);

        manifest1.Should().Be(manifest2);
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        PluginDependency[] deps = [];
        string[] perms = [];
        string[] services = [];

        PluginManifest manifest1 = new("id-1", "Name", "1.0.0", "Desc", "Auth", "1.0.0", "Entry.dll", deps, perms, services);
        PluginManifest manifest2 = new("id-2", "Name", "1.0.0", "Desc", "Auth", "1.0.0", "Entry.dll", deps, perms, services);

        manifest1.Should().NotBe(manifest2);
    }

    [Fact]
    public void With_ChangedProperty_ReturnsNewInstance()
    {
        PluginManifest original = new("id", "Name", "1.0.0", "Desc", "Auth", "1.0.0", "Entry.dll", [], [], []);

        PluginManifest updated = original with { Version = "2.0.0" };

        updated.Version.Should().Be("2.0.0");
        original.Version.Should().Be("1.0.0");
    }
}

public class PluginDependencyTests
{
    [Fact]
    public void Constructor_WithValidData_SetsProperties()
    {
        PluginDependency dep = new("dep-id", ">=1.0.0");

        dep.Id.Should().Be("dep-id");
        dep.VersionRange.Should().Be(">=1.0.0");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        PluginDependency dep1 = new("dep-id", ">=1.0.0");
        PluginDependency dep2 = new("dep-id", ">=1.0.0");

        dep1.Should().Be(dep2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        PluginDependency dep1 = new("dep-1", ">=1.0.0");
        PluginDependency dep2 = new("dep-2", ">=1.0.0");

        dep1.Should().NotBe(dep2);
    }
}

public class PluginContextTests
{
    [Fact]
    public void Constructor_WithDependencies_SetsAllProperties()
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        IConfiguration configuration = Substitute.For<IConfiguration>();
        ILogger<PluginContext> logger = Substitute.For<ILogger<PluginContext>>();

        PluginContext context = new(serviceProvider, configuration, logger);

        context.ServiceProvider.Should().BeSameAs(serviceProvider);
        context.Configuration.Should().BeSameAs(configuration);
        context.Logger.Should().BeSameAs(logger);
    }
}

public class PluginLifecycleStateTests
{
    [Theory]
    [InlineData(PluginLifecycleState.Discovered, 0)]
    [InlineData(PluginLifecycleState.Installed, 1)]
    [InlineData(PluginLifecycleState.Enabled, 2)]
    [InlineData(PluginLifecycleState.Disabled, 3)]
    [InlineData(PluginLifecycleState.Uninstalled, 4)]
    public void EnumValues_HaveExpectedOrdinals(PluginLifecycleState state, int expected)
    {
        ((int)state).Should().Be(expected);
    }

    [Fact]
    public void AllValues_ContainsFiveStates()
    {
        PluginLifecycleState[] values = Enum.GetValues<PluginLifecycleState>();

        values.Should().HaveCount(5);
    }
}

public class PluginPermissionTests
{
    [Theory]
    [InlineData(PluginPermission.BillingRead, "billing:read")]
    [InlineData(PluginPermission.NotificationsSend, "notifications:send")]
    [InlineData(PluginPermission.StorageRead, "storage:read")]
    [InlineData(PluginPermission.StorageWrite, "storage:write")]
    [InlineData(PluginPermission.IdentityRead, "identity:read")]
    public void Constants_HaveExpectedValues(string actual, string expected)
    {
        actual.Should().Be(expected);
    }

    [Fact]
    public void Constants_FollowModuleActionPattern()
    {
        string[] allPermissions =
        [
            PluginPermission.BillingRead,
            PluginPermission.NotificationsSend,
            PluginPermission.StorageRead,
            PluginPermission.StorageWrite,
            PluginPermission.IdentityRead,
        ];

        allPermissions.Should().AllSatisfy(p => p.Should().MatchRegex(@"^[a-z]+:[a-z]+$"));
    }
}
