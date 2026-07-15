using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Events;

public sealed record ScimSyncCompletedEvent(
    Guid TenantId,
    string Operation,
    string ResourceType,
    bool Success,
    string? ErrorMessage) : DomainEvent;
