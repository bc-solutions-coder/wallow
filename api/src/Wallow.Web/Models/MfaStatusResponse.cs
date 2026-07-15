namespace Wallow.Web.Models;

public sealed record MfaStatusResponse(bool Enabled, string? Method, int BackupCodeCount);
