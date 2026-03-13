using Foundry.Shared.Kernel.Results;
using Foundry.Showcases.Application.Contracts;
using Foundry.Showcases.Application.Queries.GetShowcase;
using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;

namespace Foundry.Showcases.Tests.Application.Queries.GetShowcase;

public class GetShowcaseHandlerTests
{
    private readonly IShowcaseRepository _repository;
    private readonly GetShowcaseHandler _handler;

    public GetShowcaseHandlerTests()
    {
        _repository = Substitute.For<IShowcaseRepository>();
        _handler = new GetShowcaseHandler(_repository);
    }

    private static Showcase CreateValidShowcase()
    {
        Result<Showcase> result = Showcase.Create(
            "My Showcase",
            "A description",
            ShowcaseCategory.WebApp,
            demoUrl: "https://demo.example.com",
            gitHubUrl: "https://github.com/example",
            videoUrl: null,
            tags: ["dotnet", "csharp"],
            displayOrder: 3,
            isPublished: true);
        return result.Value;
    }

    [Fact]
    public async Task Handle_WhenShowcaseExists_ReturnsDto()
    {
        Showcase showcase = CreateValidShowcase();
        _repository.GetByIdAsync(showcase.Id, Arg.Any<CancellationToken>()).Returns(showcase);

        GetShowcaseQuery query = new(showcase.Id);

        Result<ShowcaseDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(showcase.Id);
        result.Value.Title.Should().Be("My Showcase");
        result.Value.Description.Should().Be("A description");
        result.Value.Category.Should().Be(ShowcaseCategory.WebApp);
        result.Value.DemoUrl.Should().Be("https://demo.example.com");
        result.Value.GitHubUrl.Should().Be("https://github.com/example");
        result.Value.VideoUrl.Should().BeNull();
        result.Value.Tags.Should().BeEquivalentTo(["dotnet", "csharp"]);
        result.Value.DisplayOrder.Should().Be(3);
        result.Value.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenShowcaseNotFound_ReturnsNotFoundError()
    {
        ShowcaseId id = ShowcaseId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Showcase?)null);

        GetShowcaseQuery query = new(id);

        Result<ShowcaseDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }
}
