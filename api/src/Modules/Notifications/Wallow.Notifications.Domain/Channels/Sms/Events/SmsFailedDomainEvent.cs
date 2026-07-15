using JetBrains.Annotations;
using Wallow.Notifications.Domain.Channels.Sms.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Sms.Events;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SmsFailedDomainEvent(
    SmsMessageId MessageId,
    string Reason) : DomainEvent;
