using System.Text.Json;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Domain.Entities;

namespace Foundry.Storage.Application.Mappings;

public static class StorageMappings
{
    public static StoredFileDto ToDto(this StoredFile file)
    {
        return new StoredFileDto(
            file.Id.Value,
            file.TenantId.Value,
            file.BucketId.Value,
            file.FileName,
            file.ContentType,
            file.SizeBytes,
            file.Path,
            file.IsPublic,
            file.UploadedBy,
            file.UploadedAt,
            file.Metadata);
    }

    public static BucketDto ToDto(this StorageBucket bucket)
    {
        IReadOnlyList<string>? allowedContentTypes = null;
        if (!string.IsNullOrEmpty(bucket.AllowedContentTypes))
        {
            allowedContentTypes = JsonSerializer.Deserialize<List<string>>(bucket.AllowedContentTypes);
        }

        RetentionPolicyDto? retentionDto = null;
        if (bucket.Retention is not null)
        {
            retentionDto = new RetentionPolicyDto(
                bucket.Retention.Days,
                bucket.Retention.Action.ToString());
        }

        return new BucketDto(
            bucket.Id.Value,
            bucket.Name,
            bucket.Description,
            bucket.Access.ToString(),
            bucket.MaxFileSizeBytes,
            allowedContentTypes,
            retentionDto,
            bucket.Versioning,
            bucket.CreatedAt);
    }
}
