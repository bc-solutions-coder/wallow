using Wallow.Shared.Kernel.Errors;

namespace Wallow.Branding.Domain.Errors;

/// <summary>
/// The error catalog the Branding module owns. Registered by <c>AddBrandingModule</c>.
/// </summary>
public static class BrandingErrors
{
    public static readonly ErrorCatalogEntry ClientBrandingClientIdRequired = new(
        "Branding.ClientBrandingClientIdRequired", ErrorKind.BusinessRule, "Client ID cannot be empty");

    public static readonly ErrorCatalogEntry ClientBrandingDisplayNameRequired = new(
        "Branding.ClientBrandingDisplayNameRequired", ErrorKind.BusinessRule, "Display name cannot be empty");
}
