namespace Wallow.Storage.Application.DTOs;

public sealed record BucketDto(
    Guid Id,
    string Name,
    string? Description,
    string Access,
    long MaxFileSizeBytes,
    IReadOnlyList<string>? AllowedContentTypes,
    RetentionPolicyDto? Retention,
    bool Versioning,
    DateTime CreatedAt);

public sealed record RetentionPolicyDto(int Days, string Action);
