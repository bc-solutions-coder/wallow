using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Showcases.Domain.Tests;

public class ShowcaseCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSuccessWithAllFields()
    {
        Result<Showcase> result = Showcase.Create(
            "My Showcase",
            "A description",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: "https://github.com/example",
            videoUrl: null,
            tags: ["dotnet", "blazor"],
            displayOrder: 5,
            isPublished: true);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("My Showcase");
        result.Value.Description.Should().Be("A description");
        result.Value.Category.Should().Be(ShowcaseCategory.WebApp);
        result.Value.DemoUrl.Should().Be("https://demo.example.com");
        result.Value.GitHubUrl.Should().Be("https://github.com/example");
        result.Value.VideoUrl.Should().BeNull();
        result.Value.Tags.Should().BeEquivalentTo(["dotnet", "blazor"]);
        result.Value.DisplayOrder.Should().Be(5);
        result.Value.IsPublished.Should().BeTrue();
        result.Value.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithOnlyDemoUrl_Succeeds()
    {
        Result<Showcase> result = Showcase.Create("Title", null, ShowcaseCategory.Api, demoUrl: "https://demo.example.com", gitHubUrl: null, videoUrl: null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithOnlyGitHubUrl_Succeeds()
    {
        Result<Showcase> result = Showcase.Create("Title", null, ShowcaseCategory.Api, demoUrl: null, gitHubUrl: "https://github.com/example", videoUrl: null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithOnlyVideoUrl_Succeeds()
    {
        Result<Showcase> result = Showcase.Create("Title", null, ShowcaseCategory.Api, demoUrl: null, gitHubUrl: null, videoUrl: "https://youtube.com/watch?v=123");

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyTitle_ReturnsFailure(string? title)
    {
        Result<Showcase> result = Showcase.Create(title!, null, ShowcaseCategory.WebApp, demoUrl: "https://demo.example.com", gitHubUrl: null, videoUrl: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Showcase.TitleRequired");
    }

    [Fact]
    public void Create_WithTitleExceeding200Chars_ReturnsFailure()
    {
        string longTitle = new string('a', 201);

        Result<Showcase> result = Showcase.Create(longTitle, null, ShowcaseCategory.WebApp, demoUrl: "https://demo.example.com", gitHubUrl: null, videoUrl: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Showcase.TitleTooLong");
    }

    [Fact]
    public void Create_WithNoUrls_ReturnsFailure()
    {
        Result<Showcase> result = Showcase.Create("Title", null, ShowcaseCategory.WebApp, demoUrl: null, gitHubUrl: null, videoUrl: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Showcase.UrlRequired");
    }

    [Fact]
    public void Create_DisplayOrderDefaultsToZero()
    {
        Result<Showcase> result = Showcase.Create("Title", null, ShowcaseCategory.WebApp, demoUrl: "https://demo.example.com", gitHubUrl: null, videoUrl: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayOrder.Should().Be(0);
    }
}

public class ShowcaseUpdateTests
{
    private static Showcase CreateValidShowcase()
    {
        Result<Showcase> result = Showcase.Create("Original", "Desc", ShowcaseCategory.WebApp, demoUrl: "https://demo.example.com", gitHubUrl: null, videoUrl: null);
        return result.Value;
    }

    [Fact]
    public void Update_WithValidData_UpdatesAllFields()
    {
        Showcase showcase = CreateValidShowcase();

        Result updateResult = showcase.Update(
            "Updated Title",
            "Updated Desc",
            ShowcaseCategory.Mobile,
            demoUrl: null,
            gitHubUrl: "https://github.com/updated",
            videoUrl: null,
            tags: ["new-tag"],
            displayOrder: 10,
            isPublished: true);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.Title.Should().Be("Updated Title");
        showcase.Description.Should().Be("Updated Desc");
        showcase.Category.Should().Be(ShowcaseCategory.Mobile);
        showcase.DemoUrl.Should().BeNull();
        showcase.GitHubUrl.Should().Be("https://github.com/updated");
        showcase.Tags.Should().BeEquivalentTo(["new-tag"]);
        showcase.DisplayOrder.Should().Be(10);
        showcase.IsPublished.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Update_WithEmptyTitle_ReturnsFailure(string? title)
    {
        Showcase showcase = CreateValidShowcase();

        Result updateResult = showcase.Update(title!, null, ShowcaseCategory.WebApp, demoUrl: "https://demo.example.com", gitHubUrl: null, videoUrl: null);

        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("Showcase.TitleRequired");
    }

    [Fact]
    public void Update_WithTitleExceeding200Chars_ReturnsFailure()
    {
        Showcase showcase = CreateValidShowcase();
        string longTitle = new string('a', 201);

        Result updateResult = showcase.Update(longTitle, null, ShowcaseCategory.WebApp, demoUrl: "https://demo.example.com", gitHubUrl: null, videoUrl: null);

        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("Showcase.TitleTooLong");
    }

    [Fact]
    public void Update_WithNoUrls_ReturnsFailure()
    {
        Showcase showcase = CreateValidShowcase();

        Result updateResult = showcase.Update("Title", null, ShowcaseCategory.WebApp, demoUrl: null, gitHubUrl: null, videoUrl: null);

        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("Showcase.UrlRequired");
    }
}
