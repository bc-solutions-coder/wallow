using Foundry.Communications.Domain.Channels.Sms.Enums;
using Foundry.Communications.Domain.Channels.Sms.Events;
using Foundry.Communications.Domain.Channels.Sms.Identity;
using Foundry.Communications.Domain.Channels.Sms.ValueObjects;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Communications.Domain.Channels.Sms.Entities;

public sealed class SmsMessage : AggregateRoot<SmsMessageId>, ITenantScoped
{
    public TenantId TenantId { get; set; }
    public PhoneNumber To { get; private set; }
    public string Body { get; private set; } = null!;
    public SmsStatus Status { get; private set; }
    public int RetryCount { get; private set; }

    private SmsMessage() { }

    private SmsMessage(
        TenantId tenantId,
        PhoneNumber to,
        string body)
        : base(SmsMessageId.New())
    {
        TenantId = tenantId;
        To = to;
        Body = body;
        Status = SmsStatus.Pending;
        RetryCount = 0;
        SetCreated();
    }

    public static SmsMessage Create(
        TenantId tenantId,
        PhoneNumber to,
        string body)
    {
        return new SmsMessage(tenantId, to, body);
    }

    public void MarkAsSent()
    {
        Status = SmsStatus.Sent;
        SetUpdated();

        RaiseDomainEvent(new SmsSentDomainEvent(Id));
    }

    public void MarkAsFailed(string reason)
    {
        Status = SmsStatus.Failed;
        RetryCount++;
        SetUpdated();

        RaiseDomainEvent(new SmsFailedDomainEvent(Id, reason));
    }

    public void ResetForRetry()
    {
        Status = SmsStatus.Pending;
        SetUpdated();
    }
}
