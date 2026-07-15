using System.Text.Json;
using Wallow.Shared.Kernel.Plugins;

namespace Wallow.Shared.Infrastructure.Plugins;

public static class PluginManifestLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<PluginManifest> LoadFromDirectory(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
        {
            return [];
        }

        List<PluginManifest> manifests = [];

        foreach (string dir in Directory.GetDirectories(pluginsPath))
        {
            string manifestPath = Path.Combine(dir, "wallow-plugin.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            string json = File.ReadAllText(manifestPath);
            PluginManifest manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions)
                ?? throw new PluginLoadException(
                    Path.GetFileName(dir),
                    $"Failed to deserialize manifest at '{manifestPath}'.");

            ValidateManifest(manifest, manifestPath);
            manifests.Add(manifest);
        }

        return manifests;
    }

    private static void ValidateManifest(PluginManifest manifest, string path)
    {
        List<string> missing = [];

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            missing.Add(nameof(manifest.Id));
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            missing.Add(nameof(manifest.Name));
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            missing.Add(nameof(manifest.Version));
        }

        if (string.IsNullOrWhiteSpace(manifest.Author))
        {
            missing.Add(nameof(manifest.Author));
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            missing.Add(nameof(manifest.EntryAssembly));
        }

        if (missing.Count > 0)
        {
            throw new PluginLoadException(
                manifest.Id ?? Path.GetFileName(Path.GetDirectoryName(path)) ?? "unknown",
                $"Manifest at '{path}' is missing required fields: {string.Join(", ", missing)}.");
        }
    }
}
