namespace Wallow.ApiKeys.Infrastructure.Services;

/// <summary>
/// The Valkey key names for the API-key validation cache. Every entry is addressed by data
/// carried on the PostgreSQL row — the key hash, the domain ApiKeyId, and the owning service
/// account — so any writer or invalidator (creation, validation repopulation, revocation, the
/// organization-deletion cascade) derives the same names from the same source of truth.
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
