# Security Audit: Authentication & Access Control

**Auditor:** auth-scout
**Date:** 2026-03-03
**Scope:** Authentication, authorization, RBAC, IDOR, Wolverine handler auth, SignalR security
**Codebase:** Foundry .NET modular monolith (expansion branch, commit 9f2666e1)

---

## CRITICAL Findings

### AUTH-001: No JWT Authentication Scheme Registered in Production Code

**Severity:** CRITICAL
**Files:** `src/Foundry.Api/Program.cs`, `src/Modules/Identity/Foundry.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`

**Description:** The production code never calls `AddAuthentication()` or `AddJwtBearer()`. The middleware pipeline calls `app.UseAuthentication()` at line 352 of `Program.cs`, but no authentication scheme is registered in the service container. The only place `AddAuthentication`/`AddJwtBearer` appear is in test code:

- `tests/Foundry.Tests.Common/Factories/FoundryApiFactory.cs:155` -- registers a `"Test"` scheme
- `tests/Modules/Identity/Foundry.Identity.IntegrationTests/KeycloakIntegrationTestBase.cs:94` -- registers `JwtBearerDefaults.AuthenticationScheme` for Keycloak integration tests

The `Keycloak` and `KeycloakAdmin` configuration sections exist in `appsettings.json` but are only consumed by `AddKeycloakAdminHttpClient()` (the admin SDK for server-to-server calls), NOT for JWT bearer token validation of incoming API requests.

**Impact:** Without a registered authentication scheme, `app.UseAuthentication()` is a no-op. The `GetFallbackPolicyAsync()` in `PermissionAuthorizationPolicyProvider` returns `RequireAuthenticatedUser()`, which should block unauthenticated requests to `[Authorize]` endpoints. However, the combination means ASP.NET Core's authentication middleware cannot validate JWTs. In practice, this is mitigated by the fallback policy, but JWT tokens are never validated -- no signature verification, no expiry checks, no issuer/audience validation occurs.

**Recommendation:** Register `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()` with proper Keycloak OIDC configuration (authority, audience, RequireHttpsMetadata) in the production service registration. Use the `Keycloak.AuthServices.Authentication` package that is already referenced.

---

### AUTH-002: Admin Tenant Override via X-Tenant-Id Header Has No Audit Trail Persistence

