using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>Desired ingestion access and acknowledged provisioning state, retained independently of OAuth secrets.</summary>
public enum TelemetryAccessState { Enabled, Disabled, Deleted }

public sealed class TelemetryRegistration : Entity<RegisteredClientId>
{
    public string ClientId { get; private set; } = string.Empty;
    public Guid OrganizationId { get; private set; }
    public long Revision { get; private set; } = 1;
    public long AcknowledgedRevision { get; private set; }
    public string CredentialId { get; private set; } = string.Empty;
    public string Verifier { get; private set; } = string.Empty;
    public DateTimeOffset NextAttemptAt { get; private set; }
    public int Attempts { get; private set; }
    public string? Failure { get; private set; }
    public Guid ConcurrencyStamp { get; private set; } = Guid.NewGuid();
    public TelemetryAccessState AccessState { get; private set; }
    public string? PreviousCredentialId { get; private set; }
    public string? PreviousVerifier { get; private set; }
    public Guid? RotationId { get; private set; }
    public DateTimeOffset? PreviousCredentialExpiresAt { get; private set; }
    public string Status => AccessState != TelemetryAccessState.Enabled
        ? AcknowledgedRevision == Revision ? "disabled" : "pending-revocation"
        : Failure is not null ? "action-required"
        : AcknowledgedRevision == Revision ? "active"
        : RotationId is not null ? "pending-rotation" : "pending";

    public void Rotate(string credentialId, string verifier, TimeProvider clock)
    {
        if (AccessState != TelemetryAccessState.Enabled || AcknowledgedRevision != Revision
            || PreviousCredentialExpiresAt > clock.GetUtcNow())
        {
            throw new BusinessRuleException(IdentityErrors.TelemetryChangePending);
        }
        PreviousCredentialId = CredentialId;
        PreviousVerifier = Verifier;
        CredentialId = credentialId;
        Verifier = verifier;
        RotationId = Guid.NewGuid();
        PreviousCredentialExpiresAt = null;
        Changed(clock);
    }

    public void Enable(string credentialId, string verifier, TimeProvider clock)
    {
        if (AccessState != TelemetryAccessState.Disabled || AcknowledgedRevision != Revision)
        {
            throw new BusinessRuleException(IdentityErrors.TelemetryChangePending);
        }
        AccessState = TelemetryAccessState.Enabled;
        CredentialId = credentialId;
        Verifier = verifier;
        ClearRotation();
        Changed(clock);
    }

    public void Revoke(bool deleted, TimeProvider clock)
    {
        TelemetryAccessState target = deleted ? TelemetryAccessState.Deleted : TelemetryAccessState.Disabled;
        if (AccessState == TelemetryAccessState.Deleted || AccessState == target) { return; }
        AccessState = target;
        ClearRotation();
        Changed(clock);
    }

    private void ClearRotation()
    {
        PreviousCredentialId = null;
        PreviousVerifier = null;
        RotationId = null;
        PreviousCredentialExpiresAt = null;
    }

    private void Changed(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Revision++;
        Failure = null;
        Attempts = 0;
        NextAttemptAt = clock.GetUtcNow();
        ConcurrencyStamp = Guid.NewGuid();
    }

    private TelemetryRegistration() { }

    public static TelemetryRegistration Create(RegisteredClient client, string credentialId, string verifier, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(clock);
        return new TelemetryRegistration
        {
            Id = client.Id,
            ClientId = client.ClientId,
            OrganizationId = client.OrganizationId,
            CredentialId = credentialId,
            Verifier = verifier,
            NextAttemptAt = clock.GetUtcNow(),
        };
    }

    public void Acknowledge(long revision, TimeProvider clock, DateTimeOffset? previousCredentialExpiresAt = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (revision != Revision)
        {
            return;
        }

        AcknowledgedRevision = revision;
        PreviousCredentialExpiresAt ??= previousCredentialExpiresAt;
        Attempts = 0;
        Failure = null;
        NextAttemptAt = clock.GetUtcNow().AddMinutes(5);
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void RequireConfiguration(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Failure = "Observability is not configured. Save this credential and contact the platform operator for the collection endpoint.";
        NextAttemptAt = clock.GetUtcNow().AddMinutes(5);
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void Retry(bool permanent, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Attempts = Math.Min(Attempts + 1, 10);
        Failure = permanent ? "Provisioning was rejected. Contact the platform operator." : null;
        NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Min(300, Math.Pow(2, Attempts)));
        ConcurrencyStamp = Guid.NewGuid();
    }
}
