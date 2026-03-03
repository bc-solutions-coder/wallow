using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Communications.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Tests.Infrastructure.Persistence.Repositories;

public sealed class ChangelogRepositoryTests : IDisposable
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly ChangelogRepository _repository;

    public ChangelogRepositoryTests()
    {
        DbContextOptions<CommunicationsDbContext> options = new DbContextOptionsBuilder<CommunicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));

        _dbContext = new CommunicationsDbContext(options, tenantContext);
        _repository = new ChangelogRepository(_dbContext);
    }

    [Fact]
    public async Task AddAsync_AddsEntryToDatabase()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Initial", "Content", DateTime.UtcNow, TimeProvider.System);

        await _repository.AddAsync(entry);

        int count = await _dbContext.ChangelogEntries.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsEntry()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);
        await _dbContext.ChangelogEntries.AddAsync(entry);
        await _dbContext.SaveChangesAsync();

        ChangelogEntry? result = await _repository.GetByIdAsync(entry.Id);

        result.Should().NotBeNull();
        result!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        ChangelogEntry? result = await _repository.GetByIdAsync(ChangelogEntryId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByVersionAsync_WhenExists_ReturnsEntry()
    {
        ChangelogEntry entry = ChangelogEntry.Create("2.0.0", "Major", "Content", DateTime.UtcNow, TimeProvider.System);
        await _dbContext.ChangelogEntries.AddAsync(entry);
        await _dbContext.SaveChangesAsync();

        ChangelogEntry? result = await _repository.GetByVersionAsync("2.0.0");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Major");
    }

    [Fact]
    public async Task GetByVersionAsync_WhenNotExists_ReturnsNull()
    {
        ChangelogEntry? result = await _repository.GetByVersionAsync("99.99.99");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestPublishedAsync_ReturnsLatestPublished()
    {
        ChangelogEntry older = ChangelogEntry.Create("1.0.0", "Old", "Content", DateTime.UtcNow.AddDays(-2), TimeProvider.System);
        older.Publish(TimeProvider.System);
        ChangelogEntry newer = ChangelogEntry.Create("2.0.0", "New", "Content", DateTime.UtcNow.AddDays(-1), TimeProvider.System);
        newer.Publish(TimeProvider.System);
        ChangelogEntry draft = ChangelogEntry.Create("3.0.0", "Draft", "Content", DateTime.UtcNow, TimeProvider.System);

        await _dbContext.ChangelogEntries.AddRangeAsync(older, newer, draft);
        await _dbContext.SaveChangesAsync();

        ChangelogEntry? result = await _repository.GetLatestPublishedAsync();

        result.Should().NotBeNull();
        result!.Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task GetLatestPublishedAsync_WhenNonePublished_ReturnsNull()
    {
        ChangelogEntry draft = ChangelogEntry.Create("1.0.0", "Draft", "Content", DateTime.UtcNow, TimeProvider.System);
        await _dbContext.ChangelogEntries.AddAsync(draft);
        await _dbContext.SaveChangesAsync();

        ChangelogEntry? result = await _repository.GetLatestPublishedAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPublishedAsync_ReturnsOnlyPublishedEntries()
    {
        ChangelogEntry published = ChangelogEntry.Create("1.0.0", "Published", "Content", DateTime.UtcNow, TimeProvider.System);
        published.Publish(TimeProvider.System);
        ChangelogEntry draft = ChangelogEntry.Create("2.0.0", "Draft", "Content", DateTime.UtcNow, TimeProvider.System);

        await _dbContext.ChangelogEntries.AddRangeAsync(published, draft);
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<ChangelogEntry> result = await _repository.GetPublishedAsync();

        result.Should().ContainSingle();
        result[0].Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task GetPublishedAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            ChangelogEntry entry = ChangelogEntry.Create($"{i}.0.0", $"Release {i}", "Content", DateTime.UtcNow.AddDays(-i), TimeProvider.System);
            entry.Publish(TimeProvider.System);
            await _dbContext.ChangelogEntries.AddAsync(entry);
        }
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<ChangelogEntry> result = await _repository.GetPublishedAsync(limit: 3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntries()
    {
        ChangelogEntry e1 = ChangelogEntry.Create("1.0.0", "First", "Content", DateTime.UtcNow, TimeProvider.System);
        ChangelogEntry e2 = ChangelogEntry.Create("2.0.0", "Second", "Content", DateTime.UtcNow, TimeProvider.System);

        await _dbContext.ChangelogEntries.AddRangeAsync(e1, e2);
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<ChangelogEntry> result = await _repository.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEntryInDatabase()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Original", "Content", DateTime.UtcNow, TimeProvider.System);
        await _dbContext.ChangelogEntries.AddAsync(entry);
        await _dbContext.SaveChangesAsync();

        entry.Update("1.0.1", "Updated", "New Content", DateTime.UtcNow, TimeProvider.System);
        await _repository.UpdateAsync(entry);

        ChangelogEntry? found = await _dbContext.ChangelogEntries.FindAsync(entry.Id);
        found!.Title.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntryFromDatabase()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "ToDelete", "Content", DateTime.UtcNow, TimeProvider.System);
        await _dbContext.ChangelogEntries.AddAsync(entry);
        await _dbContext.SaveChangesAsync();

        await _repository.DeleteAsync(entry);

        ChangelogEntry? found = await _dbContext.ChangelogEntries.FindAsync(entry.Id);
        found.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
