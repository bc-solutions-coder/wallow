namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Why a client cannot be served right now. <see cref="Reason"/> is the machine-readable code the
/// auth app's error page renders; <see cref="Description"/> is the sentence an OAuth error carries.
/// </summary>
public sealed record ClientAccessRefusal(string Reason, string Description);

/// <summary>
/// Evaluates client and organization suspension/archive state for registered clients.
/// Missing records receive no refusal here; callers must validate client existence separately.
/// </summary>
public interface IClientAccessPolicy
{
    /// <summary>
    /// Returns the applicable refusal or null when this policy finds none.
    /// </summary>
    Task<ClientAccessRefusal?> EvaluateAsync(string? clientId, CancellationToken ct = default);
}
