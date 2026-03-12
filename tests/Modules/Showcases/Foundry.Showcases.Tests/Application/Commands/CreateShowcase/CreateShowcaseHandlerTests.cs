using Foundry.Showcases.Application.Commands.CreateShowcase;
using Foundry.Showcases.Application.Contracts;
using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Showcases.Tests.Application.Commands.CreateShowcase;

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
}
