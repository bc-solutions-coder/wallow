using Foundry.Shared.Kernel.Identity;

namespace Foundry.Billing.Domain.CustomFields.Identity;

public readonly record struct CustomFieldDefinitionId(Guid Value) : IStronglyTypedId<CustomFieldDefinitionId>
{
    public static CustomFieldDefinitionId Create(Guid value) => new(value);
    public static CustomFieldDefinitionId New() => new(Guid.NewGuid());
}
