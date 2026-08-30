namespace Wallow.Shared.Kernel.Configuration;

/// <summary>
/// The fork's own product identity as the backend knows it. The frontends read the app name from
/// their build-time branding; deployments mirror it here (section <c>Branding</c>) so the API can
/// keep a developer application from dressing up as the platform itself.
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
