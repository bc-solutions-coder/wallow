namespace Wallow.Identity.Infrastructure.Options;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// The organization the bootstrap admin is created as owner of. Required because roles are
    /// granted per organization: an administrator with no organization holds no permission
    /// anywhere and the setup gate never closes.
    /// </summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>
    /// Provisions the bootstrap admin as a global administrator, granting governance across
    /// every tenant. It is deliberately settable only from seeded configuration: no runtime
    /// endpoint grants it.
    /// </summary>
    public bool IsGlobalAdmin { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(OrganizationName);
}
