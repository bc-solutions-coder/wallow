using Foundry.Communications.Domain.Channels.Push.Entities;
using Foundry.Communications.Domain.Channels.Push.Enums;
using Foundry.Communications.Domain.Channels.Push.Events;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Time.Testing;

namespace Foundry.Communications.Tests.Channels.Push.Domain.Entities;

public class PushMessageCreateTests
{
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public void Create_WithValidData_ReturnsPushMessageInPendingStatus()
    {
        TenantId tenantId = TenantId.New();
        UserId recipientId = UserId.New();
        string title = "Test Push";
        string body = "Test body";

        PushMessage message = PushMessage.Create(tenantId, recipientId, title, body, _timeProvider);

        message.TenantId.Should().Be(tenantId);
        message.RecipientId.Should().Be(recipientId);
        message.Title.Should().Be(title);
        message.Body.Should().Be(body);
        message.Status.Should().Be(PushStatus.Pending);
        message.RetryCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhiteSpaceTitle_ThrowsArgumentException(string? title)
    {
        Action act = () => PushMessage.Create(TenantId.New(), UserId.New(), title!, "body", _timeProvider);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhiteSpaceBody_ThrowsArgumentException(string? body)
    {
        Action act = () => PushMessage.Create(TenantId.New(), UserId.New(), "title", body!, _timeProvider);

        act.Should().Throw<ArgumentException>();
    }
}

public class PushMessageCanRetryTests
{
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public void CanRetry_WithZeroRetries_ReturnsTrue()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);

        bool result = message.CanRetry(maxRetries: 3);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WithRetriesLessThanMax_ReturnsTrue()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);
        message.MarkFailed("error", _timeProvider);
        message.MarkFailed("error", _timeProvider);

        bool result = message.CanRetry(maxRetries: 3);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WithRetriesEqualToMax_ReturnsFalse()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);
        message.MarkFailed("error", _timeProvider);
        message.MarkFailed("error", _timeProvider);
        message.MarkFailed("error", _timeProvider);

        bool result = message.CanRetry(maxRetries: 3);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WithRetriesExceedingMax_ReturnsFalse()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);
        message.MarkFailed("error", _timeProvider);
        message.MarkFailed("error", _timeProvider);
        message.MarkFailed("error", _timeProvider);
        message.MarkFailed("error", _timeProvider);

        bool result = message.CanRetry(maxRetries: 3);

        result.Should().BeFalse();
    }
}

public class PushMessageMarkDeliveredTests
{
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public void MarkDelivered_SetsStatusToDelivered()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);

        message.MarkDelivered(_timeProvider);

        message.Status.Should().Be(PushStatus.Delivered);
    }

    [Fact]
    public void MarkDelivered_RaisesPushMessageSentDomainEvent()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);

        message.MarkDelivered(_timeProvider);

        message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PushMessageSentDomainEvent>();
    }
}

public class PushMessageMarkFailedTests
{
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public void MarkFailed_SetsStatusToFailed()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);

        message.MarkFailed("provider timeout", _timeProvider);

        message.Status.Should().Be(PushStatus.Failed);
    }

    [Fact]
    public void MarkFailed_IncrementsRetryCount()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);

        message.MarkFailed("error", _timeProvider);

        message.RetryCount.Should().Be(1);
    }

    [Fact]
    public void MarkFailed_RaisesPushMessageFailedDomainEventWithReason()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);

        message.MarkFailed("provider timeout", _timeProvider);

        PushMessageFailedDomainEvent domainEvent = message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PushMessageFailedDomainEvent>().Subject;
        domainEvent.Reason.Should().Be("provider timeout");
    }

    [Fact]
    public void MarkFailed_MultipleCalls_AccumulatesRetryCount()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);

        message.MarkFailed("error 1", _timeProvider);
        message.MarkFailed("error 2", _timeProvider);
        message.MarkFailed("error 3", _timeProvider);

        message.RetryCount.Should().Be(3);
    }
}

public class PushMessageResetForRetryTests
{
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public void ResetForRetry_SetsStatusToPending()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);
        message.MarkFailed("error", _timeProvider);

        message.ResetForRetry(_timeProvider);

        message.Status.Should().Be(PushStatus.Pending);
    }

    [Fact]
    public void ResetForRetry_ClearsFailureReason()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);
        message.MarkFailed("provider timeout", _timeProvider);

        message.ResetForRetry(_timeProvider);

        message.FailureReason.Should().BeNull();
    }

    [Fact]
    public void ResetForRetry_DoesNotChangeRetryCount()
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "title", "body", _timeProvider);
        message.MarkFailed("error", _timeProvider);
        message.MarkFailed("error", _timeProvider);

        message.ResetForRetry(_timeProvider);

        message.RetryCount.Should().Be(2);
    }
}
