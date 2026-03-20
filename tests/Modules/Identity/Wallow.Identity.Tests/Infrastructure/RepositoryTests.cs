using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class RepositoryTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _tenantId = Guid.NewGuid();

    public RepositoryTests()
    {
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(new TenantId(_tenantId));

        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, _tenantContext, dataProtectionProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region ApiScopeRepository

    [Fact]
    public async Task ApiScopeRepository_GetAllAsync_ReturnsAllScopes()
    {
        ApiScope scope1 = ApiScope.Create("invoices.read", "Read Invoices", "Billing");
        ApiScope scope2 = ApiScope.Create("users.read", "Read Users", "Identity");
        _dbContext.ApiScopes.Add(scope1);
        _dbContext.ApiScopes.Add(scope2);
        await _dbContext.SaveChangesAsync();

        ApiScopeRepository repo = new(_dbContext);
        IReadOnlyList<ApiScope> result = await repo.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApiScopeRepository_GetAllAsync_WithCategory_FiltersCorrectly()
    {
        ApiScope scope1 = ApiScope.Create("invoices.read", "Read Invoices", "Billing");
        ApiScope scope2 = ApiScope.Create("users.read", "Read Users", "Identity");
        _dbContext.ApiScopes.Add(scope1);
        _dbContext.ApiScopes.Add(scope2);
        await _dbContext.SaveChangesAsync();

        ApiScopeRepository repo = new(_dbContext);
        IReadOnlyList<ApiScope> result = await repo.GetAllAsync(category: "Billing");

        result.Should().HaveCount(1);
        result[0].Code.Should().Be("invoices.read");
    }

    [Fact]
    public async Task ApiScopeRepository_GetByCodesAsync_ReturnMatchingScopes()
    {
        ApiScope scope1 = ApiScope.Create("invoices.read", "Read Invoices", "Billing");
        ApiScope scope2 = ApiScope.Create("users.read", "Read Users", "Identity");
        ApiScope scope3 = ApiScope.Create("payments.write", "Write Payments", "Billing");
        _dbContext.ApiScopes.Add(scope1);
        _dbContext.ApiScopes.Add(scope2);
        _dbContext.ApiScopes.Add(scope3);
        await _dbContext.SaveChangesAsync();

        ApiScopeRepository repo = new(_dbContext);
        IReadOnlyList<ApiScope> result = await repo.GetByCodesAsync(["invoices.read", "payments.write"]);

        result.Should().HaveCount(2);
        result.Select(s => s.Code).Should().Contain("invoices.read");
        result.Select(s => s.Code).Should().Contain("payments.write");
    }

    [Fact]
    public async Task ApiScopeRepository_Add_PersistsScope()
    {
        ApiScope scope = ApiScope.Create("webhooks.manage", "Manage Webhooks", "Platform");

        ApiScopeRepository repo = new(_dbContext);
        repo.Add(scope);
        await repo.SaveChangesAsync();

        IReadOnlyList<ApiScope> all = await repo.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Code.Should().Be("webhooks.manage");
    }

    [Fact]
    public async Task ApiScopeRepository_GetAllAsync_EmptyDb_ReturnsEmpty()
    {
        ApiScopeRepository repo = new(_dbContext);
        IReadOnlyList<ApiScope> result = await repo.GetAllAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region ScimSyncLogRepository

    private static ScimSyncLog CreateScimSyncLog(Guid tenantId, string externalId, string? internalId, bool success, string? errorMessage = null)
    {
        return ScimSyncLog.Create(
            new TenantId(tenantId),
            ScimOperation.Create,
            ScimResourceType.User,
            externalId,
            internalId,
            success,
            TimeProvider.System,
            errorMessage);
    }

    [Fact]
    public async Task ScimSyncLogRepository_GetRecentAsync_ReturnsResults()
    {
        ScimSyncLog log1 = CreateScimSyncLog(_tenantId, "ext-1", "int-1", true);
        ScimSyncLog log2 = CreateScimSyncLog(_tenantId, "ext-2", "int-2", true);

        _dbContext.ScimSyncLogs.Add(log1);
        _dbContext.ScimSyncLogs.Add(log2);
        await _dbContext.SaveChangesAsync();

        ScimSyncLogRepository repo = new(_dbContext);
        IReadOnlyList<ScimSyncLog> result = await repo.GetRecentAsync(10);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScimSyncLogRepository_GetRecentAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            ScimSyncLog log = CreateScimSyncLog(_tenantId, $"ext-{i}", $"int-{i}", true);
            _dbContext.ScimSyncLogs.Add(log);
        }
        await _dbContext.SaveChangesAsync();

        ScimSyncLogRepository repo = new(_dbContext);
        IReadOnlyList<ScimSyncLog> result = await repo.GetRecentAsync(3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ScimSyncLogRepository_Add_PersistsLog()
    {
        ScimSyncLog log = ScimSyncLog.Create(
            new TenantId(_tenantId),
            ScimOperation.Delete,
            ScimResourceType.Group,
            "ext-del",
            null,
            false,
            TimeProvider.System,
            errorMessage: "User not found");

        ScimSyncLogRepository repo = new(_dbContext);
        repo.Add(log);
        await repo.SaveChangesAsync();

        IReadOnlyList<ScimSyncLog> all = await repo.GetRecentAsync();
        all.Should().HaveCount(1);
        all[0].ExternalId.Should().Be("ext-del");
        all[0].Success.Should().BeFalse();
    }

    [Fact]
    public async Task ScimSyncLogRepository_GetRecentAsync_EmptyDb_ReturnsEmpty()
    {
        ScimSyncLogRepository repo = new(_dbContext);
        IReadOnlyList<ScimSyncLog> result = await repo.GetRecentAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region ScimConfigurationRepository

    [Fact]
    public async Task ScimConfigurationRepository_GetAsync_WhenNone_ReturnsNull()
    {
        ScimConfigurationRepository repo = new(_dbContext);
        ScimConfiguration? result = await repo.GetAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task ScimConfigurationRepository_Add_PersistsConfiguration()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(new TenantId(_tenantId), Guid.NewGuid(), TimeProvider.System);

        ScimConfigurationRepository repo = new(_dbContext);
        repo.Add(config);
        await repo.SaveChangesAsync();

        ScimConfiguration? found = await repo.GetAsync();
        found.Should().NotBeNull();
        found!.TenantId.Value.Should().Be(_tenantId);
    }

    #endregion
}
