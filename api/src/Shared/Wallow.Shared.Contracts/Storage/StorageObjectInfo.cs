namespace Wallow.Shared.Contracts.Storage;

/// <summary>
/// Metadata for one object enumerated from a storage backend.
/// </summary>
/// <param name="Key">The object's storage key, '/'-separated regardless of backend.</param>
/// <param name="LastModified">Last-modified time in UTC; S3 uses the current time when the timestamp is unavailable.</param>
public sealed record StorageObjectInfo(string Key, DateTimeOffset LastModified);
