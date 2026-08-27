using Wallow.Storage.Domain.Enums;

namespace Wallow.Storage.Application.DTOs;

public sealed record CompletePresignedUploadResult(
    Guid FileId,
    FileStatus Status);
