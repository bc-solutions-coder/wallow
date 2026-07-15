using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Domain.Channels.Push.Events;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Tests.Domain.Entities;

public class PushMessageTests
{
    private static PushMessage CreatePushMessage(string title = "Alert", string body = "Alert body")
    {
        return PushMessage.Create(TenantId.New(), new UserId(Guid.NewGuid()), title, body, TimeProvider.System);
    }

    [Fact]
    public void Create_WithValidData_SetsPendingStatus()
    {
        PushMessage message = CreatePushMessage();

        message.Status.Should().Be(PushStatus.Pending);
        message.RetryCount.Should().Be(0);
        message.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        Action act = () => PushMessage.Create(TenantId.New(), new UserId(Guid.NewGuid()), string.Empty, "Body", TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyBody_ThrowsArgumentException()
    {
        Action act = () => PushMessage.Create(TenantId.New(), new UserId(Guid.NewGuid()), "Title", string.Empty, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkDelivered_SetsStatusAndRaisesEvent()
    {
        PushMessage message = CreatePushMessage();

        message.MarkDelivered(TimeProvider.System);

        message.Status.Should().Be(PushStatus.Delivered);
        message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PushMessageSentDomainEvent>();
    }

    [Fact]
    public void MarkFailed_SetsStatusAndIncrementsRetry()
    {
        PushMessage message = CreatePushMessage();

        message.MarkFailed("Invalid token", TimeProvider.System);

        message.Status.Should().Be(PushStatus.Failed);
        message.FailureReason.Should().Be("Invalid token");
        message.RetryCount.Should().Be(1);
        message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PushMessageFailedDomainEvent>();
    }

    [Fact]
    public void CanRetry_WhenBelowMaxRetries_ReturnsTrue()
    {
        PushMessage message = CreatePushMessage();
        message.MarkFailed("Error", TimeProvider.System);

        message.CanRetry().Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenAtMaxRetries_ReturnsFalse()
    {
        PushMessage message = CreatePushMessage();
        message.MarkFailed("e1", TimeProvider.System);
        message.MarkFailed("e2", TimeProvider.System);
        message.MarkFailed("e3", TimeProvider.System);

        message.CanRetry().Should().BeFalse();
    }

    [Fact]
    public void ResetForRetry_SetsPendingAndClearsFailureReason()
    {
        PushMessage message = CreatePushMessage();
        message.MarkFailed("Error", TimeProvider.System);

        message.ResetForRetry(TimeProvider.System);

        message.Status.Should().Be(PushStatus.Pending);
        message.FailureReason.Should().BeNull();
    }
}
