using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Application.Commands.ScanUploadedFile;

public sealed record ScanUploadedFileCommand(StoredFileId StoredFileId);
