using FluentValidation.TestHelper;
using Foundry.Announcements.Application.Announcements.Commands.ArchiveAnnouncement;

namespace Foundry.Announcements.Tests.Application.Commands.ArchiveAnnouncement;

public class ArchiveAnnouncementValidatorTests
{
    private readonly ArchiveAnnouncementValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Id_Is_Valid()
    {
        ArchiveAnnouncementCommand command = new(Guid.NewGuid());
        TestValidationResult<ArchiveAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Id_Is_Empty()
    {
        ArchiveAnnouncementCommand command = new(Guid.Empty);
        TestValidationResult<ArchiveAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}
