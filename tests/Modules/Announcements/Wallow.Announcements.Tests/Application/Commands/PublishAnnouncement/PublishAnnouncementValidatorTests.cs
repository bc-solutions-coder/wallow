using FluentValidation.TestHelper;
using Wallow.Announcements.Application.Announcements.Commands.PublishAnnouncement;

namespace Wallow.Announcements.Tests.Application.Commands.PublishAnnouncement;

public class PublishAnnouncementValidatorTests
{
    private readonly PublishAnnouncementValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Id_Is_Valid()
    {
        PublishAnnouncementCommand command = new(Guid.NewGuid());
        TestValidationResult<PublishAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Id_Is_Empty()
    {
        PublishAnnouncementCommand command = new(Guid.Empty);
        TestValidationResult<PublishAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}
