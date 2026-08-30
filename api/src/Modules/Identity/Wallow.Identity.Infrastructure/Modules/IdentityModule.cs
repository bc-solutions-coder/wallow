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
/// Identity is the one module the registry cannot make optional, and the only one that currently
/// has handlers in both its Application and its Infrastructure assembly.
/// </summary>
public sealed class IdentityModule : IWallowModule
{
    /// <summary>
    /// The one place this module's Postgres schema name is written. <see cref="SchemaName"/>, the
    /// module's <c>HasDefaultSchema</c> and every host's <c>MigrationsHistoryTable</c> all resolve
    /// to this constant, so the compiler rather than a convention is what keeps them equal.
    /// </summary>
    /// <remarks>
    /// Internal, like every other module's. <c>Wallow.SeederService</c> is a third host, in its own
    /// assembly, that builds this module's <c>DbContext</c> and would otherwise have to hand-type
    /// the string again, so it — and only it — gets an <c>InternalsVisibleTo</c> grant in
    /// <c>Wallow.Identity.Infrastructure.csproj</c>. That keeps the reach one named assembly wide
    /// instead of exposing the schema name to everything that references this module.
    /// </remarks>
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
