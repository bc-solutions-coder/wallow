using JetBrains.Annotations;

namespace Wallow.Shared.Contracts.Storage;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record UploadResult(
    Guid FileId,
    string FileName,
    string StorageKey,
    long SizeBytes,
    string ContentType,
    DateTime UploadedAt);
