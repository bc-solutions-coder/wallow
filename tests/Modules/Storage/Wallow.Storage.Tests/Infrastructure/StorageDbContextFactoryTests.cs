using Wallow.Storage.Infrastructure.Persistence;

namespace Wallow.Storage.Tests.Infrastructure;

public sealed class StorageDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithDefaultArgs_ReturnsNonNullContext()
    {
        StorageDbContextFactory factory = new();

        StorageDbContext context = factory.CreateDbContext([]);

        context.Should().NotBeNull();
    }
}
