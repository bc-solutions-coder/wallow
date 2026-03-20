using Wallow.Shared.Kernel.Results;
using Wallow.Showcases.Application.Commands.CreateShowcase;
using Wallow.Showcases.Application.Contracts;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;
using Wallow.Showcases.Domain.Identity;

namespace Wallow.Showcases.Tests.Application.Commands.CreateShowcase;

public class CreateShowcaseHandlerTests
{
    private readonly IShowcaseRepository _repository;
    private readonly CreateShowcaseHandler _handler;

    public CreateShowcaseHandlerTests()
    {
        _repository = Substitute.For<IShowcaseRepository>();
        _handler = new CreateShowcaseHandler(_repository);
    }

    [Fact]
    public async Task Handle_WithValidData_ReturnsSuccessWithShowcaseId()
    {
        CreateShowcaseCommand command = new(
            Title: "My Showcase",
            Description: "A great showcase",
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        Result<ShowcaseId> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithValidData_CallsRepositoryAddAsync()
    {
        CreateShowcaseCommand command = new(
            Title: "My Showcase",
            Description: null,
            Category: ShowcaseCategory.Api,
            DemoUrl: null,
            GitHubUrl: "https://github.com/example",
            VideoUrl: null);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<Showcase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidTitle_ReturnsFailure()
    {
        CreateShowcaseCommand command = new(
            Title: "",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        Result<ShowcaseId> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Showcase.TitleRequired");
    }

    [Fact]
    public async Task Handle_WithNoUrls_ReturnsFailure()
    {
        CreateShowcaseCommand command = new(
            Title: "My Showcase",
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: null,
            GitHubUrl: null,
            VideoUrl: null);

        Result<ShowcaseId> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Showcase.UrlRequired");
    }

    [Fact]
    public async Task Handle_WithTagsAndDisplayOrder_PersistsShowcaseWithValues()
    {
        Showcase? capturedShowcase = null;
        await _repository.AddAsync(Arg.Do<Showcase>(s => capturedShowcase = s), Arg.Any<CancellationToken>());

        CreateShowcaseCommand command = new(
            Title: "Tagged Showcase",
            Description: "With tags",
            Category: ShowcaseCategory.Tool,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null,
            Tags: ["dotnet", "csharp"],
            DisplayOrder: 7,
            IsPublished: true);

        Result<ShowcaseId> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedShowcase.Should().NotBeNull();
        capturedShowcase!.Tags.Should().BeEquivalentTo(["dotnet", "csharp"]);
        capturedShowcase.DisplayOrder.Should().Be(7);
        capturedShowcase.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithTitleTooLong_ReturnsFailure()
    {
        CreateShowcaseCommand command = new(
            Title: new string('a', 201),
            Description: null,
            Category: ShowcaseCategory.WebApp,
            DemoUrl: "https://demo.example.com",
            GitHubUrl: null,
            VideoUrl: null);

        Result<ShowcaseId> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Showcase.TitleTooLong");
    }
}
