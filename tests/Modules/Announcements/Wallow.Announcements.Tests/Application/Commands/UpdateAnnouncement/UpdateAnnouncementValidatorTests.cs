using FluentValidation.TestHelper;
using Wallow.Announcements.Application.Announcements.Commands.UpdateAnnouncement;
using Wallow.Announcements.Domain.Announcements.Enums;

namespace Wallow.Announcements.Tests.Application.Commands.UpdateAnnouncement;

public class UpdateAnnouncementValidatorTests
{
    private readonly UpdateAnnouncementValidator _validator = new();

    private static UpdateAnnouncementCommand Valid() => new(
        Guid.NewGuid(),
        "Test Announcement",
        "This is test content",
        AnnouncementType.Feature,
        AnnouncementTarget.All,
        null,
        null,
        null,
        false,
        true,
        null,
        null,
        null);

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        UpdateAnnouncementCommand command = Valid();
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Id_Is_Empty()
    {
        UpdateAnnouncementCommand command = Valid() with { Id = Guid.Empty };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Should_Have_Error_When_Title_Is_Empty()
    {
        UpdateAnnouncementCommand command = Valid() with { Title = "" };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_Title_Exceeds_200_Characters()
    {
        UpdateAnnouncementCommand command = Valid() with { Title = new string('x', 201) };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_Content_Is_Empty()
    {
        UpdateAnnouncementCommand command = Valid() with { Content = "" };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Should_Have_Error_When_Content_Exceeds_5000_Characters()
    {
        UpdateAnnouncementCommand command = Valid() with { Content = new string('x', 5001) };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Should_Have_Error_When_Content_Contains_Script_Tags()
    {
        UpdateAnnouncementCommand command = Valid() with { Content = "Hello <script>alert('xss')</script>" };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Should_Have_Error_When_Type_Is_Invalid()
    {
        UpdateAnnouncementCommand command = Valid() with { Type = (AnnouncementType)999 };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Should_Have_Error_When_Target_Is_Invalid()
    {
        UpdateAnnouncementCommand command = Valid() with { Target = (AnnouncementTarget)999 };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Target);
    }

    [Fact]
    public void Should_Have_Error_When_ActionUrl_Exceeds_2000_Characters()
    {
        UpdateAnnouncementCommand command = Valid() with { ActionUrl = new string('x', 2001) };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ActionUrl);
    }

    [Fact]
    public void Should_Have_Error_When_ActionLabel_Exceeds_100_Characters()
    {
        UpdateAnnouncementCommand command = Valid() with { ActionLabel = new string('x', 101) };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ActionLabel);
    }

    [Fact]
    public void Should_Have_Error_When_ImageUrl_Exceeds_2000_Characters()
    {
        UpdateAnnouncementCommand command = Valid() with { ImageUrl = new string('x', 2001) };
        TestValidationResult<UpdateAnnouncementCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ImageUrl);
    }
}
