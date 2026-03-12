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

    [Fact]
    public async Task Handle_MapsAllDtoFieldsCorrectly()
    {
        Result<Showcase> createResult = Showcase.Create(
            "Full Showcase",
            "Full description",
            ShowcaseCategory.Library,
            demoUrl: "https://demo.example.com",
            gitHubUrl: "https://github.com/example",
            videoUrl: "https://video.example.com",
            tags: ["blazor", "wasm"],
            displayOrder: 5,
            isPublished: true);
        Showcase showcase = createResult.Value;

        _repository.GetAllAsync(null, null, Arg.Any<CancellationToken>()).Returns(new List<Showcase> { showcase });

        GetShowcasesQuery query = new(Category: null, Tag: null);

        Result<IReadOnlyList<ShowcaseDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ShowcaseDto dto = result.Value[0];
        dto.Id.Should().Be(showcase.Id);
        dto.Title.Should().Be("Full Showcase");
        dto.Description.Should().Be("Full description");
        dto.Category.Should().Be(ShowcaseCategory.Library);
        dto.DemoUrl.Should().Be("https://demo.example.com");
        dto.GitHubUrl.Should().Be("https://github.com/example");
        dto.VideoUrl.Should().Be("https://video.example.com");
        dto.Tags.Should().BeEquivalentTo(["blazor", "wasm"]);
        dto.DisplayOrder.Should().Be(5);
        dto.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithCategoryOnly_PassesNullTag()
    {
        _repository.GetAllAsync(ShowcaseCategory.Api, null, Arg.Any<CancellationToken>()).Returns(new List<Showcase>());

        GetShowcasesQuery query = new(Category: ShowcaseCategory.Api, Tag: null);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetAllAsync(ShowcaseCategory.Api, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithTagOnly_PassesNullCategory()
    {
        _repository.GetAllAsync(null, "dotnet", Arg.Any<CancellationToken>()).Returns(new List<Showcase>());

        GetShowcasesQuery query = new(Category: null, Tag: "dotnet");

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetAllAsync(null, "dotnet", Arg.Any<CancellationToken>());
    }
}
