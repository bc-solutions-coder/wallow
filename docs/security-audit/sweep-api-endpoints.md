# Security Audit: API & Endpoint Security

**Auditor:** api-scout
**Date:** 2026-03-03
**Scope:** All API endpoints, middleware pipeline, CORS, rate limiting, security headers, authentication/authorization configuration

---

## Executive Summary

Audited 25 controllers, 1 SignalR hub, the middleware pipeline (Program.cs), CORS/rate-limiting configuration, security headers middleware, global exception handler, and Hangfire dashboard. Found **14 findings** across Critical, High, Medium, and Low severity.

The codebase has a generally strong security posture: most endpoints enforce `[Authorize]` with granular `[HasPermission]` policies, security headers are comprehensive, CORS is well-configured with explicit origins, and a global exception handler prevents stack trace leakage in production. The findings below represent areas that could be hardened further.

---

## Findings

### CRITICAL

#### C-1: Quota Admin Endpoints Missing Authorization Policy

**File:** `src/Modules/Billing/Wallow.Billing.Api/Controllers/QuotasController.cs`, lines 48-89
**Code:**
```csharp
[HttpPut("admin/{tenantId:guid}")]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> SetOverride(...)

[HttpDelete("admin/{tenantId:guid}/{meterCode}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
public async Task<IActionResult> RemoveOverride(...)
```

**Vulnerability:** The `SetOverride` and `RemoveOverride` endpoints on `QuotasController` are admin-only operations (they accept an arbitrary `tenantId` and modify quotas for other tenants), but they have **no `[HasPermission]` attribute**. The controller has `[Authorize]` at class level, so any authenticated user can call these endpoints to set or remove quota overrides for **any tenant** in the system.

**Impact:** Any authenticated user can remove quota limits for their own tenant (or any other) or set artificially high limits, bypassing billing controls entirely. This is a privilege escalation vulnerability.

**Recommendation:** Add `[HasPermission(PermissionType.BillingAdmin)]` or equivalent admin-only permission to both endpoints.

---

#### C-2: MetersController Missing Permission Check

**File:** `src/Modules/Billing/Wallow.Billing.Api/Controllers/MetersController.cs`, lines 31-39
**Code:**
```csharp
[HttpGet]
[ProducesResponseType(typeof(IReadOnlyList<MeterDefinitionDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
```

**Vulnerability:** The `GetAll` endpoint on `MetersController` has `[Authorize]` (class-level) but **no `[HasPermission]` attribute**. Any authenticated user can list all meter definitions. Meter definitions may contain internal billing configuration that should be restricted to users with billing read permissions.

**Impact:** Information disclosure of internal metering/billing configuration to any authenticated user.

**Recommendation:** Add `[HasPermission(PermissionType.BillingRead)]` or a more specific meter-read permission.

---

### HIGH

#### H-1: AdminAnnouncementsController Missing [Authorize] Attribute

**File:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/AdminAnnouncementsController.cs`, lines 20-26
**Code:**
```csharp
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/announcements")]
[HasPermission(PermissionType.AnnouncementManage)]  // <-- this alone does NOT enforce authentication
[Tags("Admin - Announcements")]
```

**Vulnerability:** The controller uses `[HasPermission(PermissionType.AnnouncementManage)]` at class level but is **missing `[Authorize]`**. The `HasPermission` attribute checks claims on the current user, but without `[Authorize]`, the ASP.NET authorization middleware may not challenge unauthenticated requests in all scenarios (depending on how the custom `HasPermissionAttribute` is implemented). If `HasPermission` is an `IAuthorizationFilter` that only checks claims, an unauthenticated user with no claims would simply fail the check and get a 403, which is acceptable. However, best practice is always to pair it with `[Authorize]` for defense-in-depth.

**Impact:** Potential bypass if the `HasPermission` implementation doesn't enforce authentication. Even if it does, the inconsistency increases the risk of future regressions.

**Recommendation:** Add `[Authorize]` to the controller, consistent with all other controllers.

---

#### H-2: AdminChangelogController Missing [Authorize] Attribute

**File:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/AdminChangelogController.cs`, lines 18-22
**Code:**
```csharp
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/changelog")]
[HasPermission(PermissionType.ChangelogManage)]  // <-- missing [Authorize]
```

**Vulnerability:** Same issue as H-1. Missing `[Authorize]` on an admin-only controller.

**Recommendation:** Add `[Authorize]` to the controller.

---

#### H-3: Announcements Dismiss Endpoint Missing Permission Check

