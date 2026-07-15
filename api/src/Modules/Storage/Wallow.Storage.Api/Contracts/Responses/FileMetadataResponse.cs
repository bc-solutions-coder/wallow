namespace Wallow.Storage.Api.Contracts.Responses;

public sealed record FileMetadataResponse(
    Guid Id,
    Guid BucketId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Path,
    bool IsPublic,
    Guid UploadedBy,
    DateTime UploadedAt);
