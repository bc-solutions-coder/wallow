using Foundry.Showcases.Application.Contracts;
using Foundry.Showcases.Application.Queries.GetShowcases;
using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Showcases.Tests.Application.Queries.GetShowcases;

public class GetShowcasesHandlerTests
{
    private readonly IShowcaseRepository _repository;
    private readonly GetShowcasesHandler _handler;

    public GetShowcasesHandlerTests()
    {
        _repository = Substitute.For<IShowcaseRepository>();
        _handler = new GetShowcasesHandler(_repository);
    }

    private static Showcase CreateValidShowcase(string title, ShowcaseCategory category)
    {
        Result<Showcase> result = Showcase.Create(
            title,
            null,
            category,
            demoUrl: "https://demo.example.com",
            gitHubUrl: null,
            videoUrl: null);
        return result.Value;
    }

    [Fact]
    public async Task Handle_ReturnsAllShowcases()
    {
        Showcase showcase1 = CreateValidShowcase("Showcase 1", ShowcaseCategory.WebApp);
        Showcase showcase2 = CreateValidShowcase("Showcase 2", ShowcaseCategory.Api);
        IReadOnlyList<Showcase> showcases = new List<Showcase> { showcase1, showcase2 };

        _repository.GetAllAsync(null, null, Arg.Any<CancellationToken>()).Returns(showcases);

        GetShowcasesQuery query = new(Category: null, Tag: null);

        Result<IReadOnlyList<ShowcaseDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Title.Should().Be("Showcase 1");
        result.Value[1].Title.Should().Be("Showcase 2");
    }

    [Fact]
    public async Task Handle_WithNoShowcases_ReturnsEmptyList()
    {
        _repository.GetAllAsync(null, null, Arg.Any<CancellationToken>()).Returns(new List<Showcase>());

        GetShowcasesQuery query = new(Category: null, Tag: null);

        Result<IReadOnlyList<ShowcaseDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesCategoryAndTagToRepository()
    {
        ShowcaseCategory category = ShowcaseCategory.WebApp;
        string tag = "dotnet";
        _repository.GetAllAsync(category, tag, Arg.Any<CancellationToken>()).Returns(new List<Showcase>());

        GetShowcasesQuery query = new(Category: category, Tag: tag);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetAllAsync(category, tag, Arg.Any<CancellationToken>());
    }
}
