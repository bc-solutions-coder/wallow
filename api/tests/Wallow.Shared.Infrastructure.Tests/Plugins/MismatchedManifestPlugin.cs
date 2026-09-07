using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Kernel.Plugins;

namespace Wallow.Shared.Infrastructure.Tests.Plugins;

/// <summary>
/// Plugin fixture used for loading and manifest-ID validation.
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
