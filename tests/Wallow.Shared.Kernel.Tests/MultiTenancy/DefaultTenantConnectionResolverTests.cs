using Microsoft.Extensions.Configuration;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Shared.Kernel.Tests.MultiTenancy;

public class DefaultTenantConnectionResolverTests
{
    [Fact]
    public async Task ResolveConnectionStringAsync_WithConfiguredConnection_ReturnsConnectionString()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
            })
            .Build();

        DefaultTenantConnectionResolver resolver = new(configuration);
        TenantId tenantId = TenantId.New();

        string result = await resolver.ResolveConnectionStringAsync(tenantId, CancellationToken.None);

        result.Should().Be("Host=localhost;Database=test");
    }

    [Fact]
    public async Task ResolveConnectionStringAsync_WithoutConfiguredConnection_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        DefaultTenantConnectionResolver resolver = new(configuration);
        TenantId tenantId = TenantId.New();

        Func<Task> act = () => resolver.ResolveConnectionStringAsync(tenantId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DefaultConnection*not configured*");
    }

    [Fact]
    public async Task ResolveConnectionStringAsync_ReturnsSameStringForDifferentTenants()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=shared"
            })
            .Build();

        DefaultTenantConnectionResolver resolver = new(configuration);

        string result1 = await resolver.ResolveConnectionStringAsync(TenantId.New(), CancellationToken.None);
        string result2 = await resolver.ResolveConnectionStringAsync(TenantId.New(), CancellationToken.None);

        result1.Should().Be(result2);
    }
}
