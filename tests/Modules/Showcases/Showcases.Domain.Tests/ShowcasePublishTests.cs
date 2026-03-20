using Wallow.Shared.Kernel.Results;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;

namespace Wallow.Showcases.Domain.Tests;

public class ShowcasePublishTests
{
    private static Showcase CreateUnpublishedShowcase()
    {
        Result<Showcase> result = Showcase.Create(
            "Test Showcase",
            "Description",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            isPublished: false);

        return result.Value;
    }

    private static Showcase CreatePublishedShowcase()
    {
        Result<Showcase> result = Showcase.Create(
            "Test Showcase",
            "Description",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            isPublished: true);

        return result.Value;
    }

    [Fact]
    public void Create_WithIsPublishedFalse_DefaultsToUnpublished()
    {
        Result<Showcase> result = Showcase.Create(
            "Title",
            null,
            ShowcaseCategory.Api,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsPublished.Should().BeFalse();
    }

    [Fact]
    public void Create_WithIsPublishedTrue_CreatesPublishedShowcase()
    {
        Result<Showcase> result = Showcase.Create(
            "Title",
            null,
            ShowcaseCategory.Api,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            isPublished: true);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsPublished.Should().BeTrue();
    }

    [Fact]
    public void Update_FromUnpublishedToPublished_SetsIsPublishedTrue()
    {
        Showcase showcase = CreateUnpublishedShowcase();
        showcase.IsPublished.Should().BeFalse();

        Result updateResult = showcase.Update(
            "Test Showcase",
            "Description",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            isPublished: true);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.IsPublished.Should().BeTrue();
    }

    [Fact]
    public void Update_FromPublishedToUnpublished_SetsIsPublishedFalse()
    {
        Showcase showcase = CreatePublishedShowcase();
        showcase.IsPublished.Should().BeTrue();

        Result updateResult = showcase.Update(
            "Test Showcase",
            "Description",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            isPublished: false);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.IsPublished.Should().BeFalse();
    }

    [Fact]
    public void Update_PublishedToPublished_RemainsPublished()
    {
        Showcase showcase = CreatePublishedShowcase();

        Result updateResult = showcase.Update(
            "Updated Title",
            "Updated Desc",
            ShowcaseCategory.Mobile,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            isPublished: true);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.IsPublished.Should().BeTrue();
    }

    [Fact]
    public void Update_UnpublishedToUnpublished_RemainsUnpublished()
    {
        Showcase showcase = CreateUnpublishedShowcase();

        Result updateResult = showcase.Update(
            "Updated Title",
            "Updated Desc",
            ShowcaseCategory.Mobile,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null,
            isPublished: false);

        updateResult.IsSuccess.Should().BeTrue();
        showcase.IsPublished.Should().BeFalse();
    }
}