**File:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/AnnouncementsController.cs`, lines 71-89
**Code:**
```csharp
[HttpPost("{id:guid}/dismiss")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
// No [HasPermission] attribute
public async Task<IActionResult> DismissAnnouncement(Guid id, CancellationToken ct)
```

**Vulnerability:** The `DismissAnnouncement` endpoint is authenticated (via class-level `[Authorize]`) but has **no `[HasPermission]` check**. While other endpoints on the same controller require `PermissionType.AnnouncementRead`, the dismiss action can be called by any authenticated user regardless of their permissions.

**Impact:** Low-privilege users who should not have access to announcements can still dismiss them. This is an inconsistency rather than a critical vulnerability since dismissal is a user-scoped action.

**Recommendation:** Add `[HasPermission(PermissionType.AnnouncementRead)]` for consistency.

---

#### H-4: Health Check Endpoints Expose Infrastructure Details in Non-Production

**File:** `src/Wallow.Api/Program.cs`, lines 421-452
**Code:**
```csharp
if (env.IsProduction())
{
    // Returns only { status: "..." }
}
// Non-production returns:
object response = new
{
    status = report.Status.ToString(),
    duration = report.TotalDuration.TotalMilliseconds,
    checks = report.Entries.Select(e => new
    {
        name = e.Key,
        status = e.Value.Status.ToString(),
        duration = e.Value.Duration.TotalMilliseconds,
        description = e.Value.Description,
        error = e.Value.Exception?.Message  // <-- exception messages exposed
    })
};
```

**Vulnerability:** In staging/non-production environments, health check responses include exception messages from infrastructure components (PostgreSQL, RabbitMQ, Redis, Hangfire). The `AllowAnonymous` attribute means anyone can probe these endpoints to discover infrastructure details and error messages.

**Impact:** Information disclosure of infrastructure component names, connection states, and error messages. Could reveal database hostnames, service versions, or configuration issues to unauthenticated users in staging environments.

**Recommendation:** Either restrict the detailed health check to authenticated users in non-development environments, or redact exception messages in staging. The `/health/live` endpoint (which returns no details) is already correctly minimal.

---

### MEDIUM

#### M-1: SCIM Controller Uses [AllowAnonymous] Globally

**File:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs`, line 20
**Code:**
```csharp
[AllowAnonymous] // SCIM uses Bearer token authentication via middleware (not OAuth)
```

**Vulnerability:** The entire SCIM controller is marked `[AllowAnonymous]`. Authentication is handled by `ScimAuthenticationMiddleware`, which correctly validates Bearer tokens for non-discovery endpoints. However, this pattern bypasses the ASP.NET Core authorization pipeline entirely, meaning any authorization policies or `[Authorize]` checks that might be added in the future would be silently ignored. If the middleware has a bug or is accidentally reordered in the pipeline, all SCIM CRUD endpoints (user create/update/delete, group create/update/delete) become fully unauthenticated.

**Impact:** The current implementation is secure because the middleware correctly short-circuits unauthenticated requests. However, this is a defense-in-depth concern -- the architecture relies on a single middleware ordering assumption for security of highly privileged endpoints (user provisioning/deprovisioning).

**Recommendation:** Consider using a dedicated authentication scheme (e.g., `[Authorize(AuthenticationSchemes = "ScimBearer")]`) registered with ASP.NET Core's auth system instead of `[AllowAnonymous]` + custom middleware. This integrates with the standard auth pipeline.

---

#### M-2: SCIM Endpoints Not Rate Limited

**File:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs`

**Vulnerability:** SCIM endpoints handle user provisioning (create/update/delete) and group management but have **no rate limiting**. The global rate limiter applies (1000 requests/hour per IP), but SCIM operations are heavyweight (each creates/modifies users in Keycloak) and are typically called by enterprise IdPs doing bulk sync.

**Impact:** A compromised SCIM token could be used to rapidly create/delete users, potentially causing a DoS against Keycloak or flooding the system with rogue accounts before the global rate limit kicks in.

**Recommendation:** Add a dedicated rate limiting policy for SCIM endpoints with a lower threshold than the global limit.

---

#### M-3: SCIM Error Responses Expose Exception Messages in Development

**File:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs`, lines 99-108
**Code:**
```csharp
catch (Exception ex)
{
    LogScimOperationError(ex, "CreateUser");
    return BadRequest(new ScimError
    {
        Status = 400,
        ScimType = "invalidValue",
        Detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred"
    });
}
```

**Vulnerability:** In development mode, full exception messages are returned in SCIM error responses. This is guarded by `IsDevelopment()` check so it does not affect production, but if someone accidentally deploys with the Development environment name (a common misconfiguration), internal exception details would leak.

**Impact:** Low in practice (development only). Exception messages could reveal internal implementation details, database schema names, or Keycloak configuration.

**Recommendation:** This is acceptable but worth noting. Consider using a custom environment check or a feature flag for verbose error details.

---

#### M-4: No Request Body Size Limits on Most Endpoints

**File:** Multiple controllers

**Vulnerability:** Only `StorageController.Upload` explicitly sets `[RequestSizeLimit(100 * 1024 * 1024)]`. All other endpoints accepting `[FromBody]` JSON payloads rely on the ASP.NET Core default (which is ~30MB for Kestrel). This means endpoints like:
- `CreateUser` (Identity)
- `ConfigureSaml` / `ConfigureOidc` (SSO -- accepts certificate data)
- `CreateAnnouncement` (Communications -- accepts HTML content)
- `SendMessage` (Conversations -- accepts message body)

...could receive payloads up to 30MB.

**Impact:** An attacker could send very large JSON payloads to consume server memory and CPU during deserialization, leading to resource exhaustion.

**Recommendation:** Set a global `[RequestSizeLimit]` in Kestrel configuration (e.g., 1MB for API endpoints) and only override it for endpoints that genuinely need larger payloads (file upload).

---

#### M-5: Feature Flags Bulk Evaluate Endpoint Bypasses API Versioning

**File:** `src/Modules/Configuration/Wallow.Configuration.Api/Controllers/FeatureFlagsController.cs`, line 234
**Code:**
```csharp
[HttpGet("/api/feature-flags/evaluate")]
[ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
public async Task<IActionResult> Evaluate(CancellationToken cancellationToken)
```

**Vulnerability:** This endpoint uses an absolute route `/api/feature-flags/evaluate` instead of the versioned route pattern `api/v{version:apiVersion}/...`. This:
1. Bypasses API version routing, making it harder to deprecate or version
2. Has no `[HasPermission]` attribute -- any authenticated user can enumerate all feature flag keys and their evaluated values for the current tenant

**Impact:** Feature flag names and states may reveal upcoming features or internal system capabilities. The endpoint intentionally allows any authenticated user (per the XML comment), but this should be an explicit design decision documented in a permission.

**Recommendation:** Move to versioned routing for consistency. If the intent is truly "any authenticated user", document this decision explicitly and consider whether feature flag keys could be sensitive.

---

#### M-6: Hangfire Dashboard Accessible Without Rate Limiting

**File:** `src/Wallow.Api/Extensions/HangfireExtensions.cs`, lines 33-43, and `src/Wallow.Api/Middleware/HangfireDashboardAuthFilter.cs`

**Vulnerability:** In development, the Hangfire dashboard at `/hangfire` is accessible to anyone (the auth filter returns `true` in development). In production, it requires `Admin` role. However:
1. The `/hangfire` path is not rate-limited and not behind the global rate limiter (it's a non-API path)
2. The Hangfire dashboard allows viewing job details, which may contain sensitive data (connection strings in job arguments, email addresses in job parameters)
3. The auth check uses `httpContext.User.IsInRole("Admin")` with a string literal -- if the Keycloak role name changes, this silently fails open (returns false, denying access, which is safe but could lock out admins)

**Impact:** In development environments, anyone with network access can view and manipulate background jobs. In production, admin users can see potentially sensitive job parameters.

**Recommendation:** Add IP allowlisting for Hangfire dashboard in production. Consider redacting sensitive job parameters.

---

### LOW

#### L-1: AuthController Error Responses May Enable User Enumeration

**File:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthController.cs`, lines 66-76
**Code:**
```csharp
if (!result.Success)
{
    return Unauthorized(new ProblemDetails
    {
        Title = "Authentication failed",
        Detail = result.ErrorDescription ?? result.Error ?? "Invalid credentials",
        Extensions = { ["error"] = result.Error }
    });
}
```

**Vulnerability:** The error response forwards Keycloak's `ErrorDescription` and `Error` fields. Depending on Keycloak's configuration, these may differ between "invalid user" and "invalid password" scenarios, enabling user enumeration. The `[EnableRateLimiting("auth")]` policy (5 attempts per 5 minutes) mitigates but does not eliminate this risk.

**Impact:** An attacker could determine whether a specific email address has an account by comparing error messages. Rate limiting makes this slow but not impossible over time.

**Recommendation:** Return a generic "Invalid email or password" message regardless of the failure reason. Ensure Keycloak is configured to not differentiate between invalid user and invalid password in its error responses.

---

#### L-2: Root Info Endpoint Exposes Version Information

**File:** `src/Wallow.Api/Program.cs`, lines 338-343
**Code:**
```csharp
app.MapGet("/", () => Results.Ok(new
{
    Name = "Wallow API",
    Version = "1.0.0",
    Health = "/health"
})).ExcludeFromDescription().AllowAnonymous();
```

**Vulnerability:** The root endpoint returns a hardcoded version string. While this is a minor information disclosure, it confirms the API name and could help attackers identify the software and known vulnerabilities.

**Impact:** Very low. The version is hardcoded ("1.0.0") and does not reflect the actual deployment version.

**Recommendation:** Consider removing the version field from the root endpoint in production, or making it configurable.

---

#### L-3: Server Header Not Explicitly Removed

**File:** `src/Wallow.Api/Middleware/SecurityHeadersMiddleware.cs`

**Vulnerability:** The security headers middleware adds excellent protective headers (X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, CSP, HSTS) but does **not remove the `Server` response header**. By default, Kestrel includes a `Server: Kestrel` header, which discloses the web server technology.

**Impact:** Minor information disclosure. Attackers can identify the server technology, which marginally helps in targeting known Kestrel vulnerabilities.

**Recommendation:** Add `headers.Remove("Server")` to the SecurityHeadersMiddleware, or configure Kestrel with `options.AddServerHeader = false` in Program.cs.

---

## Summary Table

| ID | Severity | Category | Component | Description |
|----|----------|----------|-----------|-------------|
| C-1 | Critical | Authorization | QuotasController | Admin endpoints missing `[HasPermission]` -- any user can modify quotas |
| C-2 | Critical | Authorization | MetersController | Missing permission check on meter definitions |
| H-1 | High | Authorization | AdminAnnouncementsController | Missing `[Authorize]` attribute |
| H-2 | High | Authorization | AdminChangelogController | Missing `[Authorize]` attribute |
| H-3 | High | Authorization | AnnouncementsController | Dismiss endpoint missing permission check |
| H-4 | High | Info Disclosure | Health Checks | Infrastructure details exposed in non-production |
| M-1 | Medium | Auth Design | ScimController | `[AllowAnonymous]` on all endpoints, relies on middleware |
| M-2 | Medium | Rate Limiting | ScimController | No rate limiting on user provisioning endpoints |
| M-3 | Medium | Info Disclosure | ScimController | Exception messages in development error responses |
| M-4 | Medium | Input Validation | Multiple | No request body size limits on most endpoints |
| M-5 | Medium | Auth/Design | FeatureFlagsController | Bulk evaluate bypasses versioning and has no permission |
| M-6 | Medium | Access Control | Hangfire Dashboard | Unrestricted in development, potential data exposure |
| L-1 | Low | User Enumeration | AuthController | Error messages may enable user enumeration |
| L-2 | Low | Info Disclosure | Program.cs root endpoint | Version info exposed |
| L-3 | Low | Info Disclosure | SecurityHeadersMiddleware | Server header not removed |

---

## Positive Findings (What's Done Well)

1. **Comprehensive permission model**: Nearly all endpoints use `[HasPermission(PermissionType.X)]` for granular access control
2. **Rate limiting on auth endpoints**: Auth endpoints have `[EnableRateLimiting("auth")]` with 5 req/5 min
3. **Rate limiting on uploads**: Storage upload has dedicated rate limiting
4. **Global rate limiter**: 1000 req/hour per IP as a safety net
5. **Security headers**: CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, HSTS all properly configured
6. **CORS properly configured**: Explicit origins required in production (throws if not configured), development uses named policy with specific localhost origins
7. **OpenAPI/Scalar only in development**: `app.MapOpenApi()` and `app.MapScalarApiReference()` are behind `IsDevelopment()` check
8. **AsyncAPI only in development**: Same development-only guard
9. **Global exception handler**: Prevents stack trace leakage in production, properly uses RFC 7807 Problem Details
10. **SignalR hub properly secured**: `[Authorize]` attribute, tenant group validation prevents cross-tenant access
11. **HTML sanitization**: Conversation messages and announcements use `IHtmlSanitizationService` to prevent XSS
12. **HTTPS enforcement**: `UseHttpsRedirection()` enabled, HSTS in production
13. **File upload validation**: Size limit (100MB), empty file check, rate limiting
14. **Middleware pipeline order correct**: Authentication -> Tenant Resolution -> SCIM Auth -> Permission Expansion -> Authorization
15. **API key authentication**: Proper validation with scope enforcement, timing-safe comparison (assumed in service layer)
16. **Tenant isolation in SignalR**: `ValidateTenantGroup` prevents cross-tenant group joins
