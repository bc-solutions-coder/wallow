using Microsoft.EntityFrameworkCore;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Services;
using Wallow.Shared.Kernel.Persistence;

namespace Wallow.Billing.Tests.Infrastructure.Services;

public class PaymentReportServiceTests
{
    [Fact]
    public void Constructor_CreatesInstance()
    {
        using BillingDbContext dbContext = CreateDbContext();
        IReadDbContext<BillingDbContext> readDbContext = Substitute.For<IReadDbContext<BillingDbContext>>();
        readDbContext.Context.Returns(dbContext);

        PaymentReportService service = new(readDbContext);

        service.Should().NotBeNull();
    }

    private static BillingDbContext CreateDbContext()
    {
        DbContextOptions<BillingDbContext> options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BillingDbContext(options);
    }
}
