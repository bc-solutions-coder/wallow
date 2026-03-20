using Wallow.Shared.Kernel.CustomFields;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.CustomFields.Events;

public sealed record CustomFieldDefinitionCreatedEvent(
    Guid DefinitionId,
    Guid TenantId,
    string EntityType,
    string FieldKey,
    string DisplayName,
    CustomFieldType FieldType) : DomainEvent;
