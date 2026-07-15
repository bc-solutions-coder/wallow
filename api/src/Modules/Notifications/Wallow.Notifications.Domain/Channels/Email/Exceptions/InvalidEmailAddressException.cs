using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Email.Exceptions;

public sealed class InvalidEmailAddressException : DomainException
{
    public InvalidEmailAddressException(string message)
        : base("Email.InvalidEmailAddress", message)
    {
    }
}
