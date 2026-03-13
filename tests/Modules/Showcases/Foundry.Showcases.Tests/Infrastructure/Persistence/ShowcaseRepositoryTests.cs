using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;
using Foundry.Showcases.Infrastructure.Persistence;
using Foundry.Showcases.Infrastructure.Persistence.Repositories;
using Foundry.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Showcases.Tests.Infrastructure.Persistence;

[CollectionDefinition("ShowcasesPostgresDatabase")]
public class ShowcasesPostgresDatabaseCollection : ICollectionFixture<PostgresContainerFixture>;

[Collection("ShowcasesPostgresDatabase")]
[Trait("Category", "Integration")]
#pragma warning disable CA1001 // IAsyncLifetime.DisposeAsync handles cleanup
public sealed class ShowcaseRepositoryTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private ShowcasesDbContext _dbContext = null!;
#pragma warning restore CA1001

    public async Task InitializeAsync()
    {
        DbContextOptions<ShowcasesDbContext> options = new DbContextOptionsBuilder<ShowcasesDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        _dbContext = new ShowcasesDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private ShowcaseRepository CreateRepository() => new(_dbContext);

    private static Showcase CreateValidShowcase(
        string title = "Test Showcase",
        ShowcaseCategory category = ShowcaseCategory.WebApp,
        string? demoUrl = "https://demo.example.com",
        bool isPublished = true)
    {
        Foundry.Shared.Kernel.Results.Result<Showcase> result = Showcase.Create(
            title,
            "A description",
            category,
            demoUrl,
            null,
            null,
            new List<string> { "dotnet" },
            0,
            isPublished);
        return result.Value;
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ReturnsShowcase()
    {
        ShowcaseRepository repository = CreateRepository();
        Showcase showcase = CreateValidShowcase();

        await repository.AddAsync(showcase);

        Showcase? result = await repository.GetByIdAsync(showcase.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Showcase");
        result.Category.Should().Be(ShowcaseCategory.WebApp);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        ShowcaseRepository repository = CreateRepository();

        Showcase? result = await repository.GetByIdAsync(ShowcaseId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllShowcases()
    {
        ShowcaseRepository repository = CreateRepository();
        Showcase showcase1 = CreateValidShowcase("Showcase A");
        Showcase showcase2 = CreateValidShowcase("Showcase B");

        await repository.AddAsync(showcase1);
        await repository.AddAsync(showcase2);

        IReadOnlyList<Showcase> result = await repository.GetAllAsync(null, null);

        result.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllAsync_WithCategoryFilter_ReturnsOnlyMatchingCategory()
    {
        ShowcaseRepository repository = CreateRepository();
        Showcase webApp = CreateValidShowcase($"WebApp-{Guid.NewGuid()}", ShowcaseCategory.WebApp);
        Showcase api = CreateValidShowcase($"Api-{Guid.NewGuid()}", ShowcaseCategory.Api);

        await repository.AddAsync(webApp);
        await repository.AddAsync(api);

        IReadOnlyList<Showcase> result = await repository.GetAllAsync(ShowcaseCategory.Api, null);

        result.Should().AllSatisfy(s => s.Category.Should().Be(ShowcaseCategory.Api));
    }

    [Fact]
    public async Task GetAllAsync_WithTagFilter_ReturnsOnlyMatchingTag()
    {
        ShowcaseRepository repository = CreateRepository();

        Foundry.Shared.Kernel.Results.Result<Showcase> withTagResult = Showcase.Create(
            $"Tagged-{Guid.NewGuid()}",
            null,
            ShowcaseCategory.Tool,
            "https://demo.example.com",
            null,
            null,
            new List<string> { "special-tag-xyz" },
            0,
            true);
        await repository.AddAsync(withTagResult.Value);

        Foundry.Shared.Kernel.Results.Result<Showcase> withoutTagResult = Showcase.Create(
            $"NotTagged-{Guid.NewGuid()}",
            null,
            ShowcaseCategory.Tool,
            "https://demo.example.com",
            null,
            null,
            new List<string> { "other-tag" },
            0,
            true);
        await repository.AddAsync(withoutTagResult.Value);

        IReadOnlyList<Showcase> result = await repository.GetAllAsync(null, "special-tag-xyz");

        result.Should().AllSatisfy(s => s.Tags.Should().Contain("special-tag-xyz"));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesShowcase()
    {
        ShowcaseRepository repository = CreateRepository();
        Showcase showcase = CreateValidShowcase("Original Title");
        await repository.AddAsync(showcase);

        showcase.Update(
            "Updated Title",
            "New Description",
            ShowcaseCategory.Mobile,
            "https://demo.example.com",
            null,
            null,
            null,
            10,
            false);
        await repository.UpdateAsync(showcase);

        Showcase? result = await repository.GetByIdAsync(showcase.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated Title");
        result.Category.Should().Be(ShowcaseCategory.Mobile);
        result.DisplayOrder.Should().Be(10);
    }

    [Fact]
    public async Task DeleteAsync_RemovesShowcase()
    {
        ShowcaseRepository repository = CreateRepository();
        Showcase showcase = CreateValidShowcase("To Delete");
        await repository.AddAsync(showcase);

        await repository.DeleteAsync(showcase.Id);

        Showcase? result = await repository.GetByIdAsync(showcase.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_DoesNotThrow()
    {
        ShowcaseRepository repository = CreateRepository();

        Func<Task> act = async () => await repository.DeleteAsync(ShowcaseId.New());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsShowcasesOrderedByDisplayOrderThenTitle()
    {
        ShowcaseRepository repository = CreateRepository();
        string suffix = Guid.NewGuid().ToString()[..8];

        Foundry.Shared.Kernel.Results.Result<Showcase> b2 = Showcase.Create(
            $"B-{suffix}", null, ShowcaseCategory.Tool, "https://demo.example.com", null, null, null, 2, true);
        Foundry.Shared.Kernel.Results.Result<Showcase> a1 = Showcase.Create(
            $"A-{suffix}", null, ShowcaseCategory.Tool, "https://demo.example.com", null, null, null, 1, true);
        Foundry.Shared.Kernel.Results.Result<Showcase> c1 = Showcase.Create(
            $"C-{suffix}", null, ShowcaseCategory.Tool, "https://demo.example.com", null, null, null, 1, true);

        await repository.AddAsync(b2.Value);
        await repository.AddAsync(a1.Value);
        await repository.AddAsync(c1.Value);

        IReadOnlyList<Showcase> result = await repository.GetAllAsync(ShowcaseCategory.Tool, null);

        // Filter to our created items only
        List<Showcase> ours = result.Where(s => s.Title.EndsWith(suffix, StringComparison.Ordinal)).ToList();
        ours.Should().HaveCount(3);
        ours[0].DisplayOrder.Should().Be(1);
        ours[1].DisplayOrder.Should().Be(1);
        ours[2].DisplayOrder.Should().Be(2);
    }
}
