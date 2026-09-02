using Wallow.Notifications.Domain.Errors;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Sms.Exceptions;

public sealed class InvalidPhoneNumberException : DomainException
{
    public InvalidPhoneNumberException(string message)
        : base(NotificationsErrors.SmsInvalidPhoneNumber, message)
    {
    }
}
