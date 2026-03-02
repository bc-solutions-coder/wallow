using Foundry.Communications.Domain.Channels.Email.Enums;
using Foundry.Communications.Domain.Channels.Email.Events;
using Foundry.Communications.Domain.Channels.Email.Identity;
using Foundry.Communications.Domain.Channels.Email.ValueObjects;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Communications.Domain.Channels.Email.Entities;

public sealed class EmailMessage : AggregateRoot<EmailMessageId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public EmailAddress To { get; private set; } = null!;
    public EmailAddress? From { get; private set; }
    public EmailContent Content { get; private set; } = null!;
    public EmailStatus Status { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? FailureReason { get; private set; }
    public int RetryCount { get; private set; }

    private EmailMessage() { }

    private EmailMessage(
        EmailAddress to,
        EmailAddress? from,
        EmailContent content,
        TimeProvider timeProvider)
        : base(EmailMessageId.New())
    {
        To = to;
        From = from;
        Content = content;
        Status = EmailStatus.Pending;
        RetryCount = 0;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static EmailMessage Create(
        EmailAddress to,
        EmailAddress? from,
        EmailContent content,
        TimeProvider timeProvider)
    {
        return new EmailMessage(to, from, content, timeProvider);
    }

    public void MarkAsSent(TimeProvider timeProvider)
    {
        Status = EmailStatus.Sent;
        SentAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new EmailSentDomainEvent(
            Id.Value,
            To.Value,
            Content.Subject));
    }

    public void MarkAsFailed(string reason, TimeProvider timeProvider)
    {
        Status = EmailStatus.Failed;
        FailureReason = reason;
        RetryCount++;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new EmailFailedDomainEvent(
            Id.Value,
            To.Value,
            reason,
            RetryCount));
    }

    public void ResetForRetry(TimeProvider timeProvider)
    {
        Status = EmailStatus.Pending;
        FailureReason = null;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public bool CanRetry(int maxRetries = 3) => RetryCount < maxRetries;
}
