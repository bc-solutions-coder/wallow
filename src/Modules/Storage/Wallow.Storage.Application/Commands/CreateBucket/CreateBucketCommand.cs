using Wallow.Storage.Domain.Enums;

namespace Wallow.Storage.Application.Commands.CreateBucket;

public sealed record CreateBucketCommand(
    string Name,
    string? Description = null,
    AccessLevel Access = AccessLevel.Private,
    long MaxFileSizeBytes = 0,
    IReadOnlyList<string>? AllowedContentTypes = null,
    int? RetentionDays = null,
    RetentionAction? RetentionAction = null,
    bool Versioning = false);
