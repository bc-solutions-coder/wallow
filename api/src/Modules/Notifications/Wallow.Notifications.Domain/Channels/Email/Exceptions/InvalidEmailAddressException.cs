using Wallow.Notifications.Domain.Errors;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Email.Exceptions;

public sealed class InvalidEmailAddressException : DomainException
{
    public InvalidEmailAddressException(string message)
        : base(NotificationsErrors.EmailInvalidEmailAddress, message)
    {
    }
}
