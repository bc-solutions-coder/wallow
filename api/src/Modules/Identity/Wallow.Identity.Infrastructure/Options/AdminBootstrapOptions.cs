namespace Wallow.Identity.Infrastructure.Options;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Provisions the bootstrap admin as a global administrator, granting governance across
    /// every tenant. It is deliberately settable only from seeded configuration: no runtime
    /// endpoint grants it.
    /// </summary>
    public bool IsGlobalAdmin { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
}
