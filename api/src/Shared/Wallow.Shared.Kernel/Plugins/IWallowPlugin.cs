using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Shared.Kernel.Plugins;

public interface IWallowPlugin
{
    PluginManifest Manifest { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);

    Task InitializeAsync(PluginContext context);

    Task ShutdownAsync();
}
