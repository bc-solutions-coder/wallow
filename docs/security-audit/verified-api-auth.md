# Verified Security Findings: API Endpoints & Auth/Access Control

**Verifier:** verifier-api-auth
**Date:** 2026-03-03
**Source Reports:** `sweep-api-endpoints.md` (api-scout), `sweep-auth-access.md` (auth-scout)

---

## API Endpoint Findings (from api-scout)

### C-1: Quota Admin Endpoints Missing Authorization Policy
- **Original Severity:** Critical
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Billing/Wallow.Billing.Api/Controllers/QuotasController.cs:48-89`. The controller has `[Authorize]` at class level (line 19) but the `SetOverride` (line 48) and `RemoveOverride` (line 71) endpoints have no `[HasPermission]` attribute. These accept an arbitrary `tenantId:guid` path parameter, meaning any authenticated user can modify quotas for any tenant. The `GetAll` endpoint (line 35) also lacks `[HasPermission]`.
- **Adjusted Severity:** Critical (unchanged)
- **Notes:** All three endpoints on this controller lack permission checks. The `GetAll` is less severe (read-only, own tenant), but `SetOverride` and `RemoveOverride` are cross-tenant write operations with no authorization guard beyond basic authentication. This is a genuine privilege escalation vulnerability.

---

### C-2: MetersController Missing Permission Check
- **Original Severity:** Critical
- **Verdict:** PARTIALLY CONFIRMED -- severity downgraded
- **Evidence:** `src/Modules/Billing/Wallow.Billing.Api/Controllers/MetersController.cs:31-39`. The `GetAll` endpoint has `[Authorize]` (class level, line 16) but no `[HasPermission]`. Any authenticated user can list all meter definitions.
- **Adjusted Severity:** Low
- **Notes:** Meter definitions are configuration metadata (names, codes, descriptions), not sensitive billing data. This is an information disclosure of internal configuration, not a critical vulnerability. The scout rated this Critical because it was grouped with C-1, but reading the actual code, this is a read-only endpoint returning meter definition metadata. Downgraded to Low.

---

### H-1: AdminAnnouncementsController Missing [Authorize] Attribute
- **Original Severity:** High
- **Verdict:** FALSE POSITIVE
- **Evidence:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/AdminAnnouncementsController.cs:23`. The controller uses `[HasPermission(PermissionType.AnnouncementManage)]` at class level. `HasPermissionAttribute` (at `src/Shared/Wallow.Shared.Kernel/Identity/Authorization/HasPermissionAttribute.cs:6-15`) **extends `AuthorizeAttribute`** directly: `public sealed class HasPermissionAttribute : AuthorizeAttribute`. This means `[HasPermission]` IS an `[Authorize]` attribute -- it inherits all of `AuthorizeAttribute`'s behavior including requiring an authenticated user. The `PermissionAuthorizationPolicyProvider` (line 33-34) also has a fallback policy that requires authenticated users.
- **Adjusted Severity:** N/A (not a vulnerability)
- **Notes:** The scout explicitly noted "depending on how the custom HasPermissionAttribute is implemented" as a caveat but did not read the implementation. `HasPermissionAttribute` inherits from `AuthorizeAttribute`, so it implicitly requires authentication. The absence of a separate `[Authorize]` is a style inconsistency, not a security gap.

---

### H-2: AdminChangelogController Missing [Authorize] Attribute
- **Original Severity:** High
- **Verdict:** FALSE POSITIVE
- **Evidence:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/AdminChangelogController.cs:19`. Same pattern as H-1. `[HasPermission(PermissionType.ChangelogManage)]` inherits from `AuthorizeAttribute`.
- **Adjusted Severity:** N/A (not a vulnerability)
- **Notes:** Identical to H-1. `HasPermissionAttribute : AuthorizeAttribute` means this is already secured.

---

### H-3: Announcements Dismiss Endpoint Missing Permission Check
- **Original Severity:** High
- **Verdict:** PARTIALLY CONFIRMED -- severity downgraded
- **Evidence:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/AnnouncementsController.cs:71-89`. The `DismissAnnouncement` endpoint indeed has no `[HasPermission]` attribute while the `GetAnnouncements` endpoint on the same controller requires `AnnouncementRead`. However, the controller has `[Authorize]` at class level (line 21), so authentication is enforced. The dismiss action (lines 78-85) uses `_currentUserService.GetCurrentUserId()` and passes the user's own ID to the `DismissAnnouncementCommand`, so it only affects the calling user's dismissal state.
- **Adjusted Severity:** Low
- **Notes:** This is a user-scoped action (dismiss for self only). A user without `AnnouncementRead` could dismiss announcements they cannot see, which is a minor inconsistency but not exploitable. The scout correctly identified the inconsistency but overrated the severity.

