## GENERAL RULES
- Always use the correct type instead of var
- **JWT claim access**: Never use raw `FindFirst`/`FindFirstValue`/`FindAll` on `ClaimsPrincipal`. Always use `ClaimsPrincipalExtensions` from `Wallow.Shared.Kernel.Extensions`. Available methods:
  - Single-value: `GetUserId()`, `GetClientId()`, `GetTenantId()`, `GetTenantName()`, `GetEmail()`, `GetDisplayName()`, `GetFirstName()`, `GetLastName()`, `GetAuthMethod()`, `GetTenantRegion()`, `GetPlan()`
  - Multi-value: `GetRoles()`, `GetPermissions()`, `GetScopes()` — return `IReadOnlyList<string>`
  - If a needed claim has no extension, add one to `ClaimsPrincipalExtensions` rather than using raw `FindFirst`
