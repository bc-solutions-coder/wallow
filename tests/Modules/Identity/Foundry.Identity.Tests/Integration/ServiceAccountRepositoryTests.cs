using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Identity.Infrastructure.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;

namespace Foundry.Identity.Tests.Integration;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public sealed class ServiceAccountRepositoryTests : DbContextIntegrationTestBase<IdentityDbContext>
{
    private ServiceAccountRepository _repository = null!;

    public ServiceAccountRepositoryTests(PostgresContainerFixture fixture) : base(fixture) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _repository = new ServiceAccountRepository(DbContext);
    }

    [Fact]
    public async Task Add_PersistsServiceAccount()
    {
        ServiceAccountMetadata account = ServiceAccountMetadata.Create(
            TestTenantId,
            "test-client-id",
            "Test Service",
            "Test Description",
            ["billing:read", "billing:write"],
            Guid.NewGuid());

        _repository.Add(account);
        await _repository.SaveChangesAsync();

        ServiceAccountMetadata? retrieved = await _repository.GetByIdAsync(account.Id);

        retrieved.Should().NotBeNull();
        retrieved.Name.Should().Be("Test Service");
        retrieved.KeycloakClientId.Should().Be("test-client-id");
        retrieved.Scopes.Should().BeEquivalentTo("billing:read", "billing:write");
    }

    [Fact]
    public async Task GetByKeycloakClientIdAsync_FindsAccount()
    {
        ServiceAccountMetadata account = ServiceAccountMetadata.Create(
            TestTenantId,
            "unique-client-id",
            "Unique Service",
            "Description",
            ["scope:read"],
            Guid.NewGuid());

        _repository.Add(account);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        ServiceAccountMetadata? retrieved = await _repository.GetByKeycloakClientIdAsync("unique-client-id");

        retrieved.Should().NotBeNull();
        retrieved.Name.Should().Be("Unique Service");
    }

    [Fact]
    public async Task GetByKeycloakClientIdAsync_IgnoresQueryFilters()
    {
        TenantId otherTenantId = TenantId.New();
        await using IdentityDbContext otherDbContext = CreateDbContextForTenant(otherTenantId);

        ServiceAccountRepository otherRepository = new ServiceAccountRepository(otherDbContext);

        ServiceAccountMetadata account = ServiceAccountMetadata.Create(
            otherTenantId,
            "cross-tenant-client",
            "Cross Tenant",
            "Description",
            ["scope:all"],
            Guid.NewGuid());

        otherRepository.Add(account);
        await otherDbContext.SaveChangesAsync();

        ServiceAccountMetadata? retrievedFromOtherTenant = await _repository.GetByKeycloakClientIdAsync("cross-tenant-client");

        retrievedFromOtherTenant.Should().NotBeNull();
        retrievedFromOtherTenant.Name.Should().Be("Cross Tenant");
    }

    [Fact]
    public async Task GetAllAsync_ExcludesRevokedAccounts()
    {
        ServiceAccountMetadata active = ServiceAccountMetadata.Create(TestTenantId, "client-active", "Active", "Description", ["scope:read"], Guid.NewGuid());
        ServiceAccountMetadata revoked = ServiceAccountMetadata.Create(TestTenantId, "client-revoked", "Revoked", "Description", ["scope:read"], Guid.NewGuid());

        revoked.Revoke(Guid.NewGuid());

        _repository.Add(active);
        _repository.Add(revoked);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        IReadOnlyList<ServiceAccountMetadata> all = await _repository.GetAllAsync();

        all.Should().HaveCount(1);
        all[0].Name.Should().Be("Active");
        all[0].Status.Should().Be(ServiceAccountStatus.Active);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByName()
    {
        ServiceAccountMetadata charlie = ServiceAccountMetadata.Create(TestTenantId, "client-c", "Charlie", "Description", ["scope:read"], Guid.NewGuid());
        ServiceAccountMetadata alice = ServiceAccountMetadata.Create(TestTenantId, "client-a", "Alice", "Description", ["scope:read"], Guid.NewGuid());
        ServiceAccountMetadata bob = ServiceAccountMetadata.Create(TestTenantId, "client-b", "Bob", "Description", ["scope:read"], Guid.NewGuid());

        _repository.Add(charlie);
        _repository.Add(alice);
        _repository.Add(bob);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        IReadOnlyList<ServiceAccountMetadata> all = await _repository.GetAllAsync();

        all.Should().HaveCount(3);
        all[0].Name.Should().Be("Alice");
        all[1].Name.Should().Be("Bob");
        all[2].Name.Should().Be("Charlie");
    }

    [Fact]
    public async Task RespectsTenantIsolation()
    {
        ServiceAccountMetadata account1 = ServiceAccountMetadata.Create(TestTenantId, "tenant1-client", "Tenant 1", "Description", ["scope:read"], Guid.NewGuid());
        _repository.Add(account1);
        await _repository.SaveChangesAsync();

        TenantId otherTenantId = TenantId.New();
        await using IdentityDbContext otherDbContext = CreateDbContextForTenant(otherTenantId);

        ServiceAccountRepository otherRepository = new ServiceAccountRepository(otherDbContext);

        ServiceAccountMetadata account2 = ServiceAccountMetadata.Create(otherTenantId, "tenant2-client", "Tenant 2", "Description", ["scope:read"], Guid.NewGuid());
        otherRepository.Add(account2);
        await otherDbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        otherDbContext.ChangeTracker.Clear();

        IReadOnlyList<ServiceAccountMetadata> tenant1Results = await _repository.GetAllAsync();
        IReadOnlyList<ServiceAccountMetadata> tenant2Results = await otherRepository.GetAllAsync();

        tenant1Results.Should().HaveCount(1);
        tenant1Results[0].Name.Should().Be("Tenant 1");

        tenant2Results.Should().HaveCount(1);
        tenant2Results[0].Name.Should().Be("Tenant 2");
    }
}