---

### H-4: Health Check Endpoints Expose Infrastructure Details in Non-Production
- **Original Severity:** High
- **Verdict:** CONFIRMED
- **Evidence:** `src/Wallow.Api/Program.cs:421-452`. The `WriteHealthCheckResponse` method exposes `e.Value.Exception?.Message` for non-production environments (line 447). Health check endpoints are `AllowAnonymous` (lines 320-335). In staging environments, exception messages from PostgreSQL, RabbitMQ, Redis, and Hangfire health checks could reveal infrastructure details.
- **Adjusted Severity:** Medium
- **Notes:** The guard is `env.IsProduction()` (line 428), so staging/pre-prod environments are exposed. This is a valid concern but is Medium rather than High since it requires (a) access to a non-production environment and (b) a failing health check to trigger exception messages. The normal healthy response contains only names and durations, which are low-sensitivity.

---

### M-1: SCIM Controller Uses [AllowAnonymous] Globally
- **Original Severity:** Medium
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs:20` has `[AllowAnonymous]`. Authentication is handled by `ScimAuthenticationMiddleware` at `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/ScimAuthenticationMiddleware.cs:32-96`. The middleware correctly validates Bearer tokens for non-discovery endpoints (lines 44-80) and short-circuits with 401 for invalid tokens. However, the `[AllowAnonymous]` bypasses the ASP.NET Core authorization pipeline entirely.
- **Adjusted Severity:** Medium (unchanged)
- **Notes:** Current implementation is secure. The middleware properly validates tokens and creates a `ClaimsPrincipal` with `"ScimBearer"` authentication type (line 92). The defense-in-depth concern is valid -- relying solely on middleware ordering for security of privileged endpoints is fragile.

---

### M-2: SCIM Endpoints Not Rate Limited
- **Original Severity:** Medium
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs` has no `[EnableRateLimiting]` attribute. The global rate limiter (1000 req/hour per IP, `src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs:129-137`) applies, but SCIM operations create/delete users in Keycloak and are heavyweight.
- **Adjusted Severity:** Medium (unchanged)
- **Notes:** The global rate limit of 1000/hour is generous for SCIM. A dedicated, lower limit would be appropriate.

---

