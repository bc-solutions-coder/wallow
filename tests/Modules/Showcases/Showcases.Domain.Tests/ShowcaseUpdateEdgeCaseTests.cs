using Wallow.Shared.Kernel.Results;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;

namespace Wallow.Showcases.Domain.Tests;

public class ShowcaseUpdateEdgeCaseTests
{
    private static Showcase CreateShowcaseWithTags()
    {
        Result<Showcase> result = Showcase.Create(
            "Original",
            "Description",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            tags: ["dotnet", "blazor"]);

        return result.Value;
    }

    private static Showcase CreateValidShowcase()
    {
        Result<Showcase> result = Showcase.Create(
            "Original",
            "Description",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null);

        return result.Value;
    }

    [Fact]
    public void Update_WithNullTags_ClearsExistingTags()
    {
        Showcase showcase = CreateShowcaseWithTags();
        showcase.Tags.Should().HaveCount(2);

        Result updateResult = showcase.Update(
            "Updated",
            "Desc",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            tags: null);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithEmptyTagsList_ClearsExistingTags()
    {
        Showcase showcase = CreateShowcaseWithTags();
        showcase.Tags.Should().HaveCount(2);

        Result updateResult = showcase.Update(
            "Updated",
            "Desc",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            tags: []);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithBoundaryLengthTitle_Succeeds()
    {
        Showcase showcase = CreateValidShowcase();
        string boundaryTitle = new string('a', 200);

        Result updateResult = showcase.Update(
            boundaryTitle,
            "Desc",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.Title.Should().Be(boundaryTitle);
    }

    [Fact]
    public void Create_WithBoundaryLengthTitle_Succeeds()
    {
        string boundaryTitle = new string('a', 200);

        Result<Showcase> result = Showcase.Create(
            boundaryTitle,
            null,
            ShowcaseCategory.Api,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be(boundaryTitle);
    }

    [Fact]
    public void Update_WithEmptyDescription_SetsDescriptionToNull()
    {
        Showcase showcase = CreateValidShowcase();
        showcase.Description.Should().Be("Description");

        Result updateResult = showcase.Update(
            "Updated",
            null,
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullTags_DefaultsToEmptyCollection()
    {
        Result<Showcase> result = Showcase.Create(
            "Title",
            null,
            ShowcaseCategory.Api,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            tags: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyTagsList_DefaultsToEmptyCollection()
    {
        Result<Showcase> result = Showcase.Create(
            "Title",
            null,
            ShowcaseCategory.Api,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            tags: []);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithWhitespaceDescription_TrimsToEmpty()
    {
        Showcase showcase = CreateValidShowcase();

        Result updateResult = showcase.Update(
            "Updated",
            "   ",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.Description.Should().BeEmpty();
    }
}
