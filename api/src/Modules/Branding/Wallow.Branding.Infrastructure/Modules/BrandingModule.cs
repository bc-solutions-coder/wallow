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
/// Branding deliberately uses service-from-controller rather than CQRS, so it has no Wolverine
/// command handlers. Its only handlers consume cross-module integration events (a deleted client
/// takes its branding with it), which is why the Infrastructure assembly is declared below; the
/// Application anchor is an interface rather than a handler for exactly that reason.
/// </summary>
public sealed class BrandingModule : IWallowModule
{
    /// <summary>
    /// The one place this module's Postgres schema name is written. <see cref="SchemaName"/>, the
    /// module's <c>HasDefaultSchema</c> and every host's <c>MigrationsHistoryTable</c> all resolve
    /// to this constant, so the compiler rather than a convention is what keeps them equal.
    /// </summary>
    /// <remarks>
    /// Internal on purpose: every consumer — this class, the module's <c>DbContext</c> and its
    /// infrastructure extensions — lives in <c>Wallow.Branding.Infrastructure</c>. Keeping it
    /// internal stops any other assembly, another module included, from taking a compile-time
    /// dependency on this module's schema name.
    /// </remarks>
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
