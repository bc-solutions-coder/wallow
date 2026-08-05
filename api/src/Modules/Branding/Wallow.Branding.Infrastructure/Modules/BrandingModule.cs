using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Infrastructure.Extensions;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.Branding.Infrastructure.Modules;

/// <summary>
/// Branding deliberately uses service-from-controller rather than CQRS, so it has no Wolverine
/// handlers at all. It declares its Application assembly anyway: the anchor type below is an
/// interface rather than a handler for exactly that reason, and declaring the assembly is what makes
/// the first handler someone adds here get discovered instead of silently doing nothing.
/// </summary>
public sealed class BrandingModule : IWallowModule
{
    public string Name => "Branding";

    public bool IsCore => false;

    public IEnumerable<Assembly> HandlerAssemblies =>
    [
        typeof(IClientBrandingRepository).Assembly,
        typeof(BrandingModule).Assembly,
    ];

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddBrandingModule(configuration);
    }
}
