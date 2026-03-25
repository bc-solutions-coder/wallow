using Microsoft.EntityFrameworkCore;
using Wallow.ApiKeys.Domain.ApiKeys;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.ApiKeys.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.ApiKeys.Tests.Infrastructure;

public sealed class ApiKeyRepositoryTests : IDisposable
{
    private readonly ApiKeysDbContext _context;
    private readonly ApiKeyRepository _sut;
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Guid _userId = Guid.NewGuid();

    public ApiKeyRepositoryTests()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_tenantId);
        tenantContext.IsResolved.Returns(true);

        DbContextOptions<ApiKeysDbContext> options = new DbContextOptionsBuilder<ApiKeysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new TenantSaveChangesInterceptor(tenantContext))
            .Options;

        _context = new ApiKeysDbContext(options);
        _context.SetTenant(tenantContext.TenantId);
        _sut = new ApiKeyRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private ApiKey CreateApiKey(
        string serviceAccountId = "sa-test",
        string hashedKey = "hash-abc123",
        string displayName = "Test Key")
    {
        return ApiKey.Create(
            _tenantId,
            serviceAccountId,
            hashedKey,
            displayName,
            ["read", "write"],
            expiresAt: null,
            _userId,
            TimeProvider.System);
    }

    [Fact]
    public async Task AddAsync_PersistsApiKey()
    {
        ApiKey key = CreateApiKey();

        await _sut.AddAsync(key, CancellationToken.None);

        ApiKey? found = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == key.Id);
        found.Should().NotBeNull();
        found!.DisplayName.Should().Be("Test Key");
        found.ServiceAccountId.Should().Be("sa-test");
        found.HashedKey.Should().Be("hash-abc123");
    }

    [Fact]
    public async Task GetByHashAsync_WhenExists_ReturnsApiKey()
    {
        ApiKey key = CreateApiKey(hashedKey: "unique-hash");
        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync();

        ApiKey? result = await _sut.GetByHashAsync("unique-hash", _tenantId.Value, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HashedKey.Should().Be("unique-hash");
    }

    [Fact]
    public async Task GetByHashAsync_WhenNotExists_ReturnsNull()
    {
        ApiKey? result = await _sut.GetByHashAsync("nonexistent-hash", _tenantId.Value, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByHashAsync_WhenDifferentTenant_ReturnsNull()
    {
        ApiKey key = CreateApiKey(hashedKey: "tenant-specific-hash");
        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync();

        ApiKey? result = await _sut.GetByHashAsync("tenant-specific-hash", Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsApiKey()
    {
        ApiKey key = CreateApiKey();
        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync();

        ApiKey? result = await _sut.GetByIdAsync(key.Id, _tenantId.Value, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(key.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        ApiKey? result = await _sut.GetByIdAsync(ApiKeyId.New(), _tenantId.Value, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenDifferentTenant_ReturnsNull()
    {
        ApiKey key = CreateApiKey();
        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync();

        ApiKey? result = await _sut.GetByIdAsync(key.Id, Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListByServiceAccountAsync_ReturnsMatchingKeys()
    {
        ApiKey key1 = CreateApiKey(serviceAccountId: "sa-1", hashedKey: "hash-1", displayName: "Key 1");
        ApiKey key2 = CreateApiKey(serviceAccountId: "sa-1", hashedKey: "hash-2", displayName: "Key 2");
        ApiKey key3 = CreateApiKey(serviceAccountId: "sa-other", hashedKey: "hash-3", displayName: "Key 3");
        _context.ApiKeys.AddRange(key1, key2, key3);
        await _context.SaveChangesAsync();

        List<ApiKey> result = await _sut.ListByServiceAccountAsync("sa-1", _tenantId.Value, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(k => k.ServiceAccountId.Should().Be("sa-1"));
    }

    [Fact]
    public async Task ListByServiceAccountAsync_WhenNoMatch_ReturnsEmptyList()
    {
        ApiKey key = CreateApiKey(serviceAccountId: "sa-1", hashedKey: "hash-1");
        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync();

        List<ApiKey> result = await _sut.ListByServiceAccountAsync("sa-nonexistent", _tenantId.Value, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListByServiceAccountAsync_WhenDifferentTenant_ReturnsEmptyList()
    {
        ApiKey key = CreateApiKey(serviceAccountId: "sa-1", hashedKey: "hash-1");
        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync();

        List<ApiKey> result = await _sut.ListByServiceAccountAsync("sa-1", Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_PersistsAllFields()
    {
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddDays(30);
        ApiKey key = ApiKey.Create(
            _tenantId,
            "sa-full",
            "hash-full",
            "Full Key",
            ["scope-a", "scope-b", "scope-c"],
            expiry,
            _userId,
            TimeProvider.System);

        await _sut.AddAsync(key, CancellationToken.None);

        ApiKey? found = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == key.Id);
        found.Should().NotBeNull();
        found!.TenantId.Should().Be(_tenantId);
        found.ServiceAccountId.Should().Be("sa-full");
        found.HashedKey.Should().Be("hash-full");
        found.DisplayName.Should().Be("Full Key");
        found.Scopes.Should().BeEquivalentTo(["scope-a", "scope-b", "scope-c"]);
        found.ExpiresAt.Should().Be(expiry);
        found.IsRevoked.Should().BeFalse();
    }
}
