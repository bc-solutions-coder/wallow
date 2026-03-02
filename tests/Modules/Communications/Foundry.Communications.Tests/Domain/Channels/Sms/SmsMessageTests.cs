using Foundry.Communications.Domain.Channels.Sms.Entities;
using Foundry.Communications.Domain.Channels.Sms.Enums;
using Foundry.Communications.Domain.Channels.Sms.Events;
using Foundry.Communications.Domain.Channels.Sms.ValueObjects;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Domain.Channels.Sms;

public class SmsMessageCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSmsMessageInPendingStatus()
    {
        TenantId tenantId = TenantId.New();
        PhoneNumber to = PhoneNumber.Create("+14155551234");

        SmsMessage message = SmsMessage.Create(tenantId, to, null, "Hello", TimeProvider.System);

        message.TenantId.Should().Be(tenantId);
        message.To.Should().Be(to);
        message.Body.Should().Be("Hello");
        message.Status.Should().Be(SmsStatus.Pending);
        message.RetryCount.Should().Be(0);
    }
}

public class SmsMessageSendingTests
{
    [Fact]
    public void MarkAsSent_ChangesStatusToSent()
    {
        SmsMessage message = CreateTestMessage();

        message.MarkAsSent(TimeProvider.System);

        message.Status.Should().Be(SmsStatus.Sent);
    }

    [Fact]
    public void MarkAsSent_RaisesSmsSentDomainEvent()
    {
        SmsMessage message = CreateTestMessage();

        message.MarkAsSent(TimeProvider.System);

        message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SmsSentDomainEvent>()
            .Which.MessageId.Should().Be(message.Id);
    }

    [Fact]
    public void MarkAsFailed_ChangesStatusToFailedAndIncrementsRetryCount()
    {
        SmsMessage message = CreateTestMessage();

        message.MarkAsFailed("Provider timeout", TimeProvider.System);

        message.Status.Should().Be(SmsStatus.Failed);
        message.RetryCount.Should().Be(1);
    }

    [Fact]
    public void MarkAsFailed_RaisesSmsFailedDomainEvent()
    {
        SmsMessage message = CreateTestMessage();

        message.MarkAsFailed("Provider timeout", TimeProvider.System);

        message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SmsFailedDomainEvent>()
            .Which.Reason.Should().Be("Provider timeout");
    }

    private static SmsMessage CreateTestMessage() =>
        SmsMessage.Create(TenantId.New(), PhoneNumber.Create("+14155551234"), null, "Test body", TimeProvider.System);
}

public class SmsMessageRetryTests
{
    [Fact]
    public void ResetForRetry_ChangesStatusToPending()
    {
        SmsMessage message = SmsMessage.Create(
            TenantId.New(),
            PhoneNumber.Create("+14155551234"),
            null,
            "Test body", TimeProvider.System);
        message.MarkAsFailed("Error", TimeProvider.System);

        message.ResetForRetry(TimeProvider.System);

        message.Status.Should().Be(SmsStatus.Pending);
    }

    [Fact]
    public void MarkAsFailed_MultipleTimes_IncrementsRetryCountEachTime()
    {
        SmsMessage message = SmsMessage.Create(
            TenantId.New(),
            PhoneNumber.Create("+14155551234"),
            null,
            "Test body", TimeProvider.System);

        message.MarkAsFailed("Error 1", TimeProvider.System);
        message.MarkAsFailed("Error 2", TimeProvider.System);

        message.RetryCount.Should().Be(2);
    }
}
