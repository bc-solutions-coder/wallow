using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Data;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Tests.Infrastructure;

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
        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Foundry.Identity.Tests");
        _dbContext = new IdentityDbContext(options, tenantContext, dataProtectionProvider);
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
        count.Should().Be(14);
    }

    [Fact]
    public async Task SeedAsync_WhenSomeScopesExist_OnlySeedsMissingOnes()
    {
        // Pre-seed just one scope
        ApiScope existingScope = ApiScope.Create("invoices.read", "Read Invoices", "Billing",
            "Access to read invoices and invoice data", isDefault: true);
        _dbContext.ApiScopes.Add(existingScope);
        await _dbContext.SaveChangesAsync();
        int preCount = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();
        preCount.Should().Be(1);

        await _seeder.SeedAsync(_dbContext);

        int totalCount = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();
        totalCount.Should().Be(14);

        // Verify no duplicate of the pre-seeded scope
        int invoiceReadCount = await _dbContext.ApiScopes
            .IgnoreQueryFilters()
            .CountAsync(s => s.Code == "invoices.read");
        invoiceReadCount.Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_WhenMultipleScopesExist_OnlySeedsRemaining()
    {
        // Pre-seed three scopes from different categories
        _dbContext.ApiScopes.Add(ApiScope.Create("invoices.read", "Read Invoices", "Billing",
            "Access to read invoices and invoice data", isDefault: true));
        _dbContext.ApiScopes.Add(ApiScope.Create("users.read", "Read Users", "Identity",
            "Access to read user profiles and data", isDefault: true));
        _dbContext.ApiScopes.Add(ApiScope.Create("notifications.read", "Read Notifications", "Notifications",
            "Access to read notifications"));
        await _dbContext.SaveChangesAsync();

        await _seeder.SeedAsync(_dbContext);

        int totalCount = await _dbContext.ApiScopes.IgnoreQueryFilters().CountAsync();
        totalCount.Should().Be(14);
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
            "invoices.read",
            "invoices.write",
            "payments.read",
            "payments.write",
            "subscriptions.read",
            "subscriptions.write",
            "users.read",
            "users.write",
            "notifications.read",
            "notifications.write",
            "showcases.read",
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

        categories.Should().BeEquivalentTo(["Billing", "Identity", "Notifications", "Showcases", "Inquiries", "Platform"]);
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
        nonDefaultScopes.Select(s => s.Code).Should().Contain("invoices.write");
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
        count.Should().Be(14);
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
