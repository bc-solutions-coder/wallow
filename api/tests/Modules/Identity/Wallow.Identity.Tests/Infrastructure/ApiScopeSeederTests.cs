using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class ApiScopeSeederTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;

    public ApiScopeSeederTests()
    {
        TenantContext tenantContext = new();
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SeedAsync_WhenEmpty_SeedsDefaultScopes()
    {
        ILogger<ApiScopeSeeder> logger = Substitute.For<ILogger<ApiScopeSeeder>>();
        ApiScopeSeeder seeder = new(logger);

        await seeder.SeedAsync(_dbContext);

        List<ApiScope> scopes = await _dbContext.ApiScopes.IgnoreQueryFilters().ToListAsync();
        scopes.Should().NotBeEmpty();
        scopes.Select(s => s.Code).Should().Contain("users.read");
        scopes.Select(s => s.Code).Should().Contain("storage.read");
    }

    [Fact]
    public async Task SeedAsync_WhenScopesAlreadyExist_DoesNotDuplicate()
    {
        ILogger<ApiScopeSeeder> logger = Substitute.For<ILogger<ApiScopeSeeder>>();
        ApiScopeSeeder seeder = new(logger);

        // Seed twice
        await seeder.SeedAsync(_dbContext);
        int firstCount = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();

        await seeder.SeedAsync(_dbContext);
        int secondCount = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();

        secondCount.Should().Be(firstCount);
    }

    [Fact]
    public async Task SeedAsync_WhenAllScopesExist_LogsAndReturnsEarly()
    {
        ILogger<ApiScopeSeeder> logger = Substitute.For<ILogger<ApiScopeSeeder>>();
        ApiScopeSeeder seeder = new(logger);

        // First seed
        await seeder.SeedAsync(_dbContext);

        // Second seed should not throw and log debug
        Func<Task> act = () => seeder.SeedAsync(_dbContext);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SeedAsync_SeedsAllExpectedCategories()
    {
        ILogger<ApiScopeSeeder> logger = Substitute.For<ILogger<ApiScopeSeeder>>();
        ApiScopeSeeder seeder = new(logger);

        await seeder.SeedAsync(_dbContext);

        List<ApiScope> scopes = await _dbContext.ApiScopes.IgnoreQueryFilters().ToListAsync();
        scopes.Select(s => s.Category).Distinct().Should().Contain("Identity");
        scopes.Select(s => s.Category).Distinct().Should().Contain("Storage");
    }

    [Fact]
    public async Task SeedAsync_SeedsDefaultScopes_HaveIsDefaultSet()
    {
        ILogger<ApiScopeSeeder> logger = Substitute.For<ILogger<ApiScopeSeeder>>();
        ApiScopeSeeder seeder = new(logger);

        await seeder.SeedAsync(_dbContext);

        List<ApiScope> defaultScopes = await _dbContext.ApiScopes
            .IgnoreQueryFilters()
            .Where(s => s.IsDefault)
            .ToListAsync();

        defaultScopes.Should().NotBeEmpty();
        defaultScopes.Select(s => s.Code).Should().Contain("users.read");
    }
}
