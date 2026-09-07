using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wallow.Shared.Infrastructure.Modules;

/// <summary>
/// Host registration, handler discovery and migration metadata.
/// See docs/architecture/module-creation.md, Step 7, for module authoring rules.
/// </summary>
public interface IWallowModule
{
    /// <summary>
    /// Gets the feature-flag suffix: Storage uses <c>FeatureManagement:Modules.Storage</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether this module is always registered regardless of its flag and migrates first.
    /// </summary>
    bool IsCore { get; }

    /// <summary>
    /// Gets the assemblies for Wolverine discovery and AsyncAPI generation. Always include both
    /// the Application assembly and Infrastructure assembly, even when empty. Anchor the latter
    /// on the module type so moving a handler cannot silently remove the assembly from discovery.
    /// </summary>
    IReadOnlyList<Assembly> HandlerAssemblies { get; }

    /// <summary>
    /// Gets the module-owned <see cref="Microsoft.EntityFrameworkCore.DbContext"/> types.
    /// Migration hosts register these without calling <see cref="AddServices"/>.
    /// Host-owned auditing contexts are registered separately.
    /// </summary>
    IReadOnlyList<Type> DbContextTypes { get; }

    /// <summary>
    /// Gets the PostgreSQL schema containing this module's <c>__EFMigrationsHistory</c> table.
    /// </summary>
    string SchemaName { get; }

    /// <summary>
    /// Registers the module's services.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="environment">
    /// The host environment for environment-specific registrations.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment);
}
