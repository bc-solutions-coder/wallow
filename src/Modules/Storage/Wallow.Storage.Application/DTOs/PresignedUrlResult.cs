namespace Wallow.Storage.Application.DTOs;

public sealed record PresignedUrlResult(
    string Url,
    DateTime ExpiresAt);

public sealed record PresignedUploadResult(
    Guid FileId,
    string UploadUrl,
    DateTime ExpiresAt);
