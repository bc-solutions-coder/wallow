#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Foundry.Shared.Kernel.Domain;

namespace Foundry.Billing.Domain.CustomFields.Exceptions;

public class CustomFieldException : BusinessRuleException
{
    public CustomFieldException(string message)
        : base("Billing.CustomField", message)
    {
    }
}
