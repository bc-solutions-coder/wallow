using Foundry.Communications.Domain.Channels.InApp.Enums;
using Foundry.Communications.Domain.Channels.InApp.Events;
using Foundry.Communications.Domain.Channels.InApp.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Communications.Domain.Channels.InApp.Entities;

public sealed class Notification : AggregateRoot<NotificationId>, ITenantScoped
{
    public TenantId TenantId { get; set; }
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public string? ActionUrl { get; private set; }
    public string? SourceModule { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsArchived { get; private set; }

    public bool IsExpired(TimeProvider timeProvider) => ExpiresAt < timeProvider.GetUtcNow().UtcDateTime;

    private Notification() { }

    private Notification(
        TenantId tenantId,
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? actionUrl,
        string? sourceModule,
        DateTime? expiresAt,
        TimeProvider timeProvider)
        : base(NotificationId.New())
    {
        TenantId = tenantId;
        UserId = userId;
        Type = type;
        Title = title;
        Message = message;
        IsRead = false;
        IsArchived = false;
        ActionUrl = actionUrl;
        SourceModule = sourceModule;
        ExpiresAt = expiresAt;
        SetCreated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new NotificationCreatedDomainEvent(
            Id.Value,
            UserId,
            Title,
            Type.ToString()));
    }

    public static Notification Create(
        TenantId tenantId,
        Guid userId,
        NotificationType type,
        string title,
        string message,
        TimeProvider timeProvider,
        string? actionUrl = null,
        string? sourceModule = null,
        DateTime? expiresAt = null)
    {
        return new Notification(tenantId, userId, type, title, message, actionUrl, sourceModule, expiresAt, timeProvider);
    }

    public void MarkAsRead(TimeProvider timeProvider)
    {
        IsRead = true;
        ReadAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new NotificationReadDomainEvent(
            Id.Value,
            UserId));
    }

    public void Archive(TimeProvider timeProvider)
    {
        IsArchived = true;
        SetUpdated(timeProvider.GetUtcNow());
    }
}