### M-3: SCIM Error Responses Expose Exception Messages in Development
- **Original Severity:** Medium
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs:99-108` (and similar patterns at lines 127-135, 155-162, 179-185, etc.). The `_environment.IsDevelopment()` check gates exception message exposure. This is a common and generally acceptable pattern.
- **Adjusted Severity:** Low
- **Notes:** Downgraded to Low. The `IsDevelopment()` guard is the standard ASP.NET Core pattern. Production misconfigurations are possible but are an operational concern, not a code vulnerability.

---

### M-4: No Request Body Size Limits on Most Endpoints
- **Original Severity:** Medium
- **Verdict:** CONFIRMED
- **Evidence:** Searched across all controllers -- only `StorageController` has an explicit `[RequestSizeLimit]`. Kestrel's default limit is ~30MB. This applies to all `[FromBody]` endpoints.
- **Adjusted Severity:** Medium (unchanged)
- **Notes:** This is a valid hardening recommendation. The default 30MB is generous for JSON API payloads.

---

### M-5: Feature Flags Bulk Evaluate Endpoint Bypasses API Versioning
- **Original Severity:** Medium
- **Verdict:** PARTIALLY CONFIRMED
- **Evidence:** `src/Modules/Configuration/Wallow.Configuration.Api/Controllers/FeatureFlagsController.cs:234`. The endpoint uses absolute route `/api/feature-flags/evaluate` instead of the versioned pattern. However, this appears to be **intentional** -- the XML comment on line 233 states "Any authenticated user can call this endpoint." The controller has `[Authorize]` at class level (line 30). The endpoint does NOT have `[HasPermission]`, which is an intentional design decision for client-side feature flag evaluation.
- **Adjusted Severity:** Low
- **Notes:** The versioning bypass is confirmed. The lack of `[HasPermission]` is intentional per the code comment. Feature flag keys and evaluated boolean/variant values are low-sensitivity data designed for client consumption. This is a design/style issue, not a security vulnerability.

---

### M-6: Hangfire Dashboard Accessible Without Rate Limiting
- **Original Severity:** Medium
- **Verdict:** CONFIRMED
- **Evidence:** `src/Wallow.Api/Extensions/HangfireExtensions.cs:33-43` and `src/Wallow.Api/Middleware/HangfireDashboardAuthFilter.cs:14-24`. In development, the filter returns `true` unconditionally (line 17). In production, it checks `httpContext.User.IsInRole("Admin")` with capital "A" (line 23). The role mapping in `RolePermissionMapping.cs` uses lowercase `"admin"` (line 9). ASP.NET Core's `IsInRole()` depends on the `ClaimTypes.Role` claims and whether the comparison is case-sensitive -- this could cause a mismatch.
- **Adjusted Severity:** Medium (unchanged)
- **Notes:** The role case mismatch ("Admin" vs "admin") is a legitimate concern. Whether this actually causes a mismatch depends on how Keycloak role claims are mapped -- if they come through as lowercase "admin", the `IsInRole("Admin")` check might fail. This needs verification against the actual Keycloak claim format. In development, anyone with network access can view and manipulate background jobs.

---

### L-1: AuthController Error Responses May Enable User Enumeration
- **Original Severity:** Low
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthController.cs:67-75`. The error response forwards `result.ErrorDescription ?? result.Error ?? "Invalid credentials"`. Keycloak can return different error descriptions for different failure modes. Rate limiting (`[EnableRateLimiting("auth")]`, line 20) provides mitigation.
- **Adjusted Severity:** Low (unchanged)
- **Notes:** Standard Keycloak error forwarding. The rate limit of 5/5min mitigates bulk enumeration but doesn't eliminate it.

---

### L-2: Root Info Endpoint Exposes Version Information
- **Original Severity:** Low
- **Verdict:** CONFIRMED
- **Evidence:** `src/Wallow.Api/Program.cs:338-343`. Returns hardcoded `Version = "1.0.0"` and `Name = "Wallow API"`.
- **Adjusted Severity:** Informational
- **Notes:** The version is hardcoded and does not reflect actual deployment version. Very minimal information disclosure.

---

### L-3: Server Header Not Explicitly Removed
- **Original Severity:** Low
- **Verdict:** CONFIRMED
- **Evidence:** `src/Wallow.Api/Middleware/SecurityHeadersMiddleware.cs:14-36`. The middleware sets security headers but does not call `headers.Remove("Server")`. No `AddServerHeader = false` found in Kestrel configuration.
- **Adjusted Severity:** Informational
- **Notes:** Minor information disclosure. Industry best practice to remove but very low impact.

---

## Auth & Access Control Findings (from auth-scout)

### AUTH-001: No JWT Authentication Scheme Registered in Production Code
- **Original Severity:** Critical
- **Verdict:** CONFIRMED
- **Evidence:** Searched the entire `src/` directory for `AddAuthentication`, `AddJwtBearer`, `AddKeycloakWebApiAuthentication` -- none found. The only calls are:
  - `AddKeycloakAdminHttpClient` in `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs:83` (this is the **admin SDK** for server-to-server calls, NOT incoming JWT validation)
  - `IdentityModuleExtensions.cs` calls `AddIdentityInfrastructure` which calls `AddIdentityAuthorization` (registers policy provider and handler) and `AddKeycloakAdmin` (admin SDK client) but never registers an authentication scheme
  - `Program.cs:352` calls `app.UseAuthentication()` but without a registered scheme, this is effectively a no-op
  - The `PermissionAuthorizationPolicyProvider.GetFallbackPolicyAsync()` (line 33-34) returns `RequireAuthenticatedUser()`, which provides a safety net but without actual JWT validation

  The Identity module's CLAUDE.md explicitly states "Registers Keycloak OIDC JWT Bearer authentication" in its Api layer, but no such code exists. The `IdentityApiExtensions.cs` file does not exist (confirmed by file read error).

