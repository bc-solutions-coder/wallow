using Wallow.Shared.Kernel.Errors;

namespace Wallow.ApiKeys.Domain.Errors;

/// <summary>
/// The error catalog the ApiKeys module owns. Registered by <c>AddApiKeysModule</c>.
/// </summary>
public static class ApiKeysErrors
{
    public static readonly ErrorCatalogEntry ServiceAccountIdRequired = new(
        "ApiKeys.ServiceAccountIdRequired", ErrorKind.BusinessRule, "Service account ID cannot be empty");

    public static readonly ErrorCatalogEntry HashedKeyRequired = new(
        "ApiKeys.HashedKeyRequired", ErrorKind.BusinessRule, "Hashed key cannot be empty");

    public static readonly ErrorCatalogEntry ApiKeyDisplayNameRequired = new(
        "ApiKeys.ApiKeyDisplayNameRequired", ErrorKind.BusinessRule, "API key display name cannot be empty");

    public static readonly ErrorCatalogEntry ApiKeyAlreadyRevoked = new(
        "ApiKeys.ApiKeyAlreadyRevoked", ErrorKind.BusinessRule, "API key is already revoked");
}
