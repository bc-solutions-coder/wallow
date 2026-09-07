using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

/// <summary>
/// Carries the tenant through async execution. Used as a fallback when initializing
/// scoped database contexts and stamping message headers.
/// </summary>
public static class AmbientTenant
{
    private static readonly AsyncLocal<TenantId> _currentValue = new();

    public static TenantId Current
    {
        get => _currentValue.Value;
        set => _currentValue.Value = value;
    }
}
