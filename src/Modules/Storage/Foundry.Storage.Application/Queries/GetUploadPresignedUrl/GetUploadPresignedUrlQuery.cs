namespace Foundry.Storage.Application.Queries.GetUploadPresignedUrl;

public sealed record GetUploadPresignedUrlQuery(
    Guid TenantId,
    Guid UserId,
    string BucketName,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Path = null,
    TimeSpan? Expiry = null);
