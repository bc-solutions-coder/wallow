using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Channels.InApp.Identity;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Communications.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;
using InAppNotificationType = Foundry.Communications.Domain.Enums.NotificationType;

namespace Foundry.Communications.Tests.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepositoryTests : IDisposable
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly NotificationRepository _repository;
    private readonly TenantId _tenantId;

    public NotificationRepositoryTests()
    {
        _tenantId = TenantId.Create(Guid.NewGuid());

        DbContextOptions<CommunicationsDbContext> options = new DbContextOptionsBuilder<CommunicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_tenantId);

        _dbContext = new CommunicationsDbContext(options, tenantContext);
        _repository = new NotificationRepository(_dbContext);
    }

    [Fact]
    public async Task Add_AddsNotificationToDatabase()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(_tenantId, userId, InAppNotificationType.SystemAlert, "Title", "Message", TimeProvider.System);

        _repository.Add(notification);
        await _dbContext.SaveChangesAsync();

        int count = await _dbContext.Notifications.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsNotification()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(_tenantId, userId, InAppNotificationType.TaskAssigned, "Task", "Assigned to you", TimeProvider.System);
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();

        Notification? result = await _repository.GetByIdAsync(notification.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Task");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        Notification? result = await _repository.GetByIdAsync(NotificationId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdPagedAsync_ReturnsPaginatedResults()
    {
        Guid userId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            Notification n = Notification.Create(_tenantId, userId, InAppNotificationType.SystemAlert, $"N{i}", $"M{i}", TimeProvider.System);
            await _dbContext.Notifications.AddAsync(n);
        }
        await _dbContext.SaveChangesAsync();

        PagedResult<Notification> result = await _repository.GetByUserIdPagedAsync(userId, 1, 2);

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetByUserIdPagedAsync_SecondPage_ReturnsCorrectItems()
    {
        Guid userId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            Notification n = Notification.Create(_tenantId, userId, InAppNotificationType.SystemAlert, $"N{i}", $"M{i}", TimeProvider.System);
            await _dbContext.Notifications.AddAsync(n);
        }
        await _dbContext.SaveChangesAsync();

        PagedResult<Notification> result = await _repository.GetByUserIdPagedAsync(userId, 2, 2);

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCountOfUnreadNotifications()
    {
        Guid userId = Guid.NewGuid();
        Notification unread1 = Notification.Create(_tenantId, userId, InAppNotificationType.SystemAlert, "U1", "M1", TimeProvider.System);
        Notification unread2 = Notification.Create(_tenantId, userId, InAppNotificationType.TaskComment, "U2", "M2", TimeProvider.System);
        Notification read = Notification.Create(_tenantId, userId, InAppNotificationType.Mention, "R1", "M3", TimeProvider.System);
        read.MarkAsRead(TimeProvider.System);

        await _dbContext.Notifications.AddRangeAsync(unread1, unread2, read);
        await _dbContext.SaveChangesAsync();

        int count = await _repository.GetUnreadCountAsync(userId);

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadByUserIdAsync_ReturnsOnlyUnreadNotifications()
    {
        Guid userId = Guid.NewGuid();
        Notification unread = Notification.Create(_tenantId, userId, InAppNotificationType.SystemAlert, "Unread", "M1", TimeProvider.System);
        Notification read = Notification.Create(_tenantId, userId, InAppNotificationType.TaskComment, "Read", "M2", TimeProvider.System);
        read.MarkAsRead(TimeProvider.System);

        await _dbContext.Notifications.AddRangeAsync(unread, read);
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<Notification> result = await _repository.GetUnreadByUserIdAsync(userId);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Unread");
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(_tenantId, userId, InAppNotificationType.SystemAlert, "Title", "Message", TimeProvider.System);
        _repository.Add(notification);

        await _repository.SaveChangesAsync();

        int count = await _dbContext.Notifications.CountAsync();
        count.Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
