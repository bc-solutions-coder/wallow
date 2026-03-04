using Foundry.Storage.Domain.Identity;

namespace Foundry.Storage.Application.Commands.ScanUploadedFile;

public sealed record ScanUploadedFileCommand(StoredFileId StoredFileId);
