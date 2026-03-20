namespace Wallow.Storage.Api.Contracts.Responses;

public sealed record UploadResponse(
    Guid FileId,
    string FileName,
    long SizeBytes,
    string ContentType,
    DateTime UploadedAt);
