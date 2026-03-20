using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Notifications.Domain.Channels.InApp.Identity;
using Wallow.Notifications.Domain.Enums;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Kernel.Pagination;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public sealed class NotificationRepositoryTests : RepositoryTestBase
{
    private readonly NotificationRepository _repository;

    public NotificationRepositoryTests()
    {
        _repository = new NotificationRepository(Context);
    }

    private Notification CreateNotification(
        Guid? userId = null,
        NotificationType type = NotificationType.SystemAlert,
        string title = "Test Title",
        string message = "Test Message",
        bool isRead = false,
        bool isArchived = false,
        DateTime? expiresAt = null)
    {
        Notification notification = Notification.Create(
            TestTenantId,
            userId ?? Guid.NewGuid(),
            type,
            title,
            message,
            TimeProvider.System,
            expiresAt: expiresAt);

        if (isRead)
        {
            notification.MarkAsRead(TimeProvider.System);
        }

        if (isArchived)
        {
            notification.Archive(TimeProvider.System);
        }

        notification.ClearDomainEvents();
        return notification;
    }

    [Fact]
    public async Task Add_And_GetByIdAsync_ReturnsNotification()
    {
        Notification notification = CreateNotification();

        _repository.Add(notification);
        await Context.SaveChangesAsync();

        Notification? result = await _repository.GetByIdAsync(notification.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Title");
        result.Message.Should().Be("Test Message");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        Notification? result = await _repository.GetByIdAsync(NotificationId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdPagedAsync_ReturnsOnlyUserNotifications()
    {
        Guid userId = Guid.NewGuid();
        _repository.Add(CreateNotification(userId: userId, title: "User1"));
        _repository.Add(CreateNotification(userId: userId, title: "User2"));
        _repository.Add(CreateNotification(userId: Guid.NewGuid(), title: "OtherUser"));
        await Context.SaveChangesAsync();

        PagedResult<Notification> result = await _repository.GetByUserIdPagedAsync(userId, 1, 10);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByUserIdPagedAsync_ExcludesArchivedNotifications()
    {
        Guid userId = Guid.NewGuid();
        _repository.Add(CreateNotification(userId: userId, title: "Active"));
        _repository.Add(CreateNotification(userId: userId, isArchived: true));
        await Context.SaveChangesAsync();

        PagedResult<Notification> result = await _repository.GetByUserIdPagedAsync(userId, 1, 10);

        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Active");
    }

    [Fact]
    public async Task GetByUserIdPagedAsync_ExcludesExpiredNotifications()
    {
        Guid userId = Guid.NewGuid();
        _repository.Add(CreateNotification(userId: userId, title: "NoExpiry"));
        _repository.Add(CreateNotification(userId: userId, title: "FutureExpiry", expiresAt: DateTime.UtcNow.AddDays(1)));
        _repository.Add(CreateNotification(userId: userId, title: "Expired", expiresAt: DateTime.UtcNow.AddDays(-1)));
        await Context.SaveChangesAsync();

        PagedResult<Notification> result = await _repository.GetByUserIdPagedAsync(userId, 1, 10);

        result.Items.Should().HaveCount(2);
        result.Items.Should().NotContain(n => n.Title == "Expired");
    }

    [Fact]
    public async Task GetByUserIdPagedAsync_RespectsPageSize()
    {
        Guid userId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            _repository.Add(CreateNotification(userId: userId, title: $"Notification {i}"));
        }

        await Context.SaveChangesAsync();

        PagedResult<Notification> result = await _repository.GetByUserIdPagedAsync(userId, 1, 2);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        Guid userId = Guid.NewGuid();
        _repository.Add(CreateNotification(userId: userId));
        _repository.Add(CreateNotification(userId: userId));
        _repository.Add(CreateNotification(userId: userId, isRead: true));
        await Context.SaveChangesAsync();

        int count = await _repository.GetUnreadCountAsync(userId);

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadByUserIdAsync_ReturnsOnlyUnread()
    {
        Guid userId = Guid.NewGuid();
        _repository.Add(CreateNotification(userId: userId, title: "Unread1"));
        _repository.Add(CreateNotification(userId: userId, title: "Unread2"));
        _repository.Add(CreateNotification(userId: userId, isRead: true));
        await Context.SaveChangesAsync();

        IReadOnlyList<Notification> result = await _repository.GetUnreadByUserIdAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        Notification notification = CreateNotification();
        _repository.Add(notification);

        await _repository.SaveChangesAsync();

        Notification? result = await _repository.GetByIdAsync(notification.Id);
        result.Should().NotBeNull();
    }
}
