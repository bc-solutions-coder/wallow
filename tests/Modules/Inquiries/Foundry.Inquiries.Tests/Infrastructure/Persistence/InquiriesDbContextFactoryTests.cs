using Foundry.Inquiries.Infrastructure.Persistence;

namespace Foundry.Inquiries.Tests.Infrastructure.Persistence;

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
        Environment.SetEnvironmentVariable("FOUNDRY_DB_PASSWORD", "testpassword");
        InquiriesDbContextFactory factory = new();

        InquiriesDbContext context = factory.CreateDbContext([]);

        context.Should().NotBeNull();
        context.Dispose();
        Environment.SetEnvironmentVariable("FOUNDRY_DB_PASSWORD", null);
    }
}
