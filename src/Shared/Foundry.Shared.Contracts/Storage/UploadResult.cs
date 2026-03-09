using JetBrains.Annotations;

namespace Foundry.Shared.Contracts.Storage;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record UploadResult(
    Guid FileId,
    string FileName,
    string StorageKey,
    long SizeBytes,
    string ContentType,
    DateTime UploadedAt);
