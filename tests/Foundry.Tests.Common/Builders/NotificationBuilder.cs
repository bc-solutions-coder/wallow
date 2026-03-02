using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Channels.InApp.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Tests.Common.Builders;

public class NotificationBuilder
{
    private TenantId _tenantId = TenantId.New();
    private Guid _userId = Guid.NewGuid();
    private NotificationType _type = NotificationType.SystemAlert;
    private string _title = "Test Notification";
    private string _message = "This is a test notification.";
    private string? _actionUrl;
    private string? _sourceModule;
    private DateTime? _expiresAt;
    private bool _read;
    private bool _archived;

    public NotificationBuilder WithTenantId(TenantId tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public NotificationBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public NotificationBuilder WithType(NotificationType type)
    {
        _type = type;
        return this;
    }

    public NotificationBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public NotificationBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    public NotificationBuilder WithActionUrl(string actionUrl)
    {
        _actionUrl = actionUrl;
        return this;
    }

    public NotificationBuilder WithSourceModule(string sourceModule)
    {
        _sourceModule = sourceModule;
        return this;
    }

    public NotificationBuilder WithExpiresAt(DateTime expiresAt)
    {
        _expiresAt = expiresAt;
        return this;
    }

    public NotificationBuilder AsRead()
    {
        _read = true;
        return this;
    }

    public NotificationBuilder AsArchived()
    {
        _archived = true;
        return this;
    }

    public Notification Build()
    {
        Notification notification = Notification.Create(
            _tenantId,
            _userId,
            _type,
            _title,
            _message,
            _actionUrl,
            _sourceModule,
            _expiresAt);

        if (_read)
        {
            notification.MarkAsRead();
        }

        if (_archived)
        {
            notification.Archive();
        }

        notification.ClearDomainEvents();

        return notification;
    }

    public static NotificationBuilder Create() => new();
}
