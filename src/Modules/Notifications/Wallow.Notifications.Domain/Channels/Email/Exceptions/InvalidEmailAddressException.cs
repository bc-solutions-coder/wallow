#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Email.Exceptions;

public sealed class InvalidEmailAddressException : DomainException
{
    public InvalidEmailAddressException(string message)
        : base("Email.InvalidEmailAddress", message)
    {
    }
}
