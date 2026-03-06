using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Billing.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Tests.Infrastructure.Services;

public class PaymentReportServiceTests
{
    [Fact]
    public void Constructor_CreatesInstance()
    {
        using BillingDbContext dbContext = CreateDbContext();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));

        PaymentReportService service = new PaymentReportService(dbContext, tenantContext);

        service.Should().NotBeNull();
    }

    private static BillingDbContext CreateDbContext()
    {
        DbContextOptions<BillingDbContext> options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));

        return new BillingDbContext(options, tenantContext);
    }
}
