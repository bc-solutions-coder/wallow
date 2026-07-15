using FluentValidation;

namespace Wallow.Announcements.Application.Announcements.Commands.DismissAnnouncement;

public sealed class DismissAnnouncementValidator : AbstractValidator<DismissAnnouncementCommand>
{
    public DismissAnnouncementValidator()
    {
        RuleFor(x => x.AnnouncementId)
            .NotEmpty().WithMessage("Announcement ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}
