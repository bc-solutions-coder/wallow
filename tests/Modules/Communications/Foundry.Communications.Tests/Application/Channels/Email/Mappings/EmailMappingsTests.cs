using Foundry.Communications.Application.Channels.Email.DTOs;
using Foundry.Communications.Application.Channels.Email.Mappings;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Enums;
using Foundry.Communications.Domain.Channels.Email.ValueObjects;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Application.Channels.Email.Mappings;

public class EmailMappingsTests
{
    private static readonly TenantId _testTenantId = TenantId.New();

    [Fact]
    public void ToDto_EmailMessage_MapsAllFields()
    {
        EmailAddress to = EmailAddress.Create("to@example.com");
        EmailAddress from = EmailAddress.Create("from@example.com");
        EmailContent content = EmailContent.Create("Subject", "Body");

        EmailMessage emailMessage = EmailMessage.Create(_testTenantId, to, from, content, TimeProvider.System);

        EmailDto dto = emailMessage.ToDto();

        dto.Id.Should().Be(emailMessage.Id.Value);
        dto.To.Should().Be("to@example.com");
        dto.From.Should().Be("from@example.com");
        dto.Subject.Should().Be("Subject");
        dto.Body.Should().Be("Body");
        dto.Status.Should().Be(EmailStatus.Pending);
        dto.SentAt.Should().BeNull();
        dto.FailureReason.Should().BeNull();
        dto.RetryCount.Should().Be(0);
    }

    [Fact]
    public void ToDto_EmailMessage_WithNullFrom_MapsFromAsNull()
    {
        EmailAddress to = EmailAddress.Create("to@example.com");
        EmailContent content = EmailContent.Create("Subject", "Body");

        EmailMessage emailMessage = EmailMessage.Create(_testTenantId, to, null, content, TimeProvider.System);

        EmailDto dto = emailMessage.ToDto();

        dto.From.Should().BeNull();
    }

    [Fact]
    public void ToDto_EmailMessage_WhenSent_ReflectsSentStatus()
    {
        EmailAddress to = EmailAddress.Create("to@example.com");
        EmailContent content = EmailContent.Create("Subject", "Body");

        EmailMessage emailMessage = EmailMessage.Create(_testTenantId, to, null, content, TimeProvider.System);
        emailMessage.MarkAsSent(TimeProvider.System);

        EmailDto dto = emailMessage.ToDto();

        dto.Status.Should().Be(EmailStatus.Sent);
        dto.SentAt.Should().NotBeNull();
    }

    [Fact]
    public void ToDto_EmailMessage_WhenFailed_ReflectsFailedStatus()
    {
        EmailAddress to = EmailAddress.Create("to@example.com");
        EmailContent content = EmailContent.Create("Subject", "Body");

        EmailMessage emailMessage = EmailMessage.Create(_testTenantId, to, null, content, TimeProvider.System);
        emailMessage.MarkAsFailed("Connection timeout", TimeProvider.System);

        EmailDto dto = emailMessage.ToDto();

        dto.Status.Should().Be(EmailStatus.Failed);
        dto.FailureReason.Should().Be("Connection timeout");
        dto.RetryCount.Should().Be(1);
    }

    [Fact]
    public void ToDto_EmailPreference_MapsAllFields()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = EmailPreference.Create(userId, NotificationType.SystemNotification, true);

        EmailPreferenceDto dto = preference.ToDto();

        dto.Id.Should().Be(preference.Id.Value);
        dto.UserId.Should().Be(userId);
        dto.NotificationType.Should().Be(NotificationType.SystemNotification);
        dto.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ToDto_EmailPreference_WhenDisabled_ReflectsDisabledState()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned, false);

        EmailPreferenceDto dto = preference.ToDto();

        dto.IsEnabled.Should().BeFalse();
    }
}
