namespace Wallow.E2E.Tests.Infrastructure;

public sealed record UnverifiedTestUser(string Email, string Password, string VerificationLink);
