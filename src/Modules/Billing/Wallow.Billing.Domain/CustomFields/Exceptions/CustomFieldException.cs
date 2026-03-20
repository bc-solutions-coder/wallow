#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.CustomFields.Exceptions;

public class CustomFieldException : BusinessRuleException
{
    public CustomFieldException(string message)
        : base("Billing.CustomField", message)
    {
    }
}
