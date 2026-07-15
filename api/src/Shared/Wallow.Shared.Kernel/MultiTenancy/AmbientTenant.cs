using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

/// <summary>
/// Carries the current request's tenant through the async execution context.
/// Used by EF Core global query filters so that tenant filtering works regardless
/// of which DbContext instance handles the query (critical for pooled contexts
/// and Wolverine handler resolution).
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
