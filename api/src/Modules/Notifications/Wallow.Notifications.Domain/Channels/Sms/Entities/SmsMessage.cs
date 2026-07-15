using JetBrains.Annotations;
using Wallow.Notifications.Domain.Channels.Sms.Enums;
using Wallow.Notifications.Domain.Channels.Sms.Events;
using Wallow.Notifications.Domain.Channels.Sms.Identity;
using Wallow.Notifications.Domain.Channels.Sms.ValueObjects;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Domain.Channels.Sms.Entities;

public sealed class SmsMessage : AggregateRoot<SmsMessageId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public PhoneNumber To { get; private set; } = null!;
    public PhoneNumber? From { get; private set; }
    public string Body { get; private set; } = null!;
    public SmsStatus Status { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? FailureReason { get; private set; }
    public int RetryCount { get; private set; }

    private const int MaxBodyLength = 1600;

    // ReSharper disable once UnusedMember.Local
    private SmsMessage() { } // EF Core

    private SmsMessage(
        TenantId tenantId,
        PhoneNumber to,
        PhoneNumber? from,
        string body,
        TimeProvider timeProvider)
        : base(SmsMessageId.New())
    {
        TenantId = tenantId;
        To = to;
        From = from;
        Body = body;
        Status = SmsStatus.Pending;
        RetryCount = 0;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static SmsMessage Create(
        TenantId tenantId,
        PhoneNumber to,
        PhoneNumber? from,
        string body,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (body.Length > MaxBodyLength)
        {
            throw new ArgumentException($"SMS body cannot exceed {MaxBodyLength} characters.", nameof(body));
        }

        return new SmsMessage(tenantId, to, from, body, timeProvider);
    }

    public void MarkAsSent(TimeProvider timeProvider)
    {
        Status = SmsStatus.Sent;
        SentAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new SmsSentDomainEvent(Id));
    }

    public void MarkAsFailed(string reason, TimeProvider timeProvider)
    {
        Status = SmsStatus.Failed;
        FailureReason = reason;
        RetryCount++;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new SmsFailedDomainEvent(Id, reason));
    }

    [UsedImplicitly]
    public void ResetForRetry(TimeProvider timeProvider)
    {
        Status = SmsStatus.Pending;
        FailureReason = null;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public bool CanRetry(int maxRetries = 3) => RetryCount < maxRetries;
}
