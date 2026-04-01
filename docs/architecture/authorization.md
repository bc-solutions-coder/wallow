# Authorization Guide

Wallow uses role-based access control (RBAC) with permission expansion. ASP.NET Core Identity with OpenIddict manages authentication and assigns roles; the API expands roles into granular permissions at request time.

---

## How It Works

```
JWT with role claims
        │
        ▼
┌─────────────────────────────┐
│ PermissionExpansionMiddleware │
│ Reads roles from token       │
│ Expands to permission claims │
└─────────────┬───────────────┘
              ▼
┌─────────────────────────────┐
│ [HasPermission] attribute   │
│ Checks permission claims    │
└─────────────────────────────┘
```

1. User authenticates and receives a JWT containing roles (e.g., `admin`, `manager`, `user`)
2. `PermissionExpansionMiddleware` reads the roles and adds permission claims to the request identity
3. Controller actions decorated with `[HasPermission]` check for specific permissions

---

## Adding Permissions to Routes

### Step 1: Choose or Add a Permission

Permissions are defined as string constants in:

```
src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs
```

**Naming convention**: `{Domain}{Action}` — e.g., `InquiriesRead`, `InquiriesWrite`, `WebhooksManage`.

### Step 2: Map Permission to Roles

Edit the role-to-permission mapping in:

```
src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/RolePermissionMapping.cs
```

The mapping uses a `FrozenDictionary<string, string[]>` keyed by role name (case-insensitive). Each role maps to an explicit array of `PermissionType` constants.

### Step 3: Apply to Controller or Action

Add the `[HasPermission]` attribute:

```csharp
using Wallow.Shared.Kernel.Identity.Authorization;

[ApiController]
[Route("api/inquiries/submissions")]
[Authorize]
public class SubmissionsController : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionType.InquiriesRead)]
    public async Task<IActionResult> GetAll() { /* ... */ }

    [HttpPost]
    [HasPermission(PermissionType.InquiriesWrite)]
    public async Task<IActionResult> Create(CreateSubmissionRequest request) { /* ... */ }
}
```

You can apply `[HasPermission]` at the controller level (all actions) or individual action level.

### Step 4: Add Project Reference (if needed)

`HasPermissionAttribute` and `PermissionType` both live in `Wallow.Shared.Kernel`, which all modules already reference. No additional project references are needed.

---

## Adding New Roles

### Step 1: Define the Role

Add the role through the Identity module's role management API or seed it in a database migration.

### Step 2: Map Permissions to the Role

Add the role to `RolePermissionMapping.cs` with an explicit array of `PermissionType` constants.

### Step 3: Assign Role to Users

Assign roles to users via the Identity module's user management API using `IUserManagementService`.

---

## Service Account Permissions

Service accounts (machine-to-machine) and API keys use OAuth2 scopes instead of roles. The middleware detects service accounts by the `client_id` prefix (`sa-` for operator service accounts, `app-` for developer apps) and maps their scopes to permissions.

Scope-to-permission mapping is defined in:

```
src/Shared/Wallow.Shared.Kernel/Identity/Authorization/ScopePermissionMapper.cs
```

**Scope naming convention**: `{domain}.{action}` — e.g., `inquiries.read`, `inquiries.write`.

For regular user tokens, the middleware first expands roles to permissions, then supplements with any granted OAuth2 scopes (covering cases where role claims are absent from the token).

---

## Quick Reference

### Files to Edit

| Task | File |
|------|------|
| Add permission | `Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs` |
| Map permission to role | `Identity.Infrastructure/Authorization/RolePermissionMapping.cs` |
| Map scope to permission | `Shared/Wallow.Shared.Kernel/Identity/Authorization/ScopePermissionMapper.cs` |
| Apply to route | Your controller with `[HasPermission(...)]` |

### Existing Roles

| Role | Description |
|------|-------------|
| `admin` | All permissions (explicitly listed) |
| `manager` | User read, organization management, API keys, SSO read, configuration, inquiries read |
| `user` | Organization read, messaging, notifications, announcements read, storage, API key read/create, inquiries write |

> **Note:** `PermissionType` is a static class with string constants (not a numeric enum). Permissions are grouped by domain area. The current active modules are: Identity, Storage, Notifications, Messaging, Announcements, and Inquiries.

---

## Multi-Tenancy Authorization

Wallow uses JWT claims for multi-tenancy. The `TenantResolutionMiddleware` extracts the tenant ID from standard JWT claims (via `ClaimsPrincipalExtensions.GetTenantId()`) and populates `ITenantContext`.

### How Tenant Resolution Works

```
JWT with tenant claims
        │
        ▼
┌─────────────────────────────────────┐
│ TenantResolutionMiddleware          │
│ - Reads tenant ID/name from claims  │
│ - Sets ITenantContext via setter    │
└─────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────┐
│ EF Core Global Query Filters        │
│ - Automatically filter by TenantId  │
└─────────────────────────────────────┘
```

### Admin Tenant Override

Users with the `admin` role or operator service accounts (client ID prefixed with `sa-`) can switch tenant context using the `X-Tenant-Id` header:

```bash
curl -H "Authorization: Bearer $TOKEN" \
     -H "X-Tenant-Id: 550e8400-e29b-41d4-a716-446655440000" \
     http://localhost:5000/api/inquiries/submissions
```

