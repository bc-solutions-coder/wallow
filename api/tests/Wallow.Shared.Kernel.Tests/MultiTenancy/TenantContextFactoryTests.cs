using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Shared.Kernel.Tests.MultiTenancy;

public class TenantContextFactoryTests
{
    [Fact]
    public void CreateScope_SetsTenantOnContext()
    {
        TenantContext context = new();
        TenantContextFactory factory = new(context);
        TenantId tenantId = TenantId.New();

        using IDisposable scope = factory.CreateScope(tenantId);

        context.TenantId.Should().Be(tenantId);
        context.IsResolved.Should().BeTrue();
    }

    [Fact]
    public void CreateScope_WhenDisposed_ClearsContext()
    {
        TenantContext context = new();
        TenantContextFactory factory = new(context);
        TenantId tenantId = TenantId.New();

        IDisposable scope = factory.CreateScope(tenantId);
        scope.Dispose();

        context.TenantId.Should().Be(default(TenantId));
        context.IsResolved.Should().BeFalse();
        context.TenantName.Should().BeEmpty();
    }

    [Fact]
    public void CreateScope_MultipleScopes_LastScopeOverwritesTenant()
    {
        TenantContext context = new();
        TenantContextFactory factory = new(context);
        TenantId firstId = TenantId.New();
        TenantId secondId = TenantId.New();

        IDisposable scope1 = factory.CreateScope(firstId);
        IDisposable scope2 = factory.CreateScope(secondId);

        context.TenantId.Should().Be(secondId);

        scope2.Dispose();
        context.IsResolved.Should().BeFalse();

        scope1.Dispose();
        context.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void CreateScope_ScopeIsDisposable()
    {
        TenantContext context = new();
        TenantContextFactory factory = new(context);

        IDisposable scope = factory.CreateScope(TenantId.New());

        scope.Should().BeAssignableTo<IDisposable>();
        scope.Dispose();
    }

    [Fact]
    public void CreateScope_WithDefaultTenantId_StillSetsContext()
    {
        TenantContext context = new();
        TenantContextFactory factory = new(context);

        using IDisposable scope = factory.CreateScope(default);

        context.TenantId.Should().Be(default(TenantId));
        context.IsResolved.Should().BeTrue();
    }

    [Fact]
    public void CreateScope_DisposeTwice_DoesNotThrow()
    {
        TenantContext context = new();
        TenantContextFactory factory = new(context);

        IDisposable scope = factory.CreateScope(TenantId.New());
        scope.Dispose();

        Action act = () => scope.Dispose();

        act.Should().NotThrow();
    }
}
