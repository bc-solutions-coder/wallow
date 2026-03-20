using Wallow.Shared.Kernel.Results;
using Wallow.Showcases.Application.Commands.DeleteShowcase;
using Wallow.Showcases.Application.Contracts;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;
using Wallow.Showcases.Domain.Identity;

namespace Wallow.Showcases.Tests.Application.Commands.DeleteShowcase;

public class DeleteShowcaseHandlerTests
{
    private readonly IShowcaseRepository _repository;
    private readonly DeleteShowcaseHandler _handler;

    public DeleteShowcaseHandlerTests()
    {
        _repository = Substitute.For<IShowcaseRepository>();
        _handler = new DeleteShowcaseHandler(_repository);
    }

    private static Showcase CreateValidShowcase()
    {
        Result<Showcase> result = Showcase.Create(
            "Title",
            null,
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null);
        return result.Value;
    }

    [Fact]
    public async Task Handle_WhenShowcaseExists_DeletesAndReturnsSuccess()
    {
        Showcase showcase = CreateValidShowcase();
        _repository.GetByIdAsync(showcase.Id, Arg.Any<CancellationToken>()).Returns(showcase);

        DeleteShowcaseCommand command = new(showcase.Id);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(showcase.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenShowcaseNotFound_ReturnsNotFoundError()
    {
        ShowcaseId id = ShowcaseId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Showcase?)null);

        DeleteShowcaseCommand command = new(id);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<ShowcaseId>(), Arg.Any<CancellationToken>());
    }
}
