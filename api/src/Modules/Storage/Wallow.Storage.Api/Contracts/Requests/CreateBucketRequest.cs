namespace Wallow.Storage.Api.Contracts.Requests;

public sealed record CreateBucketRequest(
    string Name,
    string? Description = null,
    string Access = "Private",
    long MaxFileSizeBytes = 0,
    IReadOnlyList<string>? AllowedContentTypes = null,
    int? RetentionDays = null,
    string? RetentionAction = null,
    bool Versioning = false);
