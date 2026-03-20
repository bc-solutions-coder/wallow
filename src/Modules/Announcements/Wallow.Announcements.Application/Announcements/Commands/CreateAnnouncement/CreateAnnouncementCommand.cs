using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Announcements.Commands.CreateAnnouncement;

public sealed record CreateAnnouncementCommand(
    string Title,
    string Content,
    AnnouncementType Type,
    AnnouncementTarget Target,
    string? TargetValue,
    DateTime? PublishAt,
    DateTime? ExpiresAt,
    bool IsPinned,
    bool IsDismissible,
    string? ActionUrl,
    string? ActionLabel,
    string? ImageUrl);

public sealed class CreateAnnouncementHandler(
    IAnnouncementRepository repository,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task<Result<AnnouncementDto>> Handle(CreateAnnouncementCommand command, CancellationToken ct)
    {
        Announcement announcement = Announcement.Create(
            tenantContext.TenantId,
            command.Title,
            command.Content,
            command.Type,
            timeProvider,
            command.Target,
            command.TargetValue,
            command.PublishAt,
            command.ExpiresAt,
            command.IsPinned,
            command.IsDismissible,
            command.ActionUrl,
            command.ActionLabel,
            command.ImageUrl);

        await repository.AddAsync(announcement, ct);

        return Result.Success(MapToDto(announcement));
    }

    private static AnnouncementDto MapToDto(Announcement a) => new(
        a.Id.Value, a.Title, a.Content, a.Type, a.Target, a.TargetValue,
        a.PublishAt, a.ExpiresAt, a.IsPinned, a.IsDismissible,
        a.ActionUrl, a.ActionLabel, a.ImageUrl, a.Status, a.CreatedAt);
}
