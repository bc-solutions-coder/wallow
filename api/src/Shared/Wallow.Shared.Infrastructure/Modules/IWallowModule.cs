using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wallow.Shared.Infrastructure.Modules;

/// <summary>
/// A Wallow module, described to the host well enough that the host never has to guess.
/// <para>
/// Handler discovery used to walk <c>AppDomain.CurrentDomain.GetAssemblies()</c> and keep every
/// assembly whose name started with "Wallow.". That made the set of discovered Wolverine handlers a
/// function of which types earlier code happened to touch first: a module's <c>.Application</c>
/// assembly is loaded only because its <c>AddXModule</c> body referenced a type in it, so a refactor
/// that moved that type to <c>.Domain</c> would silently drop every handler in the assembly — no
/// error, just fewer chains. <see cref="HandlerAssemblies"/> states the set instead of inferring it.
/// </para>
/// </summary>
public interface IWallowModule
{
    /// <summary>
    /// Gets the module name, which is also its feature-flag suffix: "Storage" is gated by
    /// <c>FeatureManagement:Modules.Storage</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the module is a required platform dependency. A core module
    /// ignores its feature flag — it is always registered, and it migrates first.
    /// </summary>
    bool IsCore { get; }

    /// <summary>
    /// Gets every assembly the host should hand to Wolverine's handler discovery and to the AsyncAPI
    /// document generator on this module's behalf.
    /// <para>
    /// Declare BOTH the module's <c>.Application</c> and its <c>.Infrastructure</c> assembly, always,
    /// even when one of them currently holds no handlers. Declaring an empty assembly costs nothing —
    /// Wolverine simply finds no handlers in it — whereas omitting one means the first handler added
    /// there is silently never discovered. Only Identity has an Infrastructure handler today
    /// (<c>SessionEvictedHandler</c>); listing the assembly everywhere is what stops that from being a
    /// fact anyone has to remember. Anchor the Infrastructure entry on the module type itself
    /// (<c>typeof(XModule).Assembly</c>) so it cannot rot when the anchor type moves.
    /// </para>
    /// </summary>
    IEnumerable<Assembly> HandlerAssemblies { get; }

    /// <summary>
    /// Registers the module's services.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="environment">
    /// The host environment. Only Identity reads it; the other six accept it so that the one module
    /// the registry cannot make optional still fits the interface.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment);
}
