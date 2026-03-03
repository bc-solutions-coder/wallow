using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Storage.Domain.Identity;

namespace Foundry.Storage.Domain.Events;

public sealed record FileUploadedEvent(
    StoredFileId FileId,
    StorageBucketId BucketId,
    TenantId TenantId) : DomainEvent;

public sealed record FileDeletedEvent(
    StoredFileId FileId,
    StorageBucketId BucketId,
    TenantId TenantId) : DomainEvent;

public sealed record BucketCreatedEvent(
    StorageBucketId BucketId) : DomainEvent;

public sealed record BucketDeletedEvent(
    StorageBucketId BucketId) : DomainEvent;
