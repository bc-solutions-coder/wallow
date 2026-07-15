namespace Wallow.Storage.Api.Contracts.Requests;

public sealed record PresignedUploadRequest(
    string BucketName,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Path = null,
    int? ExpiryMinutes = null);
