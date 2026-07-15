using JetBrains.Annotations;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Announcements.Domain.Announcements.Entities;

public sealed class Announcement : AggregateRoot<AnnouncementId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public AnnouncementType Type { get; private set; }
    public AnnouncementTarget Target { get; private set; }
    public string? TargetValue { get; private set; }
    public DateTime? PublishAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsDismissible { get; private set; }
    public string? ActionUrl { get; private set; }
    public string? ActionLabel { get; private set; }
    public string? ImageUrl { get; private set; }
    public AnnouncementStatus Status { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private Announcement() { } // EF Core

    private Announcement(
        TenantId tenantId,
        string title,
        string content,
        AnnouncementType type,
        AnnouncementTarget target,
        string? targetValue,
        DateTime? publishAt,
        DateTime? expiresAt,
        bool isPinned,
        bool isDismissible,
        string? actionUrl,
        string? actionLabel,
        string? imageUrl,
        TimeProvider timeProvider)
        : base(AnnouncementId.New())
    {
        TenantId = tenantId;
        Title = title;
        Content = content;
        Type = type;
        Target = target;
        TargetValue = targetValue;
        PublishAt = publishAt;
        ExpiresAt = expiresAt;
        IsPinned = isPinned;
        IsDismissible = isDismissible;
        ActionUrl = actionUrl;
        ActionLabel = actionLabel;
        ImageUrl = imageUrl;
        Status = publishAt.HasValue ? AnnouncementStatus.Scheduled : AnnouncementStatus.Draft;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static Announcement Create(
        TenantId tenantId,
        string title,
        string content,
        AnnouncementType type,
        TimeProvider timeProvider,
        AnnouncementTarget target = AnnouncementTarget.All,
        string? targetValue = null,
        DateTime? publishAt = null,
        DateTime? expiresAt = null,
        bool isPinned = false,
        bool isDismissible = true,
        string? actionUrl = null,
        string? actionLabel = null,
        string? imageUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new Announcement(
            tenantId, title, content, type, target, targetValue,
            publishAt, expiresAt, isPinned, isDismissible,
            actionUrl, actionLabel, imageUrl, timeProvider);
    }

    public void Update(
        string title,
        string content,
        AnnouncementType type,
        AnnouncementTarget target,
        string? targetValue,
        DateTime? publishAt,
        DateTime? expiresAt,
        bool isPinned,
        bool isDismissible,
        string? actionUrl,
        string? actionLabel,
        string? imageUrl,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Title = title;
        Content = content;
        Type = type;
        Target = target;
        TargetValue = targetValue;
        PublishAt = publishAt;
        ExpiresAt = expiresAt;
        IsPinned = isPinned;
        IsDismissible = isDismissible;
        ActionUrl = actionUrl;
        ActionLabel = actionLabel;
        ImageUrl = imageUrl;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void Publish(TimeProvider timeProvider)
    {
        if (Status == AnnouncementStatus.Published)
        {
            return;
        }

        Status = AnnouncementStatus.Published;
        PublishAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow());
    }

    [UsedImplicitly]
    public void Expire(TimeProvider timeProvider)
    {
        Status = AnnouncementStatus.Expired;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void Archive(TimeProvider timeProvider)
    {
        Status = AnnouncementStatus.Archived;
        SetUpdated(timeProvider.GetUtcNow());
    }
}
