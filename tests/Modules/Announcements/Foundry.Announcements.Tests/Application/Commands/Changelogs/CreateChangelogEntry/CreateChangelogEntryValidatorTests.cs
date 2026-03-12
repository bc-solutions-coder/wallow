using FluentValidation.TestHelper;
using Foundry.Announcements.Application.Changelogs.Commands.CreateChangelogEntry;

namespace Foundry.Announcements.Tests.Application.Commands.Changelogs.CreateChangelogEntry;

public class CreateChangelogEntryValidatorTests
{
    private readonly CreateChangelogEntryValidator _validator = new();

    private static CreateChangelogEntryCommand Valid() => new(
        "1.0.0",
        "Initial Release",
        "First release of the platform",
        DateTime.UtcNow);

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        CreateChangelogEntryCommand command = Valid();
        TestValidationResult<CreateChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Title_Is_Empty()
    {
        CreateChangelogEntryCommand command = Valid() with { Title = "" };
        TestValidationResult<CreateChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_Title_Exceeds_200_Characters()
    {
        CreateChangelogEntryCommand command = Valid() with { Title = new string('x', 201) };
        TestValidationResult<CreateChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_Content_Is_Empty()
    {
        CreateChangelogEntryCommand command = Valid() with { Content = "" };
        TestValidationResult<CreateChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Should_Have_Error_When_Content_Exceeds_10000_Characters()
    {
        CreateChangelogEntryCommand command = Valid() with { Content = new string('x', 10001) };
        TestValidationResult<CreateChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Should_Have_Error_When_Version_Is_Empty()
    {
        CreateChangelogEntryCommand command = Valid() with { Version = "" };
        TestValidationResult<CreateChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Version);
    }

    [Fact]
    public void Should_Have_Error_When_Version_Is_Not_SemVer()
    {
        CreateChangelogEntryCommand command = Valid() with { Version = "not-a-version" };
        TestValidationResult<CreateChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Version);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Version_Has_Prerelease()
    {
        CreateChangelogEntryCommand command = Valid() with { Version = "1.0.0-beta.1" };
        TestValidationResult<CreateChangelogEntryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Version);
    }
}
