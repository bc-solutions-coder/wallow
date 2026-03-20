using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Announcements.Application.Announcements.Services;

public interface IAnnouncementTargetingService
{
    Task<IReadOnlyList<AnnouncementDto>> GetActiveAnnouncementsForUserAsync(
        UserContext userContext,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> ResolveTargetUsersAsync(
        Announcement announcement,
        CancellationToken ct = default);
}

public sealed record UserContext(
    UserId UserId,
    TenantId TenantId,
    string? PlanName,
    IReadOnlyList<string> Roles);

public sealed class AnnouncementTargetingService(
    IAnnouncementRepository announcementRepository,
    IAnnouncementDismissalRepository dismissalRepository,
    TimeProvider timeProvider) : IAnnouncementTargetingService
{

    public async Task<IReadOnlyList<AnnouncementDto>> GetActiveAnnouncementsForUserAsync(
        UserContext userContext,
        CancellationToken ct = default)
    {
        IReadOnlyList<Announcement> announcements = await announcementRepository.GetPublishedAsync(ct);
        IReadOnlyList<AnnouncementDismissal> dismissals = await dismissalRepository.GetByUserIdAsync(userContext.UserId, ct);
        HashSet<AnnouncementId> dismissedIds = dismissals.Select(d => d.AnnouncementId).ToHashSet();

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        return announcements
            .Where(a => IsActiveAndNotExpired(a, now))
            .Where(a => MatchesTarget(a, userContext))
            .Where(a => !a.IsDismissible || !dismissedIds.Contains(a.Id))
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    public Task<IReadOnlyList<Guid>> ResolveTargetUsersAsync(
        Announcement announcement,
        CancellationToken ct = default)
    {
        // For now, return empty list - the Notifications module will handle
        // broadcast-style delivery based on target criteria in the integration event.
        // A full implementation would query tenant/user data to resolve specific recipients.
        IReadOnlyList<Guid> emptyList = [];
        return Task.FromResult(emptyList);
    }

    private static bool IsActiveAndNotExpired(Announcement announcement, DateTime now)
    {
        if (announcement.Status != AnnouncementStatus.Published)
        {
            return false;
        }

        if (announcement.ExpiresAt < now)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesTarget(Announcement announcement, UserContext userContext)
    {
        return announcement.Target switch
        {
            AnnouncementTarget.All => true,
            AnnouncementTarget.Tenant => MatchesTenant(announcement, userContext),
            AnnouncementTarget.Plan => MatchesPlan(announcement, userContext),
            AnnouncementTarget.Role => MatchesRole(announcement, userContext),
            _ => false
        };
    }

    private static bool MatchesTenant(Announcement announcement, UserContext userContext)
    {
        if (string.IsNullOrEmpty(announcement.TargetValue))
        {
            return false;
        }

        return Guid.TryParse(announcement.TargetValue, out Guid targetTenantId)
               && targetTenantId == userContext.TenantId.Value;
    }

    private static bool MatchesPlan(Announcement announcement, UserContext userContext)
    {
        if (string.IsNullOrEmpty(announcement.TargetValue) || string.IsNullOrEmpty(userContext.PlanName))
        {
            return false;
        }

        return announcement.TargetValue.Equals(userContext.PlanName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesRole(Announcement announcement, UserContext userContext)
    {
        if (string.IsNullOrEmpty(announcement.TargetValue))
        {
            return false;
        }

        return userContext.Roles.Any(r =>
            r.Equals(announcement.TargetValue, StringComparison.OrdinalIgnoreCase));
    }

    private static AnnouncementDto MapToDto(Announcement announcement)
    {
        return new AnnouncementDto(
            announcement.Id.Value,
            announcement.Title,
            announcement.Content,
            announcement.Type,
            announcement.Target,
            announcement.TargetValue,
            announcement.PublishAt,
            announcement.ExpiresAt,
            announcement.IsPinned,
            announcement.IsDismissible,
            announcement.ActionUrl,
            announcement.ActionLabel,
            announcement.ImageUrl,
            announcement.Status,
            announcement.CreatedAt);
    }
}
