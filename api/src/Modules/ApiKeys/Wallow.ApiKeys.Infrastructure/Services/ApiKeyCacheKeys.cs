namespace Wallow.ApiKeys.Infrastructure.Services;

/// <summary>
/// Valkey cache names derived from database key hashes, domain IDs and owner IDs.
/// Creation, validation and revocation must use the same names.
/// </summary>
internal static class ApiKeyCacheKeys
{
    private const string KeyPrefix = "apikey:";
    private const string UserKeysPrefix = "apikeys:user:";

    /// <summary>The validation entry: full metadata JSON, looked up by key hash.</summary>
    internal static string ByHash(string keyHash) => $"{KeyPrefix}{keyHash}";

    /// <summary>The revocation entry: the same JSON, looked up by the domain ApiKeyId.</summary>
    internal static string ById(string keyId) => $"{KeyPrefix}id:{keyId}";

    /// <summary>The set of a user's key ids; its cardinality gates key creation.</summary>
    internal static string UserSet(string userId) => $"{UserKeysPrefix}{userId}";
}
