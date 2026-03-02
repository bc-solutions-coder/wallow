using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Enums;
using Foundry.Communications.Domain.Channels.Email.Events;
using Foundry.Communications.Domain.Channels.Email.ValueObjects;

namespace Foundry.Communications.Tests.Channels.Email.Domain.Entities;

public class EmailMessageCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsEmailMessageInPendingStatus()
    {
        EmailAddress to = EmailAddress.Create("test@example.com");
        EmailAddress from = EmailAddress.Create("sender@example.com");
        EmailContent content = EmailContent.Create("Test Subject", "Test Body");

        EmailMessage message = EmailMessage.Create(to, from, content, TimeProvider.System);

        message.To.Should().Be(to);
        message.From.Should().Be(from);
        message.Content.Should().Be(content);
        message.Status.Should().Be(EmailStatus.Pending);
        message.RetryCount.Should().Be(0);
    }

    [Fact]
    public void Create_WithoutFrom_AllowsNullSender()
    {
        EmailAddress to = EmailAddress.Create("test@example.com");
        EmailContent content = EmailContent.Create("Subject", "Body");

        EmailMessage message = EmailMessage.Create(to, null, content, TimeProvider.System);

        message.From.Should().BeNull();
    }
}

public class EmailMessageSendingTests
{
    [Fact]
    public void MarkAsSent_ChangesStatusToSent()
    {
        EmailMessage message = EmailMessage.Create(
            EmailAddress.Create("test@example.com"),
            null,
            EmailContent.Create("Subject", "Body"), TimeProvider.System);

        message.MarkAsSent(TimeProvider.System);

        message.Status.Should().Be(EmailStatus.Sent);
        message.SentAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsSent_RaisesEmailSentEvent()
    {
        EmailAddress to = EmailAddress.Create("test@example.com");
        EmailMessage message = EmailMessage.Create(to, null, EmailContent.Create("Subject", "Body"), TimeProvider.System);

        message.MarkAsSent(TimeProvider.System);

        message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<EmailSentDomainEvent>()
            .Which.ToAddress.Should().Be(to.Value);
    }

    [Fact]
    public void MarkAsFailed_ChangesStatusToFailedAndIncrementsRetryCount()
    {
        EmailMessage message = EmailMessage.Create(
            EmailAddress.Create("test@example.com"),
            null,
            EmailContent.Create("Subject", "Body"), TimeProvider.System);

        message.MarkAsFailed("SMTP connection failed", TimeProvider.System);

        message.Status.Should().Be(EmailStatus.Failed);
        message.FailureReason.Should().Be("SMTP connection failed");
        message.RetryCount.Should().Be(1);
    }

    [Fact]
    public void MarkAsFailed_RaisesEmailFailedEvent()
    {
        EmailMessage message = EmailMessage.Create(
            EmailAddress.Create("test@example.com"),
            null,
            EmailContent.Create("Subject", "Body"), TimeProvider.System);

        message.MarkAsFailed("SMTP error", TimeProvider.System);

        message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<EmailFailedDomainEvent>();
    }
}

public class EmailMessageRetryTests
{
    [Fact]
    public void ResetForRetry_ChangesStatusToPending()
    {
        EmailMessage message = EmailMessage.Create(
            EmailAddress.Create("test@example.com"),
            null,
            EmailContent.Create("Subject", "Body"), TimeProvider.System);
        message.MarkAsFailed("Error", TimeProvider.System);

        message.ResetForRetry(TimeProvider.System);

        message.Status.Should().Be(EmailStatus.Pending);
        message.FailureReason.Should().BeNull();
    }

    [Fact]
    public void CanRetry_WithinLimit_ReturnsTrue()
    {
        EmailMessage message = EmailMessage.Create(
            EmailAddress.Create("test@example.com"),
            null,
            EmailContent.Create("Subject", "Body"), TimeProvider.System);
        message.MarkAsFailed("Error 1", TimeProvider.System);
        message.MarkAsFailed("Error 2", TimeProvider.System);

        bool canRetry = message.CanRetry(maxRetries: 3);

        canRetry.Should().BeTrue();
        message.RetryCount.Should().Be(2);
    }

    [Fact]
    public void CanRetry_ExceedsLimit_ReturnsFalse()
    {
        EmailMessage message = EmailMessage.Create(
            EmailAddress.Create("test@example.com"),
            null,
            EmailContent.Create("Subject", "Body"), TimeProvider.System);
        message.MarkAsFailed("Error 1", TimeProvider.System);
        message.MarkAsFailed("Error 2", TimeProvider.System);
        message.MarkAsFailed("Error 3", TimeProvider.System);

        bool canRetry = message.CanRetry(maxRetries: 3);

        canRetry.Should().BeFalse();
        message.RetryCount.Should().Be(3);
    }
}
