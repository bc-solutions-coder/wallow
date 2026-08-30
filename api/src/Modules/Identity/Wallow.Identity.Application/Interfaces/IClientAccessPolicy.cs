namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Why a client cannot be served right now. <see cref="Reason"/> is the machine-readable code the
/// auth app's error page renders; <see cref="Description"/> is the sentence an OAuth error carries.
/// </summary>
public sealed record ClientAccessRefusal(string Reason, string Description);

/// <summary>
/// The one answer to "is this client currently serviceable?" — the effective state the authorize
/// and token endpoints both consult, so they cannot drift apart. A registered client is served
/// only while it is active, not platform-suspended, and its organization is neither archived nor
/// platform-suspended. First-party clients are bound to no organization and are never refused
/// here.
/// </summary>
public interface IClientAccessPolicy
{
    /// <summary>The refusal in force for <paramref name="clientId"/>, or null when the client is serviceable.</summary>
    Task<ClientAccessRefusal?> EvaluateAsync(string? clientId, CancellationToken ct = default);
}
