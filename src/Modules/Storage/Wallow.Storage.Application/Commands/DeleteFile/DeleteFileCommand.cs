namespace Wallow.Storage.Application.Commands.DeleteFile;

public sealed record DeleteFileCommand(
    Guid TenantId,
    Guid FileId);
