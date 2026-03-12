using FluentValidation.TestHelper;
using Foundry.Announcements.Application.Announcements.Commands.DismissAnnouncement;

namespace Foundry.Announcements.Tests.Application.Commands.DismissAnnouncement;

public class DismissAnnouncementValidatorTests
{
    private readonly DismissAnnouncementValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        DismissAnnouncementCommand command = new(Guid.NewGuid(), Guid.NewGuid());
        TestValidationResult<DismissAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_AnnouncementId_Is_Empty()
    {
        DismissAnnouncementCommand command = new(Guid.Empty, Guid.NewGuid());
        TestValidationResult<DismissAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AnnouncementId);
    }

    [Fact]
    public void Should_Have_Error_When_UserId_Is_Empty()
    {
        DismissAnnouncementCommand command = new(Guid.NewGuid(), Guid.Empty);
        TestValidationResult<DismissAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
