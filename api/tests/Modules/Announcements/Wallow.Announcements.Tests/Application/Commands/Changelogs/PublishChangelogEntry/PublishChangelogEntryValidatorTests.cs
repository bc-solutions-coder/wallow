using FluentValidation.TestHelper;
using Wallow.Announcements.Application.Changelogs.Commands.PublishChangelogEntry;

namespace Wallow.Announcements.Tests.Application.Commands.Changelogs.PublishChangelogEntry;

public class PublishChangelogEntryValidatorTests
{
    private readonly PublishChangelogEntryValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Id_Is_Valid()
    {
        PublishChangelogEntryCommand command = new(Guid.NewGuid());
        TestValidationResult<PublishChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Id_Is_Empty()
    {
        PublishChangelogEntryCommand command = new(Guid.Empty);
        TestValidationResult<PublishChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}
