using Foundry.Communications.Domain.Channels.Sms.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Channels.Sms.Events;

public sealed record SmsSentDomainEvent(
    SmsMessageId MessageId) : DomainEvent;
