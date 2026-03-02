using Foundry.Communications.Domain.Channels.Sms.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Channels.Sms.Events;

public sealed record SmsFailedDomainEvent(
    SmsMessageId MessageId,
    string Reason) : DomainEvent;
