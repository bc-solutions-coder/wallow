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
/// ApiKeys deliberately uses service-from-controller rather than CQRS, so it has no Wolverine
/// handlers at all. It declares its Application assembly anyway: the anchor type below is an
/// interface rather than a handler for exactly that reason, and declaring the assembly is what makes
/// the first handler someone adds here get discovered instead of silently doing nothing.
/// </summary>
public sealed class ApiKeysModule : IWallowModule
{
    /// <summary>
    /// The one place this module's Postgres schema name is written. <see cref="SchemaName"/>, the
    /// module's <c>HasDefaultSchema</c> and every host's <c>MigrationsHistoryTable</c> all resolve
    /// to this constant, so the compiler rather than a convention is what keeps them equal.
    /// </summary>
    /// <remarks>
    /// Internal on purpose: every consumer — this class, the module's <c>DbContext</c> and its
    /// infrastructure extensions — lives in <c>Wallow.ApiKeys.Infrastructure</c>. Keeping it
    /// internal stops any other assembly, another module included, from taking a compile-time
    /// dependency on this module's schema name.
    /// </remarks>
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