- **Adjusted Severity:** Critical (unchanged)
- **Notes:** This is the most significant finding in the entire audit. Without `AddAuthentication().AddJwtBearer()`, ASP.NET Core's authentication middleware cannot validate JWT tokens -- no signature verification, no expiry checks, no issuer/audience validation. The fallback authorization policy provides partial protection by requiring `IsAuthenticated == true`, but an unauthenticated request would simply be denied rather than having its token validated. The practical question is: how does `IsAuthenticated` become true? The `ApiKeyAuthenticationMiddleware` creates a `ClaimsPrincipal` with `"ApiKey"` authentication type, and `TenantResolutionMiddleware` checks `IsAuthenticated`. For JWT-based requests, without a registered scheme, the authentication middleware cannot set `IsAuthenticated = true`, which means JWT requests should be rejected by the fallback policy. This means JWT auth is **broken entirely** (all JWT requests return 401), not that it's insecure (tokens accepted without validation). Either way, this must be fixed.

---

### AUTH-002: Admin Tenant Override via X-Tenant-Id Header Has No Audit Trail Persistence
- **Original Severity:** High
- **Verdict:** PARTIALLY CONFIRMED -- severity downgraded
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs:41-51`. The override check validates: (1) header present, (2) user has admin role (`HasRealmAdminRole` at lines 75-106 with case-insensitive check), (3) valid GUID. The override IS logged via structured logging (`LogAdminTenantOverride` at line 50), which includes the overriding tenant ID, original tenant ID, user ID, and request path. No validation that the target tenant exists.
- **Adjusted Severity:** Medium
- **Notes:** The logging is more comprehensive than the scout described -- it logs the override tenant, original tenant, user ID, and request path via structured logging (line 181). Whether this constitutes adequate audit depends on the log retention and monitoring strategy. The missing tenant existence validation is valid. The finding about "no persistent audit trail" is arguably false since structured logs ARE typically persisted (Serilog is configured with OpenTelemetry export). Downgraded to Medium since the core concern (admin access logging) is addressed, though the suggestions for existence validation and rate limiting remain valid.

---

### AUTH-003: Wolverine Message Handlers Have No Authorization Checks
- **Original Severity:** High
- **Verdict:** CONFIRMED -- but nuanced
- **Evidence:** Wolverine handlers are application-layer components invoked via `IMessageBus.InvokeAsync()` from controllers. Controllers enforce `[HasPermission]` before invoking handlers. The handlers themselves have no authorization checks. Wolverine auto-discovers handlers in all `Wallow.*` assemblies (`Program.cs:93-97`). Messages can also arrive via RabbitMQ when `ModuleMessaging:Transport` is `"RabbitMq"` (`Program.cs:155-176`).
- **Adjusted Severity:** Medium
- **Notes:** The scout is correct that handlers lack authorization, but the threat model needs nuance. For `IMessageBus.InvokeAsync()` from controllers, authorization is enforced at the controller level. The actual risk is: (1) RabbitMQ message injection if RabbitMQ is compromised, (2) internal code paths that bypass controllers. For in-memory transport (the default), message injection requires code-level access. For RabbitMQ transport, it requires RabbitMQ access. This is a defense-in-depth concern, not an immediate exploitable vulnerability. Downgraded to Medium.

---

### AUTH-004: SCIM Controller Uses [AllowAnonymous] with Middleware-Based Auth
- **Original Severity:** High
- **Verdict:** CONFIRMED (duplicate of M-1)
- **Evidence:** Same as M-1 above. `ScimController.cs:20` has `[AllowAnonymous]`. `ScimAuthenticationMiddleware` handles authentication. The middleware creates an identity with `"ScimBearer"` authentication type.
- **Adjusted Severity:** Medium
- **Notes:** Duplicate of M-1 from the API endpoint report. The additional point about no fine-grained permission control on SCIM operations is valid -- once a SCIM token validates, full CRUD is available. However, SCIM tokens are typically provisioned per-IdP integration and full CRUD is the expected behavior per SCIM protocol. The lack of read-only SCIM tokens is a valid enhancement suggestion but not a vulnerability per se.

---

### AUTH-005: Organization Access Control Missing on Multiple User Operations
- **Original Severity:** High
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/UsersController.cs`:
  - `GetUserById` (lines 58-74): **Has** tenant ownership check via `_keycloakOrg.GetUserOrganizationsAsync()` + `belongsToTenant` validation (lines 67-72)
  - `DeactivateUser` (lines 115-121): **No** tenant ownership check. Directly calls `_keycloakAdmin.DeactivateUserAsync(id, ct)` with the raw GUID
  - `ActivateUser` (lines 127-132): **No** tenant ownership check. Same pattern.
  - `AssignRole` (lines 138-146): **No** tenant ownership check. Directly calls `_keycloakAdmin.AssignRoleAsync(userId, request.RoleName, ct)`
  - `RemoveRole` (lines 151-157): **No** tenant ownership check. Same pattern.
  - `CreateUser` (lines 97-110): **No** tenant ownership check, but this creates a new user so it's less of an IDOR concern.
  - `GetUsers` (lines 41-51): **No** tenant ownership check on the list endpoint.
