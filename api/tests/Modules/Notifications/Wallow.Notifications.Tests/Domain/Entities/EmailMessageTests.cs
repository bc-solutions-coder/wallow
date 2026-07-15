using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Notifications.Domain.Channels.Email.Enums;
using Wallow.Notifications.Domain.Channels.Email.Events;
using Wallow.Notifications.Domain.Channels.Email.ValueObjects;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Tests.Domain.Entities;

public class EmailMessageTests
{
    private static EmailMessage CreateEmail(string to = "user@test.com", string subject = "Test Subject", string body = "Body text")
    {
        EmailAddress toAddress = EmailAddress.Create(to);
        EmailContent content = EmailContent.Create(subject, body);
        return EmailMessage.Create(TenantId.New(), toAddress, null, content, TimeProvider.System);
    }

    [Fact]
    public void Create_WithValidData_SetsPendingStatus()
    {
        EmailMessage email = CreateEmail();

        email.Status.Should().Be(EmailStatus.Pending);
        email.RetryCount.Should().Be(0);
        email.SentAt.Should().BeNull();
        email.FailureReason.Should().BeNull();
    }

    [Fact]
    public void MarkAsSent_SetsStatusSentAndRaisesEvent()
    {
        EmailMessage email = CreateEmail();

        email.MarkAsSent(TimeProvider.System);

        email.Status.Should().Be(EmailStatus.Sent);
        email.SentAt.Should().NotBeNull();
        email.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<EmailSentDomainEvent>();
    }

    [Fact]
    public void MarkAsFailed_SetsStatusFailedAndIncrementsRetry()
    {
        EmailMessage email = CreateEmail();

        email.MarkAsFailed("SMTP timeout", TimeProvider.System);

        email.Status.Should().Be(EmailStatus.Failed);
        email.FailureReason.Should().Be("SMTP timeout");
        email.RetryCount.Should().Be(1);
        email.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<EmailFailedDomainEvent>();
    }

    [Fact]
    public void MarkAsFailed_MultipleAttempts_IncrementsRetryEachTime()
    {
        EmailMessage email = CreateEmail();

        email.MarkAsFailed("Error 1", TimeProvider.System);
        email.MarkAsFailed("Error 2", TimeProvider.System);

        email.RetryCount.Should().Be(2);
    }

    [Fact]
    public void CanRetry_WhenBelowMaxRetries_ReturnsTrue()
    {
        EmailMessage email = CreateEmail();
        email.MarkAsFailed("Error", TimeProvider.System);

        email.CanRetry().Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenAtMaxRetries_ReturnsFalse()
    {
        EmailMessage email = CreateEmail();
        email.MarkAsFailed("e1", TimeProvider.System);
        email.MarkAsFailed("e2", TimeProvider.System);
        email.MarkAsFailed("e3", TimeProvider.System);

        email.CanRetry().Should().BeFalse();
    }

    [Fact]
    public void ResetForRetry_SetsPendingAndClearsFailureReason()
    {
        EmailMessage email = CreateEmail();
        email.MarkAsFailed("Error", TimeProvider.System);

        email.ResetForRetry(TimeProvider.System);

        email.Status.Should().Be(EmailStatus.Pending);
        email.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Create_WithFromAddress_SetsFromAddress()
    {
        EmailAddress to = EmailAddress.Create("to@test.com");
        EmailAddress from = EmailAddress.Create("from@test.com");
        EmailContent content = EmailContent.Create("Subject", "Body");

        EmailMessage email = EmailMessage.Create(TenantId.New(), to, from, content, TimeProvider.System);

        email.From.Should().NotBeNull();
        email.From!.Value.Should().Be("from@test.com");
    }
}
