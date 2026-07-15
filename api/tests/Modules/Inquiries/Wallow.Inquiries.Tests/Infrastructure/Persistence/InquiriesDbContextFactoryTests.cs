using Wallow.Inquiries.Infrastructure.Persistence;

namespace Wallow.Inquiries.Tests.Infrastructure.Persistence;

public class InquiriesDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_ReturnsDbContext()
    {
        InquiriesDbContextFactory factory = new();

        InquiriesDbContext context = factory.CreateDbContext([]);

        context.Should().NotBeNull();
        context.Dispose();
    }

    [Fact]
    public void CreateDbContext_UsesEnvVarPassword()
    {
        Environment.SetEnvironmentVariable("WALLOW_DB_PASSWORD", "testpassword");
        InquiriesDbContextFactory factory = new();

        InquiriesDbContext context = factory.CreateDbContext([]);

        context.Should().NotBeNull();
        context.Dispose();
        Environment.SetEnvironmentVariable("WALLOW_DB_PASSWORD", null);
    }
}
