namespace Wallow.Identity.Domain.Enums;

/// <summary>
/// What a registered client is to the organization that owns it: an application a person signs
/// in to, or a service account that authenticates on its own behalf.
/// </summary>
public enum RegisteredClientKind
{
    Application,
    ServiceAccount,
}
