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

    /// <summary>The presented API key is malformed, unknown, expired, or revoked.</summary>
    public static readonly ErrorCatalogEntry ApiKeyInvalid = new(
        "ApiKeys.Invalid", ErrorKind.Unauthenticated, "The API key is not valid.");

    /// <summary>API keys are scoped to an organization; the caller has none selected.</summary>
    public static readonly ErrorCatalogEntry OrganizationRequired = new(
        "ApiKeys.OrganizationRequired", ErrorKind.Validation, "You must belong to an organization to create API keys.");

    /// <summary>A requested scope grants more than the caller currently holds.</summary>
    public static readonly ErrorCatalogEntry ScopeExceedsPermissions = new(
        "ApiKeys.ScopeExceedsPermissions", ErrorKind.Forbidden, "A requested scope exceeds your current permissions.");

    /// <summary>The per-user key quota is spent.</summary>
    public static readonly ErrorCatalogEntry LimitReached = new(
        "ApiKeys.LimitReached", ErrorKind.BusinessRule, "You have reached the maximum number of API keys. Revoke an existing key before creating a new one.");

    /// <summary>The key store could not persist a new key.</summary>
    public static readonly ErrorCatalogEntry CreateFailed = new(
        "ApiKeys.CreateFailed", ErrorKind.Failure, "The API key could not be created.");

    /// <summary>No key with that id belongs to the caller.</summary>
    public static readonly ErrorCatalogEntry ApiKeyNotFound = new(
        "ApiKeys.NotFound", ErrorKind.NotFound, "The API key does not exist or does not belong to you.");
}
