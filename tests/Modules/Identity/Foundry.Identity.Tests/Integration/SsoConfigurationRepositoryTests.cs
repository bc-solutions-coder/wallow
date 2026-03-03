using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Identity.Infrastructure.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Tests.Integration;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public sealed class SsoConfigurationRepositoryTests : DbContextIntegrationTestBase<IdentityDbContext>
{
    private SsoConfigurationRepository _repository = null!;

    public SsoConfigurationRepositoryTests(PostgresContainerFixture fixture) : base(fixture) { }

    protected override IdentityDbContext CreateDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    {
        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Foundry.Identity.Tests");
        return new IdentityDbContext(options, tenantContext, dataProtectionProvider);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _repository = new SsoConfigurationRepository(DbContext);
    }

    [Fact]
    public async Task Add_PersistsSsoConfiguration()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            TestTenantId,
            "Corporate SAML",
            SsoProtocol.Saml,
            "email",
            "firstName",
            "lastName",
            Guid.NewGuid());

        _repository.Add(config);
        await _repository.SaveChangesAsync();

        SsoConfiguration? retrieved = await _repository.GetAsync();

        retrieved.Should().NotBeNull();
        retrieved.Protocol.Should().Be(SsoProtocol.Saml);
        retrieved.DisplayName.Should().Be("Corporate SAML");
        retrieved.EmailAttribute.Should().Be("email");
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenNoConfiguration()
    {
        SsoConfiguration? config = await _repository.GetAsync();

        config.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsSingleConfigurationPerTenant()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            TestTenantId,
            "OIDC Provider",
            SsoProtocol.Oidc,
            "email",
            "given_name",
            "family_name",
            Guid.NewGuid());

        _repository.Add(config);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        SsoConfiguration? retrieved = await _repository.GetAsync();

        retrieved.Should().NotBeNull();
        retrieved.Protocol.Should().Be(SsoProtocol.Oidc);
    }

    [Fact]
    public async Task RespectsTenantIsolation()
    {
        SsoConfiguration config1 = SsoConfiguration.Create(
            TestTenantId,
            "Tenant 1 SAML",
            SsoProtocol.Saml,
            "email",
            "firstName",
            "lastName",
            Guid.NewGuid());

        _repository.Add(config1);
        await _repository.SaveChangesAsync();

        TenantId otherTenantId = TenantId.New();
        await using IdentityDbContext otherDbContext = CreateDbContextForTenant(otherTenantId);

        SsoConfigurationRepository otherRepository = new(otherDbContext);

        SsoConfiguration config2 = SsoConfiguration.Create(
            otherTenantId,
            "Tenant 2 OIDC",
            SsoProtocol.Oidc,
            "email",
            "given_name",
            "family_name",
            Guid.NewGuid());

        otherRepository.Add(config2);
        await otherDbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        otherDbContext.ChangeTracker.Clear();

        SsoConfiguration? tenant1Config = await _repository.GetAsync();
        SsoConfiguration? tenant2Config = await otherRepository.GetAsync();

        tenant1Config.Should().NotBeNull();
        tenant1Config.Protocol.Should().Be(SsoProtocol.Saml);
        tenant1Config.DisplayName.Should().Be("Tenant 1 SAML");

        tenant2Config.Should().NotBeNull();
        tenant2Config.Protocol.Should().Be(SsoProtocol.Oidc);
        tenant2Config.DisplayName.Should().Be("Tenant 2 OIDC");
    }
}
