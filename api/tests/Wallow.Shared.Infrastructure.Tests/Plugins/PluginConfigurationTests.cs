using Wallow.Shared.Infrastructure.Plugins;

namespace Wallow.Shared.Infrastructure.Tests.Plugins;

public class PluginConfigurationTests
{
    [Fact]
    public void SectionName_IsPlugins()
    {
        PluginOptions.SectionName.Should().Be("Plugins");
    }

    [Fact]
    public void Defaults_PluginsDirectory_IsPluginsSlash()
    {
        PluginOptions options = new();

        options.PluginsDirectory.Should().Be("plugins/");
    }

    [Fact]
    public void Defaults_AutoDiscover_IsTrue()
    {
        PluginOptions options = new();

        options.AutoDiscover.Should().BeTrue();
    }

    [Fact]
    public void Defaults_AutoEnable_IsFalse()
    {
        PluginOptions options = new();

        options.AutoEnable.Should().BeFalse();
    }

    [Fact]
    public void Defaults_Permissions_IsEmptyDictionary()
    {
        PluginOptions options = new();

        options.Permissions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SetProperties_RetainsValues()
    {
        PluginOptions options = new()
        {
            PluginsDirectory = "/custom/path/",
            AutoDiscover = false,
            AutoEnable = true,
            Permissions = new Dictionary<string, List<string>>
            {
                ["my-plugin"] = ["read", "write"]
            }
        };

        options.PluginsDirectory.Should().Be("/custom/path/");
        options.AutoDiscover.Should().BeFalse();
        options.AutoEnable.Should().BeTrue();
        options.Permissions.Should().ContainKey("my-plugin");
        options.Permissions["my-plugin"].Should().BeEquivalentTo(["read", "write"]);
    }
}
