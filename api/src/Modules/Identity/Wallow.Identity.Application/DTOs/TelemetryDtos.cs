namespace Wallow.Identity.Application.DTOs;

public sealed record TelemetryStatusDto(string Status, long Revision, long AcknowledgedRevision, string? Failure, DateTimeOffset? PreviousCredentialExpiresAt = null);

/// <summary>One-time server configuration. Read APIs never return Credential.</summary>
public sealed record TelemetryConfigurationDto(string Endpoint, string Credential, string Environment, string Release);

public sealed record TelemetryEnableResult(TelemetryStatusDto Status, TelemetryConfigurationDto Configuration);
