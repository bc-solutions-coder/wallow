namespace Foundry.Shared.Infrastructure.Plugins;

public sealed class PluginLoadException : Exception
{
    public string PluginId { get; } = string.Empty;

    public PluginLoadException() { }

    public PluginLoadException(string message) : base(message) { }

    public PluginLoadException(string message, Exception innerException) : base(message, innerException) { }

    public PluginLoadException(string pluginId, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        PluginId = pluginId;
    }
}
