namespace Wallow.E2E.Tests.Infrastructure;

public sealed record OrgAdminTestUser(string Email, string Password, Guid OrgId, string AuthCookie);
