namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record MfaVerifyRequest(string Code, bool UseBackupCode = false);
