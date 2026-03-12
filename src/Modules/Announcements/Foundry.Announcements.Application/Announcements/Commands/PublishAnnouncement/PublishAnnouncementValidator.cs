using FluentValidation;

namespace Foundry.Announcements.Application.Announcements.Commands.PublishAnnouncement;

public sealed class PublishAnnouncementValidator : AbstractValidator<PublishAnnouncementCommand>
{
    public PublishAnnouncementValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Announcement ID is required");
    }
}
