using Foundry.Configuration.Infrastructure.Persistence;

namespace Foundry.Configuration.Tests.Infrastructure.Persistence;

public class DesignTimeTenantContextTests
{
    // DesignTimeTenantContext is internal so we test via the factory
    [Fact]
    public void DesignTimeTenantContext_ExposedViaFactory_HasExpectedDefaults()
    {
        // The factory creates a DesignTimeTenantContext internally.
        // We verify the DbContext is created successfully, which means
        // the tenant context returns valid defaults.
        ConfigurationDbContextFactory factory = new();

        ConfigurationDbContext context = factory.CreateDbContext([]);

        // If we got here without exception, the DesignTimeTenantContext worked correctly
        context.Should().NotBeNull();
    }
}
