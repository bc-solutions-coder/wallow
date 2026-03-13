using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Storage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Storage.Tests.Infrastructure;

public sealed class StorageDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithDefaultArgs_ReturnsNonNullContext()
    {
        StorageDbContextFactory factory = new();

        StorageDbContext context = factory.CreateDbContext([]);

        context.Should().NotBeNull();
    }

    [Fact]
    public void DesignTimeTenantContext_WhenInstantiated_HasExpectedDefaults()
    {
        DesignTimeTenantContext tenantContext = new();

        tenantContext.TenantId.Value.Should().Be(Guid.Empty);
        tenantContext.TenantName.Should().Be("design-time");
        tenantContext.Region.Should().Be(RegionConfiguration.PrimaryRegion);
        tenantContext.IsResolved.Should().BeTrue();
    }
}
