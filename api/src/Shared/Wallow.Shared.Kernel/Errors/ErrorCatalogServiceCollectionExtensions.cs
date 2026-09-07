using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wallow.Shared.Kernel.Errors;

/// <summary>
/// Registers error catalogs with the host so the API can aggregate them.
/// </summary>
public static class ErrorCatalogServiceCollectionExtensions
{
    /// <summary>
    /// Registers a module's catalog for host aggregation. Reads entries immediately to reject
    /// invalid catalogs during registration; duplicate codes are checked when the aggregate resolves.
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
