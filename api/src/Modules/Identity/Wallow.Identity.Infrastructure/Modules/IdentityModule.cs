using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Identity.Application.Commands.CreateServiceAccount;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.Identity.Infrastructure.Modules;

/// <summary>
/// Identity is the one module the registry cannot make optional, and the only one that currently
/// has handlers in both its Application and its Infrastructure assembly.
/// </summary>
public sealed class IdentityModule : IWallowModule
{
    public string Name => "Identity";

    public bool IsCore => true;

    public IEnumerable<Assembly> HandlerAssemblies =>
    [
        typeof(CreateServiceAccountHandler).Assembly,
        typeof(IdentityModule).Assembly,
    ];

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddIdentityModule(configuration, environment);
    }
}
