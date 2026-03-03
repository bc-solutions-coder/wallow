namespace Foundry.Shared.Infrastructure.Plugins;

public sealed class PluginOptions
{
    public const string SectionName = "Plugins";

    public string PluginsDirectory { get; set; } = "plugins/";
    public bool AutoDiscover { get; set; } = true;
    public bool AutoEnable { get; set; }
    public Dictionary<string, List<string>> Permissions { get; set; } = [];
}
