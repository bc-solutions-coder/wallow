using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Shared.Kernel.Tests.MultiTenancy;

public class TenantContextTests
{
    [Fact]
    public void SetTenant_WithValidId_SetsTenantIdAndResolved()
    {
        TenantContext context = new();
        TenantId tenantId = TenantId.New();

        context.SetTenant(tenantId, "Test Tenant");

        context.TenantId.Should().Be(tenantId);
        context.TenantName.Should().Be("Test Tenant");
        context.IsResolved.Should().BeTrue();
    }

    [Fact]
    public void SetTenant_WithoutName_DefaultsToEmptyString()
    {
        TenantContext context = new();
        TenantId tenantId = TenantId.New();

        context.SetTenant(tenantId);

        context.TenantId.Should().Be(tenantId);
        context.TenantName.Should().BeEmpty();
        context.IsResolved.Should().BeTrue();
    }

    [Fact]
    public void Clear_AfterSetTenant_ResetsAllProperties()
    {
        TenantContext context = new();
        context.SetTenant(TenantId.New(), "Test Tenant");

        context.Clear();

        context.TenantId.Should().Be(default(TenantId));
        context.TenantName.Should().BeEmpty();
        context.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void IsResolved_WhenNewlyCreated_ReturnsFalse()
    {
        TenantContext context = new();

        context.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void TenantId_WhenNewlyCreated_ReturnsDefault()
    {
        TenantContext context = new();

        context.TenantId.Should().Be(default(TenantId));
    }

    [Fact]
    public void TenantName_WhenNewlyCreated_ReturnsEmptyString()
    {
        TenantContext context = new();

        context.TenantName.Should().BeEmpty();
    }

    [Fact]
    public void SetTenant_CalledTwice_OverwritesPreviousValues()
    {
        TenantContext context = new();
        TenantId firstId = TenantId.New();
        TenantId secondId = TenantId.New();

        context.SetTenant(firstId, "First");
        context.SetTenant(secondId, "Second");

        context.TenantId.Should().Be(secondId);
        context.TenantName.Should().Be("Second");
    }
}


