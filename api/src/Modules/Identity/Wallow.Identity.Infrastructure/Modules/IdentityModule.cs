using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.Identity.Infrastructure.Modules;

/// <summary>
/// Required Identity module with handlers in both Application and Infrastructure.
/// </summary>
public sealed class IdentityModule : IWallowModule
{
    /// <summary>
    /// Schema shared by the model and migration history table.
    /// The seeder accesses this internal constant through its InternalsVisibleTo grant.
    /// </summary>
    internal const string Schema = "identity";

    public string Name => "Identity";

    public bool IsCore => true;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(BootstrapAdminHandler).Assembly,
        typeof(IdentityModule).Assembly,
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(IdentityDbContext)];

    public string SchemaName => Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddIdentityModule(configuration, environment);
    }
}
