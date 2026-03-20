using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.CustomFields.Events;

public sealed record CustomFieldDefinitionDeactivatedEvent(
    Guid DefinitionId,
    Guid TenantId,
    string EntityType,
    string FieldKey) : DomainEvent;
