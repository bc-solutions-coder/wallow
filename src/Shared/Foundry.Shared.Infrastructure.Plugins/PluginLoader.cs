using System.Reflection;
using System.Security.Cryptography;
using Foundry.Shared.Kernel.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Shared.Infrastructure.Plugins;

public sealed partial class PluginLoader(
    PluginRegistry registry,
    IOptions<PluginOptions> options,
    ILogger<PluginLoader> logger)
{
    public PluginRegistryEntry LoadPlugin(PluginManifest manifest, string pluginsBasePath)
    {
        string pluginDir = Path.Combine(pluginsBasePath, manifest.Id);
        string assemblyPath = Path.GetFullPath(Path.Combine(pluginDir, manifest.EntryAssembly));

        VerifyAssemblyHash(manifest.Id, assemblyPath);

        PluginAssemblyLoadContext loadContext = new PluginAssemblyLoadContext(pluginDir);

        Assembly assembly;
        try
        {
            assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception ex)
        {
            loadContext.Unload();
            throw new PluginLoadException(manifest.Id, $"Failed to load assembly '{manifest.EntryAssembly}'.", ex);
        }

        List<Type> pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IFoundryPlugin).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        if (pluginTypes.Count == 0)
        {
            loadContext.Unload();
            throw new PluginLoadException(manifest.Id, $"No IFoundryPlugin implementation found in '{manifest.EntryAssembly}'.");
        }

        if (pluginTypes.Count > 1)
        {
            loadContext.Unload();
            throw new PluginLoadException(manifest.Id, $"Multiple IFoundryPlugin implementations found in '{manifest.EntryAssembly}'. Expected exactly one.");
        }

        IFoundryPlugin plugin = (IFoundryPlugin)(Activator.CreateInstance(pluginTypes[0])
            ?? throw new PluginLoadException(manifest.Id, $"Failed to create instance of '{pluginTypes[0].FullName}'."));

        if (plugin.Manifest.Id != manifest.Id)
        {
            loadContext.Unload();
            throw new PluginLoadException(manifest.Id, $"Plugin manifest ID mismatch: expected '{manifest.Id}', got '{plugin.Manifest.Id}'.");
        }

        registry.SetInstance(manifest.Id, plugin, loadContext);
        registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        return registry.GetEntry(manifest.Id)
            ?? throw new PluginLoadException(manifest.Id, "Plugin entry not found after registration.");
    }

    private void VerifyAssemblyHash(string pluginId, string assemblyPath)
    {
        Dictionary<string, string> allowedHashes = options.Value.AllowedPluginHashes;

        if (allowedHashes.Count == 0)
        {
            return;
        }

        if (!allowedHashes.TryGetValue(pluginId, out string? expectedHash))
        {
            LogPluginNotInAllowList(logger, pluginId);
            throw new PluginLoadException(pluginId, $"Plugin '{pluginId}' is not in the allowed plugin hashes list.");
        }

        byte[] fileBytes = File.ReadAllBytes(assemblyPath);
        byte[] hashBytes = SHA256.HashData(fileBytes);
        string actualHash = Convert.ToHexStringLower(hashBytes);

        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            LogPluginHashMismatch(logger, pluginId, expectedHash, actualHash);
            throw new PluginLoadException(pluginId,
                $"Plugin '{pluginId}' failed hash verification. Expected '{expectedHash}', got '{actualHash}'.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginId}' rejected: not present in the allow-list")]
    private static partial void LogPluginNotInAllowList(ILogger logger, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginId}' rejected: SHA-256 hash mismatch (expected: {ExpectedHash}, actual: {ActualHash})")]
    private static partial void LogPluginHashMismatch(ILogger logger, string pluginId, string expectedHash, string actualHash);
}
