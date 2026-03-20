using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Domain.Channels.Push.Events;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Domain.Channels.Push.Entities;

public sealed class PushMessage : AggregateRoot<PushMessageId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public UserId RecipientId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public PushStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public int RetryCount { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private PushMessage() { } // EF Core

    private PushMessage(
        TenantId tenantId,
        UserId recipientId,
        string title,
        string body,
        TimeProvider timeProvider)
        : base(PushMessageId.New())
    {
        TenantId = tenantId;
        RecipientId = recipientId;
        Title = title;
        Body = body;
        Status = PushStatus.Pending;
        RetryCount = 0;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static PushMessage Create(
        TenantId tenantId,
        UserId recipientId,
        string title,
        string body,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new PushMessage(tenantId, recipientId, title, body, timeProvider);
    }

    public void MarkDelivered(TimeProvider timeProvider)
    {
        Status = PushStatus.Delivered;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new PushMessageSentDomainEvent(Id));
    }

    public void MarkFailed(string reason, TimeProvider timeProvider)
    {
        Status = PushStatus.Failed;
        FailureReason = reason;
        RetryCount++;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new PushMessageFailedDomainEvent(Id, reason));
    }

    public void ResetForRetry(TimeProvider timeProvider)
    {
        Status = PushStatus.Pending;
        FailureReason = null;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public bool CanRetry(int maxRetries = 3) => RetryCount < maxRetries;
}
