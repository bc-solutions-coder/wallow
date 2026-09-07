namespace Wallow.Identity.Domain.Enums;

/// <summary>
/// Distinguishes interactive applications from service accounts.
/// </summary>
public enum RegisteredClientKind
{
    Application,
    ServiceAccount,
}
