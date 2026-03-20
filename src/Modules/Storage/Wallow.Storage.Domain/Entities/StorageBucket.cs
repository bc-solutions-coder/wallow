using System.Text.Json;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Domain.Events;
using Wallow.Storage.Domain.Identity;
using Wallow.Storage.Domain.ValueObjects;

namespace Wallow.Storage.Domain.Entities;

/// <summary>
/// Logical grouping of files with shared settings.
/// Tenant-scoped to ensure proper isolation.
/// </summary>
public sealed class StorageBucket : AggregateRoot<StorageBucketId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public AccessLevel Access { get; private set; }
    public long MaxFileSizeBytes { get; private set; }
    public string? AllowedContentTypes { get; private set; }
    public RetentionPolicy? Retention { get; private set; }
    public bool Versioning { get; private set; }

    private StorageBucket() { }

    public static StorageBucket Create(
        TenantId tenantId,
        string name,
        string? description = null,
        AccessLevel access = AccessLevel.Private,
        long maxFileSizeBytes = 0,
        IEnumerable<string>? allowedContentTypes = null,
        RetentionPolicy? retention = null,
        bool versioning = false)
    {
        StorageBucket bucket = new StorageBucket
        {
            Id = StorageBucketId.New(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            Access = access,
            MaxFileSizeBytes = maxFileSizeBytes,
            AllowedContentTypes = allowedContentTypes is null
                ? null
                : JsonSerializer.Serialize(allowedContentTypes.ToList()),
            Retention = retention,
            Versioning = versioning,
            CreatedAt = DateTime.UtcNow
        };

        bucket.RaiseDomainEvent(new BucketCreatedEvent(bucket.Id));

        return bucket;
    }

    /// <summary>
    /// Checks if the given content type is allowed for this bucket.
    /// Supports wildcard patterns like "image/*".
    /// </summary>
    public bool IsContentTypeAllowed(string contentType)
    {
        if (string.IsNullOrEmpty(AllowedContentTypes))
        {
            return true;
        }

        List<string>? allowedTypes = JsonSerializer.Deserialize<List<string>>(AllowedContentTypes);
        if (allowedTypes is null || allowedTypes.Count == 0)
        {
            return true;
        }

        string contentTypeLower = contentType.ToLowerInvariant();

        foreach (string pattern in allowedTypes)
        {
            string patternLower = pattern.ToLowerInvariant();

            if (patternLower == "*/*" || patternLower == "*")
            {
                return true;
            }

            if (patternLower.EndsWith("/*", StringComparison.Ordinal))
            {
                string prefix = patternLower[..^2];
                if (contentTypeLower.StartsWith(prefix + "/", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (patternLower == contentTypeLower)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the given file size is within the allowed limit.
    /// </summary>
    public bool IsFileSizeAllowed(long sizeBytes)
    {
        if (MaxFileSizeBytes == 0)
        {
            return true;
        }

        return sizeBytes <= MaxFileSizeBytes;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    public void UpdateAccess(AccessLevel access)
    {
        Access = access;
    }

    public void UpdateMaxFileSize(long maxFileSizeBytes)
    {
        MaxFileSizeBytes = maxFileSizeBytes;
    }

    public void UpdateAllowedContentTypes(IEnumerable<string>? contentTypes)
    {
        AllowedContentTypes = contentTypes is null
            ? null
            : JsonSerializer.Serialize(contentTypes.ToList());
    }

    public void UpdateRetention(RetentionPolicy? retention)
    {
        Retention = retention;
    }

    public void UpdateVersioning(bool versioning)
    {
        Versioning = versioning;
    }

    public void Delete()
    {
        RaiseDomainEvent(new BucketDeletedEvent(Id));
    }
}
