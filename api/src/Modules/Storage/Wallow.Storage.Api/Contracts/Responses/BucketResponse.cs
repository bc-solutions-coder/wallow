namespace Wallow.Storage.Api.Contracts.Responses;

public sealed record BucketResponse(
    Guid Id,
    string Name,
    string? Description,
    string Access,
    long MaxFileSizeBytes,
    IReadOnlyList<string>? AllowedContentTypes,
    RetentionPolicyResponse? Retention,
    bool Versioning,
    DateTime CreatedAt);

public sealed record RetentionPolicyResponse(int Days, string Action);
