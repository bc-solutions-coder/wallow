using Wallow.Notifications.Domain.Channels.Sms.Entities;
using Wallow.Notifications.Domain.Channels.Sms.Enums;
using Wallow.Notifications.Domain.Channels.Sms.Events;
using Wallow.Notifications.Domain.Channels.Sms.ValueObjects;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Tests.Domain.Entities;

public class SmsMessageTests
{
    private static SmsMessage CreateSms(string to = "+12025550100", string body = "Hello!")
    {
        PhoneNumber phone = PhoneNumber.Create(to);
        return SmsMessage.Create(TenantId.New(), phone, null, body, TimeProvider.System);
    }

    [Fact]
    public void Create_WithValidData_SetsPendingStatus()
    {
        SmsMessage sms = CreateSms();

        sms.Status.Should().Be(SmsStatus.Pending);
        sms.RetryCount.Should().Be(0);
        sms.SentAt.Should().BeNull();
        sms.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Create_WithBodyTooLong_ThrowsArgumentException()
    {
        PhoneNumber phone = PhoneNumber.Create("+12025550100");
        string tooLong = new('x', 1601);

        Action act = () => SmsMessage.Create(TenantId.New(), phone, null, tooLong, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyBody_ThrowsArgumentException()
    {
        PhoneNumber phone = PhoneNumber.Create("+12025550100");

        Action act = () => SmsMessage.Create(TenantId.New(), phone, null, string.Empty, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsSent_SetsStatusAndRaisesEvent()
    {
        SmsMessage sms = CreateSms();

        sms.MarkAsSent(TimeProvider.System);

        sms.Status.Should().Be(SmsStatus.Sent);
        sms.SentAt.Should().NotBeNull();
        sms.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SmsSentDomainEvent>();
    }

    [Fact]
    public void MarkAsFailed_SetsStatusAndIncrementsRetry()
    {
        SmsMessage sms = CreateSms();

        sms.MarkAsFailed("Invalid number", TimeProvider.System);

        sms.Status.Should().Be(SmsStatus.Failed);
        sms.FailureReason.Should().Be("Invalid number");
        sms.RetryCount.Should().Be(1);
        sms.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SmsFailedDomainEvent>();
    }

    [Fact]
    public void CanRetry_WhenBelowMaxRetries_ReturnsTrue()
    {
        SmsMessage sms = CreateSms();
        sms.MarkAsFailed("Error", TimeProvider.System);

        sms.CanRetry().Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenAtMaxRetries_ReturnsFalse()
    {
        SmsMessage sms = CreateSms();
        sms.MarkAsFailed("e1", TimeProvider.System);
        sms.MarkAsFailed("e2", TimeProvider.System);
        sms.MarkAsFailed("e3", TimeProvider.System);

        sms.CanRetry().Should().BeFalse();
    }

    [Fact]
    public void ResetForRetry_SetsPendingAndClearsFailureReason()
    {
        SmsMessage sms = CreateSms();
        sms.MarkAsFailed("Error", TimeProvider.System);

        sms.ResetForRetry(TimeProvider.System);

        sms.Status.Should().Be(SmsStatus.Pending);
        sms.FailureReason.Should().BeNull();
    }
}