- **Adjusted Severity:** High (unchanged)
- **Notes:** This is a genuine cross-tenant IDOR vulnerability. A user with `UsersUpdate` permission in Tenant A can deactivate users in Tenant B by GUID. Similarly, `RolesUpdate` permission holders can escalate privileges across tenants. The mitigating factor is that GUIDs are hard to guess, but they may be discoverable through other means. The `GetUsers` list endpoint also lacks tenant scoping -- it appears to return all Keycloak users regardless of tenant, though this depends on how `_keycloakAdmin.GetUsersAsync` is implemented (it may be Keycloak-realm-scoped rather than org-scoped).

---

### AUTH-006: Organization Endpoints Allow Cross-Tenant Access
- **Original Severity:** High
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/OrganizationsController.cs`:
  - `GetById` (lines 59-65): No tenant validation. Returns any org by GUID.
  - `GetMembers` (lines 70-75): No tenant validation. Lists members of any org.
  - `AddMember` (lines 80-86): No tenant validation. Adds users to any org.
  - `RemoveMember` (lines 91-97): No tenant validation. Removes users from any org.
  - `GetAll` (lines 46-54): No tenant validation. Lists all organizations.
  - `Create` (lines 33-41): Creates orgs without tenant scoping.

  All require permissions (`OrganizationsRead`, `OrganizationsManageMembers`, `OrganizationsCreate`) but these are not scoped to a specific organization/tenant.
- **Adjusted Severity:** High (unchanged)
- **Notes:** This is a genuine cross-tenant vulnerability. A user with `OrganizationsRead` in Tenant A can list members of Tenant B's organization. A user with `OrganizationsManageMembers` can add/remove members from any organization. The practical exploitability depends on whether organization GUIDs are discoverable and whether Keycloak's admin API provides any additional scoping.

---

### AUTH-007: Admin Role Gets All Permissions Including Future Ones
- **Original Severity:** Medium
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/RolePermissionMapping.cs:9`. `["admin"] = PermissionType.All.ToArray()`. `PermissionType.All` likely uses reflection to enumerate all permission constants.
- **Adjusted Severity:** Low
- **Notes:** This is a common design pattern for admin roles. The auto-grant behavior is intentional and well-understood. It does mean new permissions are automatically granted to admins, but this is generally the desired behavior for an "admin" role. Downgraded to Low as this is a design choice rather than a vulnerability.

---

