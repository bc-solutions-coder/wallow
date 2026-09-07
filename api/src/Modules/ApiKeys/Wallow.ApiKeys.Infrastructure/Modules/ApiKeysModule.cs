using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Infrastructure.Extensions;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.ApiKeys.Infrastructure.Modules;

/// <summary>
/// Uses controller services for key management and Wolverine for integration events.
/// Both Application and Infrastructure assemblies remain in handler discovery.
/// </summary>
public sealed class ApiKeysModule : IWallowModule
{
    /// <summary>
    /// Schema shared by the DbContext and migration-history configuration.
    /// Kept internal to prevent cross-module dependencies on the schema constant.
    /// </summary>
    internal const string Schema = "apikeys";

    public string Name => "ApiKeys";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(IApiKeyRepository).Assembly,
        typeof(ApiKeysModule).Assembly,
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(ApiKeysDbContext)];

    public string SchemaName => Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddApiKeysModule(configuration);
    }
}
