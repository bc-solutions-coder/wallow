using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Billing.Tests.Infrastructure.Persistence;

public class DesignTimeTenantContextTests
{
    [Fact]
    public void TenantId_ReturnsEmptyGuid()
    {
        ITenantContext context = CreateDesignTimeTenantContext();

        context.TenantId.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void TenantName_ReturnsDesignTime()
    {
        ITenantContext context = CreateDesignTimeTenantContext();

        context.TenantName.Should().Be("design-time");
    }

    [Fact]
    public void Region_ReturnsPrimaryRegion()
    {
        ITenantContext context = CreateDesignTimeTenantContext();

        context.Region.Should().Be(RegionConfiguration.PrimaryRegion);
    }

    [Fact]
    public void IsResolved_ReturnsTrue()
    {
        ITenantContext context = CreateDesignTimeTenantContext();

        context.IsResolved.Should().BeTrue();
    }

    private static ITenantContext CreateDesignTimeTenantContext()
    {
        // DesignTimeTenantContext is internal, so we use reflection
        Type type = typeof(BillingDbContext).Assembly
            .GetType("Foundry.Billing.Infrastructure.Persistence.DesignTimeTenantContext")!;
        return (ITenantContext)Activator.CreateInstance(type)!;
    }
}
