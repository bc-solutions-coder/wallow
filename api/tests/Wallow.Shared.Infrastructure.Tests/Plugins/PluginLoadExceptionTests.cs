using Wallow.Shared.Infrastructure.Plugins;

namespace Wallow.Shared.Infrastructure.Tests.Plugins;

public class PluginLoadExceptionTests
{
    [Fact]
    public void Constructor_Parameterless_CreatesException()
    {
        PluginLoadException exception = new();

        exception.Message.Should().NotBeNullOrEmpty();
        exception.InnerException.Should().BeNull();
        exception.PluginId.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithMessage_PropagatesMessage()
    {
        string message = "Something went wrong loading plugin";

        PluginLoadException exception = new(message);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
        exception.PluginId.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_PropagatesBoth()
    {
        string message = "Plugin load failed";
        InvalidOperationException inner = new("Root cause");

        PluginLoadException exception = new(message, inner);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(inner);
        exception.PluginId.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithPluginIdAndMessage_SetsPluginId()
    {
        string pluginId = "my-plugin";
        string message = "Failed to load my-plugin";

        PluginLoadException exception = new(pluginId, message);

        exception.PluginId.Should().Be(pluginId);
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithPluginIdMessageAndInnerException_SetsAll()
    {
        string pluginId = "my-plugin";
        string message = "Failed to load";
        FileNotFoundException inner = new("assembly.dll not found");

        PluginLoadException exception = new(pluginId, message, inner);

        exception.PluginId.Should().Be(pluginId);
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void IsException_DerivesFromSystemException()
    {
        PluginLoadException exception = new("error");

        exception.Should().BeAssignableTo<Exception>();
    }
}
