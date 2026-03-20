using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Announcements.Commands.UpdateAnnouncement;

public sealed record UpdateAnnouncementCommand(
    Guid Id,
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

public sealed class UpdateAnnouncementHandler(
    IAnnouncementRepository repository,
    TimeProvider timeProvider)
{
    public async Task<Result<AnnouncementDto>> Handle(UpdateAnnouncementCommand command, CancellationToken ct)
    {
        Announcement? announcement = await repository.GetByIdAsync(AnnouncementId.Create(command.Id), ct);
        if (announcement is null)
        {
            return Result.Failure<AnnouncementDto>(Error.NotFound("Announcement.NotFound", "Announcement not found"));
        }

        announcement.Update(
            command.Title,
            command.Content,
            command.Type,
            command.Target,
            command.TargetValue,
            command.PublishAt,
            command.ExpiresAt,
            command.IsPinned,
            command.IsDismissible,
            command.ActionUrl,
            command.ActionLabel,
            command.ImageUrl,
            timeProvider);

        await repository.UpdateAsync(announcement, ct);

        return Result.Success(MapToDto(announcement));
    }

    private static AnnouncementDto MapToDto(Announcement a) => new(
        a.Id.Value, a.Title, a.Content, a.Type, a.Target, a.TargetValue,
        a.PublishAt, a.ExpiresAt, a.IsPinned, a.IsDismissible,
        a.ActionUrl, a.ActionLabel, a.ImageUrl, a.Status, a.CreatedAt);
}
