using Foundry.Shared.Kernel.Domain;

namespace Foundry.Billing.Domain.CustomFields.Events;

public sealed record CustomFieldDefinitionDeactivatedEvent(
    Guid DefinitionId,
    Guid TenantId,
    string EntityType,
    string FieldKey) : DomainEvent;