### AUTH-008: Account Enumeration via Token Endpoint Error Messages
- **Original Severity:** Medium
- **Verdict:** CONFIRMED (duplicate of L-1)
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthController.cs:67-75`. Same finding as L-1 from the API endpoint report.
- **Adjusted Severity:** Low
- **Notes:** Duplicate. See L-1 above.

---

### AUTH-009: Rate Limiting on Auth Endpoints May Be Insufficient
- **Original Severity:** Medium
- **Verdict:** CONFIRMED
- **Evidence:** `src/Wallow.Api/Extensions/RateLimitDefaults.cs:5-6`: `AuthPermitLimit = 5`, `AuthWindowMinutes = 5`. This is 5 attempts per 5-minute window per IP, or 60/hour. The rate limiting is applied to the `AuthController` via `[EnableRateLimiting("auth")]` (line 20). No per-account rate limiting exists -- only per-IP.
- **Adjusted Severity:** Medium (unchanged)
- **Notes:** The per-IP rate limit is reasonable for basic protection. The suggestion for per-account limiting and Keycloak brute force detection delegation is valid but may be out of scope for the application layer.

---

### AUTH-010: No Token Revocation Endpoint
- **Original Severity:** Medium
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthController.cs` has only `GetToken` (line 49) and `RefreshToken` (line 104). No logout or revocation endpoint exists.
- **Adjusted Severity:** Medium (unchanged)
- **Notes:** Valid finding. A revocation endpoint should call Keycloak's `/protocol/openid-connect/revoke`.

---

### AUTH-011: Hangfire Dashboard Auth Allows Any "Admin" Role in Production
- **Original Severity:** Medium
- **Verdict:** CONFIRMED (partially duplicate of M-6)
- **Evidence:** `src/Wallow.Api/Middleware/HangfireDashboardAuthFilter.cs:22-23`. Uses `IsInRole("Admin")` with capital A. `RolePermissionMapping.cs:7` uses `StringComparer.OrdinalIgnoreCase` for the dictionary, but the dictionary keys use lowercase `"admin"`. The `IsInRole` check uses the role value from claims, not the dictionary.
- **Adjusted Severity:** Medium (unchanged)
- **Notes:** The case mismatch concern is valid. `ClaimsPrincipal.IsInRole()` performs a case-sensitive comparison by default unless the `ClaimsIdentity` was constructed with a custom role claim type that uses case-insensitive comparison. Since Keycloak typically maps roles as lowercase, `IsInRole("Admin")` may never match, effectively locking all admins out of the Hangfire dashboard in production.

---

### AUTH-012: API Key Auth Creates Identity Without Organization Claim
- **Original Severity:** Medium
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/ApiKeyAuthenticationMiddleware.cs:71-88`. The claims list includes `NameIdentifier`, `sub`, `api_key_id`, and `auth_method` but no `organization` claim. The tenant context IS set correctly (lines 92-94) via `tenantContext.TenantId`, but the `ClaimsPrincipal` lacks the organization claim.
- **Adjusted Severity:** Low
- **Notes:** The tenant context is properly set, so multi-tenancy works correctly for API key requests. The missing `organization` claim is only a concern if code paths check `User.FindFirst("organization")` instead of using `ITenantContext`. This is more of a consistency/robustness issue than a security vulnerability. Downgraded to Low.

---

### AUTH-013: SignalR Hub Has Proper Auth but Limited Group Validation
- **Original Severity:** Low
- **Verdict:** CONFIRMED
- **Evidence:** `src/Wallow.Api/Hubs/RealtimeHub.cs:87-108`. `ValidateTenantGroup` (lines 87-108) only validates groups with the `"tenant:"` prefix. Non-tenant-prefixed groups are allowed without validation (line 89-92 early return). `UpdatePageContext` (lines 67-85) allows joining `page:{pageContext}` groups with arbitrary `pageContext` values.
- **Adjusted Severity:** Low (unchanged)
- **Notes:** The tenant group validation is solid. The page context group joining is low risk since page context groups are used for presence tracking, not sensitive data delivery.

---

### AUTH-014: Service Account Endpoints Reuse ApiKeysRead/Create/Update/Delete Permissions
- **Original Severity:** Low
- **Verdict:** CONFIRMED
- **Evidence:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/ServiceAccountsController.cs`. Uses `PermissionType.ApiKeysRead` (line 37), `ApiKeysCreate` (line 50), `ApiKeysUpdate` (lines 93, 113), `ApiKeysDelete` (line 133) rather than dedicated `ServiceAccountsXxx` permissions.
- **Adjusted Severity:** Low (unchanged)
- **Notes:** Design choice. Service accounts and API keys are related concepts in this codebase. Separate permissions would provide finer granularity.

