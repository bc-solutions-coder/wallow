using Foundry.Communications.Domain.Channels.Sms.Identity;
using Foundry.Shared.Kernel.Domain;
using JetBrains.Annotations;

namespace Foundry.Communications.Domain.Channels.Sms.Events;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SmsFailedDomainEvent(
    SmsMessageId MessageId,
    string Reason) : DomainEvent;
