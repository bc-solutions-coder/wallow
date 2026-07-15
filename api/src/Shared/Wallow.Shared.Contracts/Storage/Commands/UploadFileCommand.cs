namespace Wallow.Shared.Contracts.Storage.Commands;

public sealed record UploadFileCommand(
    Guid TenantId,
    Guid UserId,
    string BucketName,
    string FileName,
    string ContentType,
    Stream Content,
    long SizeBytes,
    string? Path = null,
    bool IsPublic = false,
    string? Metadata = null);
