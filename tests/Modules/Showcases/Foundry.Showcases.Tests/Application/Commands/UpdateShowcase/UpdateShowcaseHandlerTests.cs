using Foundry.Shared.Kernel.Results;
using Foundry.Showcases.Application.Commands.UpdateShowcase;
using Foundry.Showcases.Application.Contracts;
using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;

namespace Foundry.Showcases.Tests.Application.Commands.UpdateShowcase;

public class UpdateShowcaseHandlerTests
{
    private readonly IShowcaseRepository _repository;
    private readonly UpdateShowcaseHandler _handler;

    public UpdateShowcaseHandlerTests()
    {
        _repository = Substitute.For<IShowcaseRepository>();
        _handler = new UpdateShowcaseHandler(_repository);
    }

    private static Showcase CreateValidShowcase()
    {
        Result<Showcase> result = Showcase.Create(
            "Original Title",
            "Description",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null);
        return result.Value;
    }

    [Fact]
    public async Task Handle_WhenShowcaseExists_UpdatesAndReturnsSuccess()
    {
        Showcase showcase = CreateValidShowcase();
        _repository.GetByIdAsync(showcase.Id, Arg.Any<CancellationToken>()).Returns(showcase);

        UpdateShowcaseCommand command = new(
            ShowcaseId: showcase.Id,
            Title: "Updated Title",
            Description: "Updated Description",
            Category: ShowcaseCategory.Api,
            DemoUrl: null,
            GitHubUrl: "https://github.com/updated",
            VideoUrl: null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(showcase, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenShowcaseNotFound_ReturnsNotFoundError()
    {
        ShowcaseId id = ShowcaseId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Showcase?)null);

        UpdateShowcaseCommand command = new(
            ShowcaseId: id,
            Title: "Updated Title",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WithInvalidData_ReturnsFailure()
    {
        Showcase showcase = CreateValidShowcase();
        _repository.GetByIdAsync(showcase.Id, Arg.Any<CancellationToken>()).Returns(showcase);

        UpdateShowcaseCommand command = new(
            ShowcaseId: showcase.Id,
            Title: "",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Showcase.TitleRequired");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Showcase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNoUrls_ReturnsFailure()
    {
        Showcase showcase = CreateValidShowcase();
        _repository.GetByIdAsync(showcase.Id, Arg.Any<CancellationToken>()).Returns(showcase);

        UpdateShowcaseCommand command = new(
            ShowcaseId: showcase.Id,
            Title: "Updated Title",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: null,
            GitHubUrl: null,
            VideoUrl: null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Showcase.UrlRequired");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Showcase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UpdatesEntityWithCorrectValues()
    {
        Showcase showcase = CreateValidShowcase();
        _repository.GetByIdAsync(showcase.Id, Arg.Any<CancellationToken>()).Returns(showcase);

        UpdateShowcaseCommand command = new(
            ShowcaseId: showcase.Id,
            Title: "New Title",
            Description: "New Description",
            Category: ShowcaseCategory.Mobile,
            DemoUrl: null,
            GitHubUrl: "https://github.com/new",
            VideoUrl: "https://video.example.com",
            Tags: ["blazor"],
            DisplayOrder: 10,
            IsPublished: true);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        showcase.Title.Should().Be("New Title");
        showcase.Description.Should().Be("New Description");
        showcase.Category.Should().Be(ShowcaseCategory.Mobile);
        showcase.GitHubUrl.Should().Be("https://github.com/new");
        showcase.VideoUrl.Should().Be("https://video.example.com");
        showcase.Tags.Should().BeEquivalentTo(["blazor"]);
        showcase.DisplayOrder.Should().Be(10);
        showcase.IsPublished.Should().BeTrue();
    }
}
