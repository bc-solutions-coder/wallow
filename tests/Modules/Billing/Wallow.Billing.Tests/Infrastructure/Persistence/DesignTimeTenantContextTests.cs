using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Billing.Tests.Infrastructure.Persistence;

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
            .GetType("Wallow.Billing.Infrastructure.Persistence.DesignTimeTenantContext")!;
        return (ITenantContext)Activator.CreateInstance(type)!;
    }
}
