namespace Wallow.Shared.Kernel.Errors;

/// <summary>
/// One module's contribution to the aggregated <see cref="ErrorCatalog"/>, added by the
/// module's <c>Add&lt;Module&gt;</c> extension through
/// <see cref="ErrorCatalogServiceCollectionExtensions.AddErrorCatalog"/>.
/// </summary>
/// <param name="CatalogType">The static class whose public static entries form the catalog.</param>
public sealed record ErrorCatalogRegistration(Type CatalogType);
