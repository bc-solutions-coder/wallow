using FluentValidation.TestHelper;
using Foundry.Showcases.Application.Commands.UpdateShowcase;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;

namespace Foundry.Showcases.Tests.Application.Commands.UpdateShowcase;

public class UpdateShowcaseValidatorTests
{
    private readonly UpdateShowcaseValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_ShowcaseId_Is_Empty()
    {
        UpdateShowcaseCommand command = new(
            ShowcaseId: new ShowcaseId(Guid.Empty),
            Title: "My Showcase",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<UpdateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ShowcaseId)
            .WithErrorMessage("ShowcaseId is required");
    }

    [Fact]
    public void Should_Have_Error_When_Title_Is_Empty()
    {
        UpdateShowcaseCommand command = new(
            ShowcaseId: ShowcaseId.New(),
            Title: "",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<UpdateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required");
    }

    [Fact]
    public void Should_Have_Error_When_Title_Exceeds_200_Characters()
    {
        UpdateShowcaseCommand command = new(
            ShowcaseId: ShowcaseId.New(),
            Title: new string('a', 201),
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<UpdateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title must not exceed 200 characters");
    }

    [Fact]
    public void Should_Have_Error_When_No_Urls_Provided()
    {
        UpdateShowcaseCommand command = new(
            ShowcaseId: ShowcaseId.New(),
            Title: "My Showcase",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: null,
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<UpdateShowcaseCommand> result = _validator.TestValidate(command);

        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Should_Have_Error_When_Category_Is_Invalid()
    {
        UpdateShowcaseCommand command = new(
            ShowcaseId: ShowcaseId.New(),
            Title: "My Showcase",
            Description: null,
            Category: (ShowcaseCategory)999,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<UpdateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Category)
            .WithErrorMessage("Category must be a valid value");
    }

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        UpdateShowcaseCommand command = new(
            ShowcaseId: ShowcaseId.New(),
            Title: "My Showcase",
            Description: "A description",
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: "https://github.com/example",
            VideoUrl: null);

        TestValidationResult<UpdateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
