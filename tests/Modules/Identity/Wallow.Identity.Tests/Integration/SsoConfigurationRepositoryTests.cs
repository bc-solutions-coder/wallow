using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Identity.Tests.Integration;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public sealed class SsoConfigurationRepositoryTests(PostgresContainerFixture fixture) : DbContextIntegrationTestBase<IdentityDbContext>(fixture)
{
    private SsoConfigurationRepository _repository = null!;

    protected override IdentityDbContext CreateDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    {
        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        return new IdentityDbContext(options, dataProtectionProvider);
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
            "lastName", Guid.NewGuid(), TimeProvider.System);

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
            "family_name", Guid.NewGuid(), TimeProvider.System);

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
            "lastName", Guid.NewGuid(), TimeProvider.System);

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
            "family_name", Guid.NewGuid(), TimeProvider.System);

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
