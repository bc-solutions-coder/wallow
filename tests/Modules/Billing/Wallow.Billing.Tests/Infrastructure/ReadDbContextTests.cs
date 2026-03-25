using Microsoft.EntityFrameworkCore;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Core.Persistence;

namespace Wallow.Billing.Tests.Infrastructure;

public sealed class ReadDbContextTests : IDisposable
{
    private readonly BillingDbContext _billingDbContext;
    private readonly ReadDbContext<BillingDbContext> _readDbContext;

    public ReadDbContextTests()
    {
        DbContextOptions<BillingDbContext> options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReadDbContextTests_{Guid.NewGuid()}")
            .Options;

        _billingDbContext = new BillingDbContext(options);
        _readDbContext = new ReadDbContext<BillingDbContext>(_billingDbContext, blockWrites: true);
    }

    [Fact]
    public void Context_QueryTrackingBehavior_EqualsNoTracking()
    {
        _readDbContext.Context.ChangeTracker.QueryTrackingBehavior
            .Should().Be(QueryTrackingBehavior.NoTracking);
    }

    [Fact]
    public void SaveChanges_ThrowsInvalidOperationException()
    {
        Action act = () => _readDbContext.Context.SaveChanges();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveChangesAsync_ThrowsInvalidOperationException()
    {
        Func<Task> act = async () => await _readDbContext.Context.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Context_Invoices_ReturnsEmptyList()
    {
        List<Billing.Domain.Entities.Invoice> invoices = await _readDbContext.Context.Invoices.ToListAsync();

        invoices.Should().BeEmpty();
    }

    public void Dispose()
    {
        _billingDbContext.Dispose();
    }
}
