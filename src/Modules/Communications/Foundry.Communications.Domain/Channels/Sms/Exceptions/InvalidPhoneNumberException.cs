using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Channels.Sms.Exceptions;

public sealed class InvalidPhoneNumberException : DomainException
{
    public InvalidPhoneNumberException(string message)
        : base("Sms.InvalidPhoneNumber", message)
    {
    }

    public InvalidPhoneNumberException()
    {
    }

    public InvalidPhoneNumberException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
