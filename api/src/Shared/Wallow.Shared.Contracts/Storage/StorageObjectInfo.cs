namespace Wallow.Shared.Contracts.Storage;

/// <summary>
/// Metadata for one object enumerated from a storage backend.
/// </summary>
/// <param name="Key">The object's storage key, '/'-separated regardless of backend.</param>
/// <param name="LastModified">When the object's content was last written, in UTC.</param>
public sealed record StorageObjectInfo(string Key, DateTimeOffset LastModified);
