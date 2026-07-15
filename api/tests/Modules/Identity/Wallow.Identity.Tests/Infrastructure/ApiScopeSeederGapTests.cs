using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class ApiScopeSeederGapTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ILogger<ApiScopeSeeder> _logger;
    private readonly ApiScopeSeeder _seeder;

    public ApiScopeSeederGapTests()
    {
        TenantContext tenantContext = new();
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
        _logger = Substitute.For<ILogger<ApiScopeSeeder>>();
        _seeder = new ApiScopeSeeder(_logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SeedAsync_WhenEmpty_SeedsExactlyElevenScopes()
    {
        await _seeder.SeedAsync(_dbContext);

        int count = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();
        count.Should().Be(30);
    }

    [Fact]
    public async Task SeedAsync_WhenSomeScopesExist_OnlySeedsMissingOnes()
    {
        // Pre-seed just one scope
        ApiScope existingScope = ApiScope.Create("users.read", "Read Users", "Identity",
            "Access to read user profiles and data", isDefault: true);
        _dbContext.ApiScopes.Add(existingScope);
        await _dbContext.SaveChangesAsync();
        int preCount = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();
        preCount.Should().Be(1);

        await _seeder.SeedAsync(_dbContext);

        int totalCount = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();
        totalCount.Should().Be(30);

        // Verify no duplicate of the pre-seeded scope
        int usersReadCount = await _dbContext.ApiScopes
            .IgnoreQueryFilters()
            .CountAsync(s => s.Code == "users.read");
        usersReadCount.Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_WhenMultipleScopesExist_OnlySeedsRemaining()
    {
        // Pre-seed three scopes from different categories
        _dbContext.ApiScopes.Add(ApiScope.Create("storage.read", "Read Storage", "Storage",
            "Access to read files and storage data", isDefault: true));
        _dbContext.ApiScopes.Add(ApiScope.Create("users.read", "Read Users", "Identity",
            "Access to read user profiles and data", isDefault: true));
        _dbContext.ApiScopes.Add(ApiScope.Create("notifications.read", "Read Notifications", "Communications",
            "Access to read notifications"));
        await _dbContext.SaveChangesAsync();

        await _seeder.SeedAsync(_dbContext);

        int totalCount = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();
        totalCount.Should().Be(30);
    }

    [Fact]
    public async Task SeedAsync_SeedsAllExpectedScopeCodes()
    {
        await _seeder.SeedAsync(_dbContext);

        List<string> codes = await _dbContext.ApiScopes
            .IgnoreQueryFilters()
            .Select(s => s.Code)
            .ToListAsync();

        codes.Should().BeEquivalentTo([
            "users.read",
            "users.write",
            "users.manage",
            "roles.read",
            "roles.write",
            "roles.manage",
            "organizations.read",
            "organizations.write",
            "organizations.manage",
            "apikeys.read",
            "apikeys.write",
            "apikeys.manage",
            "sso.read",
            "sso.manage",
            "scim.manage",
            "serviceaccounts.read",
            "serviceaccounts.write",
            "serviceaccounts.manage",
            "storage.read",
            "storage.write",
            "announcements.read",
            "announcements.manage",
            "changelog.manage",
            "notifications.read",
            "notifications.write",
            "configuration.read",
            "configuration.manage",
            "inquiries.read",
            "inquiries.write",
            "webhooks.manage"
        ]);
    }

    [Fact]
    public async Task SeedAsync_SeedsAllExpectedCategories()
    {
        await _seeder.SeedAsync(_dbContext);

        List<string> categories = await _dbContext.ApiScopes
            .IgnoreQueryFilters()
            .Select(s => s.Category)
            .Distinct()
            .ToListAsync();

        categories.Should().BeEquivalentTo(["Identity", "Storage", "Communications", "Configuration", "Inquiries", "Platform"]);
    }

    [Fact]
    public async Task SeedAsync_NonDefaultScopes_HaveIsDefaultFalse()
    {
        await _seeder.SeedAsync(_dbContext);

        List<ApiScope> nonDefaultScopes = await _dbContext.ApiScopes
            .IgnoreQueryFilters()
            .Where(s => !s.IsDefault)
            .ToListAsync();

        nonDefaultScopes.Should().NotBeEmpty();
        nonDefaultScopes.Select(s => s.Code).Should().Contain("webhooks.manage");
    }

    [Fact]
    public async Task SeedAsync_AllScopes_HaveDescriptions()
    {
        await _seeder.SeedAsync(_dbContext);

        List<ApiScope> scopes = await _dbContext.ApiScopes.IgnoreQueryFilters().ToListAsync();

        scopes.Should().AllSatisfy(s => s.Description.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task SeedAsync_AllScopes_HaveDisplayNames()
    {
        await _seeder.SeedAsync(_dbContext);

        List<ApiScope> scopes = await _dbContext.ApiScopes.IgnoreQueryFilters().ToListAsync();

        scopes.Should().AllSatisfy(s => s.DisplayName.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task SeedAsync_WithCancellationToken_PropagatesToken()
    {
        using CancellationTokenSource cts = new();
        await _seeder.SeedAsync(_dbContext, cts.Token);

        int count = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();
        count.Should().Be(30);
    }

    [Fact]
    public async Task SeedAsync_WithCancelledToken_ThrowsOperationCancelled()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => _seeder.SeedAsync(_dbContext, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
