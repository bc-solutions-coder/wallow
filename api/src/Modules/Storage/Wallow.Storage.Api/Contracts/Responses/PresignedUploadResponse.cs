namespace Wallow.Storage.Api.Contracts.Responses;

public sealed record PresignedUploadResponse(
    Guid FileId,
    string UploadUrl,
    DateTime ExpiresAt);

public sealed record PresignedUrlResponse(
    string Url,
    DateTime ExpiresAt);
