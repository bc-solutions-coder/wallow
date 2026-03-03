namespace Foundry.Storage.Application.DTOs;

public sealed record StoredFileDto(
    Guid Id,
    Guid TenantId,
    Guid BucketId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Path,
    bool IsPublic,
    Guid UploadedBy,
    DateTime UploadedAt,
    string? Metadata);
