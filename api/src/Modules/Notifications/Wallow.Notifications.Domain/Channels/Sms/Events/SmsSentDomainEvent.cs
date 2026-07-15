using Wallow.Notifications.Domain.Channels.Sms.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Sms.Events;

public sealed record SmsSentDomainEvent(
    SmsMessageId MessageId) : DomainEvent;