**Severity:** HIGH
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs:49-58`

```csharp
string? headerTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
if (!string.IsNullOrEmpty(headerTenantId) &&
    HasRealmAdminRole(context.User) &&
    Guid.TryParse(headerTenantId, out Guid overrideId))
{
    tenantSetter.SetTenant(TenantId.Create(overrideId));
    LogAdminTenantOverride(overrideId, resolvedTenantId, userId, requestPath);
}
```

**Description:** Any user with the "admin" role can impersonate any tenant by setting the `X-Tenant-Id` header. While this is logged via structured logging, there is:
1. No validation that the target tenant actually exists
2. No persistent audit trail (only log messages, which may be transient)
3. No rate limiting on tenant switching
4. No notification to the target tenant that an admin accessed their data

**Impact:** A compromised admin account can silently access all tenant data. If logs are not monitored, this access goes undetected.

**Recommendation:**
- Validate the override tenant ID exists before setting the context
- Persist admin override actions to the audit table (not just logs)
- Consider requiring a second factor or explicit admin impersonation session
- Add alerting on admin tenant overrides

---

## HIGH Findings

### AUTH-003: Wolverine Message Handlers Have No Authorization Checks

**Severity:** HIGH
**Files:** All `*Handler.cs` files under `src/Modules/*/Application/`

**Description:** All Wolverine command/query handlers (approximately 40+ handlers across Billing, Communications, Configuration, Storage, and Identity modules) perform no authorization checks. They are pure application-layer handlers that process commands directly. While the API controllers that invoke these handlers via `IMessageBus.InvokeAsync()` do have `[HasPermission]` attributes, the handlers themselves are unprotected.

Wolverine auto-discovers handlers in all `Foundry.*` assemblies (Program.cs line 148). If a handler is invoked via:
- A RabbitMQ message (when `ModuleMessaging:Transport` is set to `"RabbitMq"`)
- A Wolverine scheduled/cascading message
- Any code path that bypasses the API controller layer

...then no authorization check occurs.

**Examples of unprotected handlers:**
- `CreateInvoiceHandler` -- creates invoices without verifying the caller has `InvoicesWrite` permission
- `ProcessPaymentHandler` -- processes payments without checking `PaymentsWrite`
- `SendEmailHandler` -- sends emails without any auth check
- `DeleteFileHandler` -- deletes storage files
- `CreateFeatureFlagHandler` -- modifies feature flags

**Impact:** If messages can be injected into the message bus (e.g., via a RabbitMQ compromise or internal code path), all operations can be performed without authorization.

**Recommendation:** Add a Wolverine middleware policy that validates permissions on command handlers, or add explicit authorization checks within each handler that performs a write operation.

---

### AUTH-004: SCIM Controller Uses [AllowAnonymous] with Middleware-Based Auth

**Severity:** HIGH
**File:** `src/Modules/Identity/Foundry.Identity.Api/Controllers/ScimController.cs:20`

```csharp
[AllowAnonymous] // SCIM uses Bearer token authentication via middleware (not OAuth)
```

**Description:** The SCIM controller is marked `[AllowAnonymous]`, relying entirely on `ScimAuthenticationMiddleware` to authenticate requests. This creates a layered defense gap:

1. The SCIM middleware runs before `UseAuthorization()` in the pipeline, but the `[AllowAnonymous]` attribute tells the authorization middleware to skip all policy checks
2. The SCIM middleware creates a `ClaimsPrincipal` with `"scim_client"` and `"auth_method"` claims but NO permission claims
3. The SCIM endpoints have no permission checks whatsoever -- once the bearer token validates, full CRUD on users and groups is available

**Impact:** A valid SCIM token grants unrestricted access to create, update, delete users and groups with no fine-grained permission control. SCIM token compromise gives complete identity management access.

**Recommendation:**
- Add SCIM-specific authorization policies (e.g., read-only SCIM tokens vs. full provisioning tokens)
- Consider adding scope restrictions to SCIM tokens
- Remove `[AllowAnonymous]` and use a proper SCIM authentication scheme that participates in the ASP.NET Core auth pipeline

---

### AUTH-005: Organization Access Control Missing on Multiple User Operations

**Severity:** HIGH
**File:** `src/Modules/Identity/Foundry.Identity.Api/Controllers/UsersController.cs`

**Description:** While `GetUserById` correctly validates that the requested user belongs to the current tenant's organization (lines 63-69), several other user operations do NOT perform this check:

- `DeactivateUser` (line 88) -- can deactivate any user by GUID
- `ActivateUser` (line 99) -- can activate any user by GUID
- `AssignRole` (line 110) -- can assign roles to any user by GUID
- `RemoveRole` (line 123) -- can remove roles from any user by GUID

```csharp
[HttpPost("{id:guid}/deactivate")]
[HasPermission(PermissionType.UsersUpdate)]
public async Task<ActionResult> DeactivateUser(Guid id, CancellationToken ct)
{
    // No tenant/org ownership check!
    await _keycloakAdmin.DeactivateUserAsync(id, ct);
    return NoContent();
}
```

**Impact:** A user with `UsersUpdate` permission in Tenant A can deactivate/activate users in Tenant B by guessing their GUID. Similarly, `RolesUpdate` permission holders can assign admin roles to users in other tenants.

**Recommendation:** Add the same organization membership check used in `GetUserById` to all user mutation endpoints. Extract the pattern into a shared helper method.

---

### AUTH-006: Organization Endpoints Allow Cross-Tenant Access

**Severity:** HIGH
**File:** `src/Modules/Identity/Foundry.Identity.Api/Controllers/OrganizationsController.cs`

**Description:** The Organizations controller operations do not validate that the targeted organization belongs to the current tenant:

- `GetById` -- returns any organization by GUID regardless of tenant
- `GetMembers` -- lists members of any organization
- `AddMember` -- adds users to any organization
- `RemoveMember` -- removes users from any organization

All require `OrganizationsRead` or `OrganizationsManageMembers` permissions, but these permissions are not scoped to a specific organization.

**Impact:** A manager-role user in Tenant A can enumerate and modify member lists of Tenant B's organization if they know the organization GUID.

**Recommendation:** Validate that the target organization ID matches the current tenant's organization before performing any operation.

---

## MEDIUM Findings

### AUTH-007: Admin Role Gets All Permissions Including Future Ones

**Severity:** MEDIUM
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/RolePermissionMapping.cs:10`

```csharp
["admin"] = PermissionType.All.ToArray(),
```

**Description:** The admin role receives ALL permissions via `PermissionType.All`, which uses reflection to enumerate all string constants in the `PermissionType` class. This means any new permission added to any module automatically grants it to all admin users.

**Impact:** Adding a new dangerous permission (e.g., `SystemPurge`, `DataExport`) immediately grants it to all admins without explicit review. This violates the principle of least privilege and makes permission grants invisible in code review.

**Recommendation:** Explicitly list admin permissions rather than using `PermissionType.All`. This forces a conscious decision when granting new permissions to admins.

---

### AUTH-008: Account Enumeration via Token Endpoint Error Messages

**Severity:** MEDIUM
**File:** `src/Modules/Identity/Foundry.Identity.Api/Controllers/AuthController.cs:60-67`

```csharp
return Unauthorized(new ProblemDetails
{
    Title = "Authentication failed",
    Detail = result.ErrorDescription ?? result.Error ?? "Invalid credentials",
    Status = StatusCodes.Status401Unauthorized,
    Extensions = { ["error"] = result.Error }
});
```

**Description:** The token endpoint forwards Keycloak's error messages directly to the client. Keycloak may return different error descriptions for invalid username vs. invalid password (e.g., `"Account is not fully set up"`, `"Account disabled"`, `"Invalid user credentials"`), enabling account enumeration.

**Impact:** An attacker can determine which email addresses have registered accounts by observing different error responses.

**Recommendation:** Return a generic error message like `"Invalid email or password"` for all authentication failures. Log the specific Keycloak error server-side.

---

### AUTH-009: Rate Limiting on Auth Endpoints May Be Insufficient

**Severity:** MEDIUM
**File:** `src/Foundry.Api/Extensions/RateLimitDefaults.cs`

```csharp
public const int AuthPermitLimit = 5;
public const int AuthWindowMinutes = 5;
```

**Description:** The auth rate limit allows 5 attempts per 5-minute window per IP address. This translates to 60 password attempts per hour, or 1,440 per day per IP. While this provides some protection, it may be insufficient against:
- Distributed brute force attacks (different IPs)
- Credential stuffing (using leaked credentials from other breaches)
- The refresh token endpoint also uses the "auth" rate limit policy

**Impact:** A moderately resourced attacker can attempt thousands of passwords daily using distributed IPs.

**Recommendation:**
- Consider progressive delays (exponential backoff) on repeated failures
- Add per-account rate limiting in addition to per-IP
- Implement account lockout after N failed attempts (delegate to Keycloak's brute force detection)
- Separate rate limits for token vs. refresh endpoints

---

### AUTH-010: No Token Revocation Endpoint

**Severity:** MEDIUM
**File:** `src/Modules/Identity/Foundry.Identity.Api/Controllers/AuthController.cs`

**Description:** The AuthController provides `token` and `refresh` endpoints but no `logout` or `revoke` endpoint. There is no way for clients to invalidate an access token or refresh token before expiry.

**Impact:** If a token is compromised, it remains valid until expiry. For refresh tokens, which typically have longer lifetimes, this can leave a large window of vulnerability.

**Recommendation:** Add a `/auth/logout` endpoint that revokes the refresh token via Keycloak's token revocation endpoint (`/protocol/openid-connect/revoke`).

---

### AUTH-011: Hangfire Dashboard Auth Allows Any "Admin" Role in Production

**Severity:** MEDIUM
**File:** `src/Foundry.Api/Middleware/HangfireDashboardAuthFilter.cs:19-22`

```csharp
HttpContext httpContext = context.GetHttpContext();
return httpContext.User.Identity?.IsAuthenticated == true
    && httpContext.User.IsInRole("Admin");
```

**Description:** The Hangfire dashboard in production requires only the "Admin" role (case-sensitive). However:
1. The role check uses `IsInRole("Admin")` with capital A, while `RolePermissionMapping` uses lowercase `"admin"`. This may cause a mismatch depending on how roles are mapped from JWT claims.
2. Hangfire dashboard provides access to background job details, retry capabilities, and potentially sensitive job parameters.
3. There is no additional CSP protection beyond the relaxed policy for `/hangfire` paths.

**Impact:** Either no admin can access the dashboard (role case mismatch), or any admin user across any tenant can access the global Hangfire dashboard and see cross-tenant job information.

**Recommendation:**
- Standardize role name casing (use `StringComparison.OrdinalIgnoreCase`)
- Consider requiring a specific `AdminAccess` permission instead of a role
- Restrict dashboard to specific IP ranges in production

---

### AUTH-012: API Key Auth Creates Identity Without Organization Claim

**Severity:** MEDIUM
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/ApiKeyAuthenticationMiddleware.cs:73-84`

```csharp
List<Claim> claims =
[
    new(ClaimTypes.NameIdentifier, result.UserId!.Value.ToString()),
    new("sub", result.UserId!.Value.ToString()),
    new("api_key_id", result.KeyId!),
    new("auth_method", "api_key")
];
```

**Description:** When authenticating via API key, the middleware creates a `ClaimsPrincipal` without an `organization` claim. The tenant context is set directly from the API key's stored tenant ID, bypassing the normal JWT organization claim flow. This means:
1. API key-authenticated requests skip the `TenantResolutionMiddleware` logic (already set by ApiKeyAuthMiddleware)
2. There is no role claim, so `PermissionExpansionMiddleware` relies solely on scope-to-permission mapping
3. If no scopes are specified on the API key, the principal has zero permissions but is authenticated

**Impact:** An API key with no scopes can authenticate but has no permissions, which is correct behavior. However, the lack of an organization claim means certain code paths that check `context.User.FindFirst("organization")` will return null for API key requests.

**Recommendation:** Add the tenant ID as an `organization` claim to the API key identity for consistency. Consider adding a role claim (e.g., `"service_account"`) for clearer identity semantics.

---

## LOW Findings

### AUTH-013: SignalR Hub Has Proper Auth but Limited Group Validation

**Severity:** LOW
**File:** `src/Foundry.Api/Hubs/RealtimeHub.cs`

**Description:** The `RealtimeHub` correctly uses `[Authorize]` and validates tenant groups. However:
1. `JoinGroup` allows joining non-tenant-prefixed groups without validation (line 60-62: early return if not `"tenant:"` prefixed)
2. `UpdatePageContext` adds connections to `page:{pageContext}` groups without validating the page context format, enabling injection of arbitrary group names

**Impact:** Low risk -- a user could join arbitrary non-tenant groups and receive messages intended for those groups, but this only matters if the application sends sensitive data to non-tenant-prefixed groups.

**Recommendation:** Validate all group name formats, not just tenant-prefixed ones. Consider using an allowlist of valid group name patterns.

---

### AUTH-014: Service Account Endpoints Reuse ApiKeysRead/Create/Update/Delete Permissions

**Severity:** LOW
**File:** `src/Modules/Identity/Foundry.Identity.Api/Controllers/ServiceAccountsController.cs`

**Description:** Service account management endpoints use `ApiKeysRead`, `ApiKeysCreate`, `ApiKeysUpdate`, `ApiKeysDelete` permissions rather than dedicated `ServiceAccountsXxx` permissions. This means any user who can manage API keys can also manage service accounts, despite these being different security primitives.

**Impact:** Less granular access control than expected. API key management and service account management are conflated.

**Recommendation:** Define separate `ServiceAccountsRead`, `ServiceAccountsCreate`, etc. permissions for clearer separation of concerns.

---

### AUTH-015: Changelog Controller is Publicly Accessible

**Severity:** LOW (Intentional)
**File:** `src/Modules/Communications/Foundry.Communications.Api/Controllers/ChangelogController.cs:18`

```csharp
[AllowAnonymous]
```

**Description:** The changelog endpoint is marked `[AllowAnonymous]`, making version/changelog information available without authentication. This is likely intentional for a public-facing changelog, but it does expose:
- Software version history
- Feature descriptions
- Timing of releases

**Impact:** Information disclosure about the application's feature timeline. Useful for attackers to identify when vulnerabilities might have been patched.

**Recommendation:** Confirm this is intentional. If changelog data should be tenant-scoped, add authentication.

---

## Positive Findings

The following security controls are properly implemented:

1. **Fallback Authorization Policy** -- `GetFallbackPolicyAsync()` returns `RequireAuthenticatedUser()`, ensuring endpoints without explicit `[AllowAnonymous]` require authentication.

2. **Permission-Based Authorization** -- The `HasPermissionAttribute` + `PermissionAuthorizationHandler` + `PermissionAuthorizationPolicyProvider` pattern provides a clean, extensible RBAC system.

3. **API Key Hashing** -- API keys are stored as SHA-256 hashes in Redis (`RedisApiKeyService`), never in plaintext.

4. **Rate Limiting** -- Auth endpoints have rate limiting applied via `[EnableRateLimiting("auth")]`.

5. **Security Headers** -- `SecurityHeadersMiddleware` applies CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, HSTS (production), and Permissions-Policy.

6. **HTML Sanitization** -- Messaging endpoints use `IHtmlSanitizationService` to sanitize user input before storage.

7. **SignalR Cross-Tenant Validation** -- The `RealtimeHub` validates tenant groups to prevent cross-tenant message access.

8. **Conversation Participant Check** -- `ConversationsController.GetMessages()` validates participant membership before returning messages.

9. **SCIM Discovery Endpoints** -- Correctly exempted from authentication per SCIM spec.

10. **API Key Expiration** -- API keys support expiration dates with both Redis TTL and application-level checks.

---

## Summary

| Severity | Count | Key Issue |
|----------|-------|-----------|
| CRITICAL | 1     | No JWT authentication scheme registered |
| HIGH     | 4     | Handler auth gaps, SCIM auth, IDOR on user/org operations |
| MEDIUM   | 6     | Admin role permissions, account enumeration, rate limiting, token revocation |
| LOW      | 3     | SignalR groups, permission reuse, public changelog |

The most urgent fix is **AUTH-001** (registering a JWT Bearer authentication scheme), as without it the application has no mechanism to validate JWT tokens from Keycloak. The fallback authorization policy provides partial protection by requiring authenticated users, but no actual authentication occurs against incoming tokens.