---

### AUTH-015: Changelog Controller is Publicly Accessible
- **Original Severity:** Low (Intentional)
- **Verdict:** CONFIRMED (Intentional)
- **Evidence:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/ChangelogController.cs:18`. `[AllowAnonymous]` on the entire controller. This is intentional for a public-facing changelog.
- **Adjusted Severity:** Informational
- **Notes:** Explicitly intentional. The changelog contains version numbers, titles, and descriptions of releases. This is public product information.

---

## Summary

| ID | Original Severity | Adjusted Severity | Verdict |
|----|-------------------|-------------------|---------|
| C-1 | Critical | **Critical** | CONFIRMED |
| C-2 | Critical | **Low** | PARTIALLY CONFIRMED (read-only metadata) |
| H-1 | High | **N/A** | FALSE POSITIVE (HasPermission extends Authorize) |
| H-2 | High | **N/A** | FALSE POSITIVE (same as H-1) |
| H-3 | High | **Low** | PARTIALLY CONFIRMED (user-scoped action) |
| H-4 | High | **Medium** | CONFIRMED |
| M-1 | Medium | **Medium** | CONFIRMED |
| M-2 | Medium | **Medium** | CONFIRMED |
| M-3 | Medium | **Low** | CONFIRMED (standard dev pattern) |
| M-4 | Medium | **Medium** | CONFIRMED |
| M-5 | Medium | **Low** | PARTIALLY CONFIRMED (intentional design) |
| M-6 | Medium | **Medium** | CONFIRMED |
| L-1 | Low | **Low** | CONFIRMED |
| L-2 | Low | **Informational** | CONFIRMED |
| L-3 | Low | **Informational** | CONFIRMED |
| AUTH-001 | Critical | **Critical** | CONFIRMED |
| AUTH-002 | High | **Medium** | PARTIALLY CONFIRMED (logging exists) |
| AUTH-003 | High | **Medium** | CONFIRMED (defense-in-depth) |
| AUTH-004 | High | **Medium** | CONFIRMED (duplicate of M-1) |
| AUTH-005 | High | **High** | CONFIRMED |
| AUTH-006 | High | **High** | CONFIRMED |
| AUTH-007 | Medium | **Low** | CONFIRMED (design choice) |
| AUTH-008 | Medium | **Low** | CONFIRMED (duplicate of L-1) |
| AUTH-009 | Medium | **Medium** | CONFIRMED |
| AUTH-010 | Medium | **Medium** | CONFIRMED |
| AUTH-011 | Medium | **Medium** | CONFIRMED |
| AUTH-012 | Medium | **Low** | CONFIRMED (consistency issue) |
| AUTH-013 | Low | **Low** | CONFIRMED |
| AUTH-014 | Low | **Low** | CONFIRMED |
| AUTH-015 | Low | **Informational** | CONFIRMED (intentional) |

### Key Takeaways

1. **Two false positives eliminated**: H-1 and H-2 were false positives because the scout did not read the `HasPermissionAttribute` implementation, which extends `AuthorizeAttribute` directly.

2. **True Critical findings (2)**: AUTH-001 (no JWT scheme registered) and C-1 (quota admin endpoints missing permissions) are genuine Critical issues that need immediate remediation.

3. **True High findings (2)**: AUTH-005 (user endpoint IDOR) and AUTH-006 (organization endpoint cross-tenant access) are genuine cross-tenant isolation failures.

4. **Several findings were duplicate** across the two reports: AUTH-004/M-1, AUTH-008/L-1, AUTH-011/M-6.

5. **Several severity downgrades**: C-2 (Critical to Low), H-3 (High to Low), AUTH-002 (High to Medium), AUTH-003 (High to Medium), AUTH-007 (Medium to Low).
