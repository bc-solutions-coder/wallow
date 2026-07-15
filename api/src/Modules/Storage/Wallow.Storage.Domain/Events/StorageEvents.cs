using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Domain.Events;

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
