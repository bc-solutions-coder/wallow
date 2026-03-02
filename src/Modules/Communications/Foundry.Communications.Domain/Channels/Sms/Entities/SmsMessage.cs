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
    public PhoneNumber To { get; private set; } = null!;
    public PhoneNumber? From { get; private set; }
    public string Body { get; private set; } = null!;
    public SmsStatus Status { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? FailureReason { get; private set; }
    public int RetryCount { get; private set; }

    private const int MaxBodyLength = 1600;

    private SmsMessage() { }

    private SmsMessage(
        TenantId tenantId,
        PhoneNumber to,
        PhoneNumber? from,
        string body)
        : base(SmsMessageId.New())
    {
        TenantId = tenantId;
        To = to;
        From = from;
        Body = body;
        Status = SmsStatus.Pending;
        RetryCount = 0;
        SetCreated();
    }

    public static SmsMessage Create(
        TenantId tenantId,
        PhoneNumber to,
        PhoneNumber? from,
        string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (body.Length > MaxBodyLength)
        {
            throw new ArgumentException($"SMS body cannot exceed {MaxBodyLength} characters.", nameof(body));
        }

        return new SmsMessage(tenantId, to, from, body);
    }

    public void MarkAsSent()
    {
        Status = SmsStatus.Sent;
        SentAt = DateTime.UtcNow;
        SetUpdated();

        RaiseDomainEvent(new SmsSentDomainEvent(Id));
    }

    public void MarkAsFailed(string reason)
    {
        Status = SmsStatus.Failed;
        FailureReason = reason;
        RetryCount++;
        SetUpdated();

        RaiseDomainEvent(new SmsFailedDomainEvent(Id, reason));
    }

    public void ResetForRetry()
    {
        Status = SmsStatus.Pending;
        FailureReason = null;
        SetUpdated();
    }

    public bool CanRetry(int maxRetries = 3) => RetryCount < maxRetries;
}
