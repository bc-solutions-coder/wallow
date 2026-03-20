using Wallow.Shared.Kernel.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Shared.Infrastructure.Tests.Plugins;

/// <summary>
/// A concrete IWallowPlugin whose manifest ID is "mismatched-plugin-id".
/// Used by PluginLoaderTests to verify the manifest ID mismatch error path.
/// </summary>
public class MismatchedManifestPlugin : IWallowPlugin
{
    public const string PluginId = "mismatched-plugin-id";

    public PluginManifest Manifest { get; } = new(
        PluginId, "Mismatched Plugin", "1.0.0", "Test plugin with wrong ID",
        "Test", "1.0.0", "Test.dll", [], [], []);

    public void AddServices(IServiceCollection services, IConfiguration configuration) { }

    public Task InitializeAsync(PluginContext context) => Task.CompletedTask;

    public Task ShutdownAsync() => Task.CompletedTask;
}
