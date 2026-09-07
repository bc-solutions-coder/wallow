using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Infrastructure.Extensions;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.Branding.Infrastructure.Modules;

/// <summary>
/// Uses controller services for branding writes and Wolverine handlers for integration events.
/// Both Application and Infrastructure assemblies remain in handler discovery.
/// </summary>
public sealed class BrandingModule : IWallowModule
{
    /// <summary>
    /// Schema shared by the DbContext and migration-history configuration.
    /// Kept internal to prevent cross-module dependencies on the schema constant.
    /// </summary>
    internal const string Schema = "branding";

    public string Name => "Branding";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(IClientBrandingRepository).Assembly,
        typeof(BrandingModule).Assembly,
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(BrandingDbContext)];

    public string SchemaName => Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddBrandingModule(configuration);
    }
}
