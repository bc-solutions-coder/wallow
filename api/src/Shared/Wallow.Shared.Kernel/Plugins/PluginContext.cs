using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Wallow.Shared.Kernel.Plugins;

public sealed class PluginContext(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<PluginContext> logger)
{
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
    public IConfiguration Configuration { get; } = configuration;
    public ILogger<PluginContext> Logger { get; } = logger;
}
