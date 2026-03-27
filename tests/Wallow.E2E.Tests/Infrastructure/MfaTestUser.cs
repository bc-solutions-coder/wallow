namespace Wallow.E2E.Tests.Infrastructure;

public sealed record MfaTestUser(string Email, string Password, string TotpSecret, IReadOnlyList<string> BackupCodes);
