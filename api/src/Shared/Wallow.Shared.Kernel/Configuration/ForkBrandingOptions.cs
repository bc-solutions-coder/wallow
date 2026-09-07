namespace Wallow.Shared.Kernel.Configuration;

/// <summary>
/// Backend product identity from <c>Branding</c>. Keep AppName aligned with frontend branding
/// so client display names cannot impersonate the platform.
/// </summary>
public sealed class ForkBrandingOptions
{
    public const string SectionName = "Branding";

    public string AppName { get; set; } = "Wallow";

    /// <summary>A display name that would read as the platform itself is reserved.</summary>
    public bool IsReservedDisplayName(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        return string.Equals(displayName.Trim(), AppName.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
