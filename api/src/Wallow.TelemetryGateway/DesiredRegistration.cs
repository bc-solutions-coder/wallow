using System.Text.Json.Serialization;

namespace Wallow.TelemetryGateway;

[JsonConverter(typeof(JsonStringEnumConverter<RegistrationState>))]
public enum RegistrationState
{
    Enabled,
    Disabled,
    Deleted,
}

public sealed record DesiredCredential(string Id, string Verifier);

public sealed record CredentialRotation(Guid OperationId, string PreviousCredentialId, string NewCredentialId);

public sealed record DesiredRegistration(
    long Revision,
    string ClientId,
    string ApplicationId,
    string BrowserService,
    string ServerService,
    IReadOnlyList<string> Environments,
    RegistrationState State,
    IReadOnlyList<DesiredCredential> Credentials,
    CredentialRotation? Rotation);

public sealed record RegistrationAcknowledgement(Guid RegistrationId, long Revision, DateTimeOffset AcknowledgedAt, DateTimeOffset? PreviousCredentialExpiresAt = null);
