using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Inquiries.Tests.Infrastructure.Persistence;

public sealed class InquiriesDbContextTests : IDisposable
{
    private readonly InquiriesDbContext _context;

    public InquiriesDbContextTests()
    {
        DbContextOptions<InquiriesDbContext> options = new DbContextOptionsBuilder<InquiriesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.New());
        _context = new InquiriesDbContext(options);
        _context.SetTenant(tenantContext.TenantId);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DbContext_HasInquiriesDbSet()
    {
        _context.Inquiries.Should().NotBeNull();
    }

    [Fact]
    public async Task DbContext_CanAddAndRetrieveInquiry()
    {
        Inquiry inquiry = Inquiry.Create("Context Test", "ctx@example.com", "555-0100", null, null, "Web", "$5k", "1 month", "Test message.", "127.0.0.1", TimeProvider.System);
        inquiry.ClearDomainEvents();

        await _context.Inquiries.AddAsync(inquiry);
        await _context.SaveChangesAsync();

        Inquiry? result = await _context.Inquiries.FindAsync(inquiry.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Context Test");
    }

    [Fact]
    public void DbContext_UseNoTrackingByDefault()
    {
        _context.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.NoTracking);
    }
}
