using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wallow.Shared.Kernel.Errors;

/// <summary>
/// Registers error catalogs with the host so the API can aggregate them.
/// </summary>
public static class ErrorCatalogServiceCollectionExtensions
{
    /// <summary>
    /// Contributes a module's static error catalog. Call it from the module's
    /// <c>Add&lt;Module&gt;</c> extension; the host resolves the aggregated
    /// <see cref="ErrorCatalog"/> from every registration. The catalog is read eagerly so a
    /// malformed one fails at registration rather than at the first response.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="catalogType">The static class whose public static entries form the catalog.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddErrorCatalog(this IServiceCollection services, Type catalogType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalogType);

        _ = ErrorCatalog.EntriesOf(catalogType);

        services.AddSingleton(new ErrorCatalogRegistration(catalogType));
        services.TryAddSingleton(provider => ErrorCatalog.Aggregate(
            provider.GetServices<ErrorCatalogRegistration>().Select(registration => registration.CatalogType)));

        return services;
    }
}
