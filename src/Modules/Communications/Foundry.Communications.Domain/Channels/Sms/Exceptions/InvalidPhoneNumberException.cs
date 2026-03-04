#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Channels.Sms.Exceptions;

public sealed class InvalidPhoneNumberException : DomainException
{
    public InvalidPhoneNumberException(string message)
        : base("Sms.InvalidPhoneNumber", message)
    {
    }
}
