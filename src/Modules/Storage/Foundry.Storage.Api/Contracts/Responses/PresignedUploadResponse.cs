namespace Foundry.Storage.Api.Contracts.Responses;

public sealed record PresignedUploadResponse(
    Guid FileId,
    string UploadUrl,
    string StorageKey,
    DateTime ExpiresAt);

public sealed record PresignedUrlResponse(
    string Url,
    DateTime ExpiresAt);
