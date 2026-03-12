using FluentValidation.TestHelper;
using Foundry.Showcases.Application.Commands.CreateShowcase;
using Foundry.Showcases.Domain.Enums;

namespace Foundry.Showcases.Tests.Application.Commands.CreateShowcase;

public class CreateShowcaseValidatorTests
{
    private readonly CreateShowcaseValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Title_Is_Empty()
    {
        CreateShowcaseCommand command = new(
            Title: "",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<CreateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required");
    }

    [Fact]
    public void Should_Have_Error_When_Title_Exceeds_200_Characters()
    {
        CreateShowcaseCommand command = new(
            Title: new string('a', 201),
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<CreateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title must not exceed 200 characters");
    }

    [Fact]
    public void Should_Have_Error_When_No_Urls_Provided()
    {
        CreateShowcaseCommand command = new(
            Title: "My Showcase",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: null,
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<CreateShowcaseCommand> result = _validator.TestValidate(command);

        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Should_Have_Error_When_Category_Is_Invalid()
    {
        CreateShowcaseCommand command = new(
            Title: "My Showcase",
            Description: null,
            Category: (ShowcaseCategory)999,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<CreateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Category)
            .WithErrorMessage("Category must be a valid value");
    }

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        CreateShowcaseCommand command = new(
            Title: "My Showcase",
            Description: "Description",
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: "https://github.com/example",
            VideoUrl: null);

        TestValidationResult<CreateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Only_DemoUrl_Provided()
    {
        CreateShowcaseCommand command = new(
            Title: "My Showcase",
            Description: null,
            Category: ShowcaseCategory.Api,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        TestValidationResult<CreateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Only_GitHubUrl_Provided()
    {
        CreateShowcaseCommand command = new(
            Title: "My Showcase",
            Description: null,
            Category: ShowcaseCategory.Library,
            DemoUrl: null,
            GitHubUrl: "https://github.com/example",
            VideoUrl: null);

        TestValidationResult<CreateShowcaseCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