This allows admins and operator service accounts to view data across tenants for support scenarios. Developer application clients (`app-` prefix) cannot use this override.

### Accessing Tenant Context in Code

Inject `ITenantContext` to access the current tenant:

```csharp
public class InvoiceService(ITenantContext tenantContext)
{
    public async Task<List<Invoice>> GetInvoicesAsync()
    {
        // TenantId is already set by middleware
        // EF Core global query filters handle filtering automatically
    }
}
```

---

## MFA Lockout

MFA lockout is a separate mechanism from ASP.NET Core Identity's password lockout. Both can be active simultaneously — they protect different authentication stages.

### How It Works

```
MFA code submitted
        │
        ▼
┌─────────────────────────────────────────┐
│ Check IsMfaLockedOut()                  │
│ - Active lockout? → 423 immediately     │
└─────────────────────────────────────────┘
        │ Not locked out
        ▼
┌─────────────────────────────────────────┐
│ Validate TOTP / backup code             │
│ - Valid? → complete login, reset count  │
│ - Invalid? → RecordFailure()            │
└─────────────────────────────────────────┘
        │ Invalid
        ▼
┌─────────────────────────────────────────┐
│ IMfaLockoutService.RecordFailureAsync() │
│ - Increment MfaFailedAttempts           │
│ - On 5th failure: lock + escalate       │
└─────────────────────────────────────────┘
```

### Lockout Thresholds and Durations

A lockout triggers after **5 consecutive failed MFA attempts**. Each subsequent lockout doubles the duration (exponential backoff), capped at 24 hours:

| Lockout # | Duration |
|-----------|----------|
| 1st | 15 minutes |
| 2nd | 30 minutes |
| 3rd | 1 hour |
| 4th | 2 hours |
| 5th+ | capped at 24 hours |

The `MfaLockoutCount` on the user record tracks how many times the user has been locked out. It is only reset by an admin clear (not by a successful login), so repeat offenders accumulate progressively longer lockouts.

### Error Response

When a user is locked out, the API returns HTTP **423 Locked**:

```json
{ "succeeded": false, "error": "mfa_locked_out" }
```

The lockout end time is not included in the response body — clients should display a generic "too many attempts" message and not expose the exact unlock time to callers.

A `UserMfaLockedOutEvent` is published on the Wolverine bus when a lockout occurs, allowing the Notifications module to email the user.

### Relationship to Password Lockout

ASP.NET Core Identity's built-in password lockout (failed `SignInManager.PasswordSignInAsync` calls) operates independently of MFA lockout:

- **Password lockout** triggers during the password step and uses Identity's `LockoutEnd` / `AccessFailedCount` fields.
- **MFA lockout** triggers during the MFA step and uses `MfaLockoutEnd` / `MfaFailedAttempts` fields on `WallowUser`.

A user could be subject to both simultaneously. The admin clear-lockout endpoint (below) resets **both**.

### Admin Clear-Lockout Endpoint

Admins can clear all lockout state for a user — both password lockout and MFA lockout — via:

```
POST /api/v1/identity/mfa/admin/{userId}/clear-lockout
```

Requires: `Authorization: Bearer <admin-token>` (caller must have the `admin` role).

**What it clears:**
- `LockoutEnd` and `AccessFailedCount` (Identity password lockout)
- `MfaLockoutEnd`, `MfaFailedAttempts`, and `MfaLockoutCount` (MFA lockout, including the escalation counter)
- The Redis cache entry for the MFA lockout

A `UserMfaLockoutClearedEvent` is published after a successful clear, recording which admin performed the action.

**Example:**

```bash
curl -X POST \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  http://localhost:5001/api/v1/identity/mfa/admin/550e8400-e29b-41d4-a716-446655440000/clear-lockout
```

**Response:**

```json
{ "succeeded": true }
```

**Troubleshooting MFA lockout**
- `423` on MFA submit but lockout time has passed — the Redis cache entry may outlive the DB record in edge cases; an admin clear resolves this
- Admin clear returns `404` — verify the user ID is correct; the endpoint looks up by Identity user ID (GUID), not email

---

## Middleware Pipeline Order

The authorization middleware must be registered in the correct order in `Program.cs`:

```
1. UseAuthentication()           - OpenIddict JWT validation
2. TenantResolutionMiddleware    - Reads tenant claims → ITenantContext
3. PermissionExpansionMiddleware - Expands roles/scopes → permission claims
4. UseAuthorization()            - Enforces [HasPermission] attributes
```

**Warning**: Reordering these middlewares will break authorization. `PermissionExpansionMiddleware` requires an authenticated user to have claims to expand.

---

## Troubleshooting

**403 Forbidden but user has the role**
- Check `RolePermissionMapping` includes the permission for that role
- Verify the role name matches (comparison is case-insensitive)
- Check the JWT contains the role claim (decode at jwt.io)

**Permission not being checked**
- Ensure `[Authorize]` is on the controller (authentication required first)
- Verify `[HasPermission]` attribute is applied
- `HasPermissionAttribute` is in `Wallow.Shared.Kernel` — all modules reference this already

**Service account getting 403**
- Verify the scope is in the token
- Check `ScopePermissionMapper.MapScopeToPermission` includes the mapping
- Confirm the client ID prefix is correct (`sa-` or `app-`)
