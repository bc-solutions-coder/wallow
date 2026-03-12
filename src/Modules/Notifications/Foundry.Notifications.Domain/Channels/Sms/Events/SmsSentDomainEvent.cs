using Foundry.Notifications.Domain.Channels.Sms.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Notifications.Domain.Channels.Sms.Events;

public sealed record SmsSentDomainEvent(
    SmsMessageId MessageId) : DomainEvent;
