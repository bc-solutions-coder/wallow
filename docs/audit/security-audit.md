# Security Audit Report -- Foundry

**Date:** 2026-03-02
**Auditor:** security-auditor (automated)
**Scope:** All source files under `src/`
**Branch:** `expansion`

---

## Executive Summary

The Foundry codebase demonstrates **strong security fundamentals** with a well-layered defense-in-depth approach. Multi-tenancy isolation is enforced at both the EF Core query-filter level and in raw Dapper queries. Authentication and authorization are consistently applied across controllers. The codebase includes HTML sanitization, security headers, rate limiting, and input validation via FluentValidation.

Several findings require attention, primarily around information disclosure in SCIM error responses, missing granular permissions on some controllers, and message body sanitization.

**Finding Summary:**
| Severity | Count |
|----------|-------|
| CRITICAL | 0 |
| HIGH | 2 |
| MEDIUM | 5 |
| LOW | 4 |

---

## Findings

### HIGH-1: SCIM Controller Exposes Internal Exception Messages

**Severity:** HIGH
**File:** `src/Modules/Identity/Foundry.Identity.Api/Controllers/ScimController.cs`
**Lines:** 92, 113, 135, 158, 178, 207, 221, 247

**Issue:** Multiple SCIM endpoints catch `Exception` and return `ex.Message` directly in the response body. Internal exception messages may contain stack traces, SQL details, entity framework internals, or other implementation details that help an attacker understand the system.

**Code example (line 92):**
```csharp
catch (Exception ex)
{
    return BadRequest(new ScimError
    {
        Status = 400,
        ScimType = "invalidValue",
        Detail = ex.Message  // Leaks internal details
    });
}
```

This pattern repeats in `CreateUser`, `UpdateUser`, `PatchUser`, `DeleteUser`, `CreateGroup`, `UpdateGroup`, `DeleteGroup`.

**Remediation:** Return generic error messages in production. Log the full exception server-side. Use the same environment-aware pattern as `GlobalExceptionHandler`:
```csharp
Detail = _environment.IsDevelopment() ? ex.Message : "An error occurred processing the request."
```

---

### HIGH-2: Message Body Not HTML-Sanitized (Stored XSS Risk)

**Severity:** HIGH
**Files:**
- `src/Modules/Communications/Foundry.Communications.Api/Controllers/ConversationsController.cs:117`
- `src/Modules/Communications/Foundry.Communications.Application/Messaging/Commands/SendMessage/SendMessageHandler.cs:23`

**Issue:** The `SendMessage` endpoint passes `request.Body` directly to the command handler without HTML sanitization. While the `AdminChangelogController` and `AdminAnnouncementsController` both use `IHtmlSanitizationService` to sanitize content, the messaging system does not. If the message body is rendered as HTML in a frontend, this creates a stored XSS vector.

The validator (`SendMessageValidator`) only checks length (max 4000) and non-empty -- it does not sanitize content.

**Remediation:** Either:
1. Sanitize `request.Body` in the controller before passing to the command (consistent with AdminChangelog/AdminAnnouncements pattern), or
2. Ensure the frontend always renders message bodies as plain text (not innerHTML), and document this contract clearly

---

### MEDIUM-1: Hangfire Dashboard Accessible in Non-Development Without Explicit Auth Gate

**Severity:** MEDIUM
**Files:**
- `src/Foundry.Api/Middleware/HangfireDashboardAuthFilter.cs`
- `src/Foundry.Api/Extensions/HangfireExtensions.cs:36`

**Issue:** The Hangfire dashboard at `/hangfire` is registered in all environments. In production, it requires `IsAuthenticated && IsInRole("Admin")`, which is reasonable. However, the Hangfire dashboard is placed in the middleware pipeline after `UseAuthorization()` but the dashboard itself uses its own `IDashboardAuthorizationFilter` -- it does not go through the standard ASP.NET Core authorization pipeline. This means:
- CORS policies may not apply to it
- Rate limiting may not apply
- The dashboard endpoint is always discoverable (returns 401/403 rather than 404)

**Remediation:** Consider restricting the Hangfire dashboard to development-only, or placing it behind a dedicated route prefix with an explicit `RequireAuthorization` policy. Alternatively, use `app.Map("/hangfire", ...)` with `RequireAuthorization("Admin")` for defense in depth.

---

### MEDIUM-2: Missing Granular Permissions on Several Controllers

**Severity:** MEDIUM
**Files:**
- `src/Modules/Billing/Foundry.Billing.Api/Controllers/UsageController.cs` -- `[Authorize]` only, no `[HasPermission]`
- `src/Modules/Billing/Foundry.Billing.Api/Controllers/MetersController.cs` -- `[Authorize]` only, no `[HasPermission]`
- `src/Modules/Billing/Foundry.Billing.Api/Controllers/QuotasController.cs` -- `[Authorize]` only, no `[HasPermission]`
- `src/Modules/Communications/Foundry.Communications.Api/Controllers/NotificationsController.cs` -- `[Authorize]` only
- `src/Modules/Communications/Foundry.Communications.Api/Controllers/EmailPreferencesController.cs` -- `[Authorize]` only
- `src/Modules/Communications/Foundry.Communications.Api/Controllers/AnnouncementsController.cs` -- `[Authorize]` only
- `src/Modules/Communications/Foundry.Communications.Api/Controllers/ConversationsController.cs` -- `[Authorize]` only
- `src/Modules/Storage/Foundry.Storage.Api/Controllers/StorageController.cs` -- `[Authorize]` only
- `src/Modules/Identity/Foundry.Identity.Api/Controllers/ApiKeysController.cs` -- `[Authorize]` only
- `src/Modules/Identity/Foundry.Identity.Api/Controllers/ScopesController.cs` -- `[Authorize]` only

**Issue:** These controllers require authentication (`[Authorize]`) but do not enforce granular permission checks (`[HasPermission]`). This means any authenticated user can access these endpoints regardless of role. Compare with `FeatureFlagsController`, `UsersController`, `OrganizationsController`, etc., which properly use `[HasPermission(PermissionType.XXX)]`.

**Remediation:** Add `[HasPermission]` attributes to controllers/actions where not all authenticated users should have access. At minimum, write operations (POST/PUT/DELETE) on Billing, Storage, and API Keys should require specific permissions.

---

### MEDIUM-3: Conversations Endpoint Lacks Participant Authorization Check

**Severity:** MEDIUM
**File:** `src/Modules/Communications/Foundry.Communications.Api/Controllers/ConversationsController.cs:93-108`

**Issue:** The `GetMessages` endpoint retrieves messages for a conversation given a conversation ID and the current user's ID. However, the Dapper query in `MessagingQueryService.GetMessagesAsync` (`src/Modules/Communications/Foundry.Communications.Infrastructure/Services/MessagingQueryService.cs:47-93`) filters by `TenantId` and `ConversationId` but does **not** verify that the requesting user is actually a participant in that conversation. A user within the same tenant could read messages from any conversation by guessing/enumerating conversation GUIDs.

The `GetConversations` query correctly filters by user participation (`WHERE p.user_id = @UserId`), but `GetMessages` does not.

**Remediation:** Add a participant check to the `GetMessagesAsync` query:
```sql
INNER JOIN communications.participants p
    ON p.conversation_id = m.conversation_id
    AND p.user_id = @UserId
```

---

### MEDIUM-4: AsyncAPI Endpoints Publicly Accessible

**Severity:** MEDIUM
**File:** `src/Foundry.Api/Extensions/AsyncApiEndpointExtensions.cs:24-34`

**Issue:** Three AsyncAPI endpoints are registered with `.AllowAnonymous()`:
- `/asyncapi/v1.json` -- Returns the full async API document
- `/asyncapi/v1/flows` -- Returns Mermaid diagram of message flows
- `/asyncapi` -- HTML viewer

These endpoints expose the internal messaging architecture, event names, queue names, and module communication patterns to unauthenticated users. This is useful intelligence for an attacker to understand the system internals.

**Remediation:** Restrict these endpoints to development environment only (like the OpenAPI/Scalar endpoints), or require authentication.

---

### MEDIUM-5: Keycloak `ssl-required: none` in Development Config

**Severity:** MEDIUM
**File:** `src/Foundry.Api/appsettings.Development.json:31` (inferred from Keycloak config)

**Issue:** The development configuration sets `"ssl-required": "none"` for Keycloak. While acceptable for local development, if this configuration leaks to staging/production (e.g., through misconfigured environment overrides), JWT tokens would be transmitted over plain HTTP, making them susceptible to interception.

The production config correctly uses `"ssl-required": "external"`, so this is well-handled -- but the risk is configuration drift.

**Remediation:** Add a startup check that validates `ssl-required != "none"` when not in Development environment.

---

### LOW-1: Testing Config Contains Hardcoded Credentials

**Severity:** LOW
**File:** `src/Foundry.Api/appsettings.Testing.json`

**Issue:** Testing configuration contains hardcoded credentials:
- Database: `Username=test;Password=test`
- RabbitMQ: `amqp://guest:guest@localhost:5672`
- Keycloak: `"secret": "test-client-secret"`

While these are clearly test values and the file is appropriately named, they are checked into version control.

**Remediation:** These are acceptable for local test infrastructure (Testcontainers). No action required unless test infrastructure is shared. Consider documenting that these must never be used for any non-local environment.

---

### LOW-2: SCIM Discovery Endpoints Bypass Authentication

**Severity:** LOW
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/ScimAuthenticationMiddleware.cs:56-62`

**Issue:** SCIM discovery endpoints (`ServiceProviderConfig`, `Schemas`, `ResourceTypes`) are accessible without authentication per the SCIM spec. The middleware correctly identifies and bypasses auth for these. However, the controller is `[AllowAnonymous]`, which means **all** SCIM endpoints bypass ASP.NET Core authorization. The custom middleware handles auth, but if the middleware is removed or reordered, all SCIM endpoints become unauthenticated.

**Remediation:** Consider using ASP.NET Core's built-in authorization with a custom SCIM policy rather than relying solely on middleware ordering. Alternatively, add integration tests that verify SCIM endpoints require authentication.

---

### LOW-3: `GetCurrentUserId()` Returns `Guid.Empty` on Auth Failure (Storage)

**Severity:** LOW
**File:** `src/Modules/Storage/Foundry.Storage.Api/Controllers/StorageController.cs:262-272`

**Issue:** The `GetCurrentUserId()` helper in `StorageController` returns `Guid.Empty` if the user claim cannot be parsed. This value is then passed into the `UploadFileCommand.UserId`. Unlike the `ConversationsController` which returns `Unauthorized()` when the user ID is null, the storage controller proceeds with an empty GUID.

**Remediation:** Return `Unauthorized()` (or throw) if the user ID cannot be resolved, consistent with the Conversations controller pattern.

---

### LOW-4: Domain Exception Messages Exposed in All Environments

**Severity:** LOW
**File:** `src/Foundry.Api/Middleware/GlobalExceptionHandler.cs:91-95`

**Issue:** For `DomainException` and `ValidationException`, the exception message is always returned in the response (lines 91-100), regardless of environment. While domain exception messages are typically user-facing and safe, if a domain exception inadvertently contains internal details (e.g., entity IDs, SQL constraint names), this could leak information.

**Remediation:** Review all `DomainException` subclasses to ensure messages are user-safe. Consider sanitizing or truncating messages in production.

---

## What's Done Well

### Multi-Tenancy Isolation (Excellent)
- `TenantAwareDbContext` applies global query filters on all `ITenantScoped` entities via EF Core (`src/Shared/Foundry.Shared.Infrastructure/Persistence/TenantAwareDbContext.cs`)
- `TenantSaveChangesInterceptor` automatically stamps new entities with `TenantId` and prevents modification of existing tenant IDs
- All Dapper queries consistently include `WHERE tenant_id = @TenantId` with parameterized values
- Tenant context is resolved from JWT claims via `TenantResolutionMiddleware` with admin override properly gated behind `HasRealmAdminRole`
- Wolverine messages carry tenant context via `TenantStampingMiddleware` / `TenantRestoringMiddleware`

### SQL Injection Prevention (Excellent)
- All Dapper queries use parameterized queries consistently (`@TenantId`, `@UserId`, etc.)
- The `CustomFieldIndexManager` validates identifiers against a strict regex (`^[a-zA-Z_][a-zA-Z0-9_]{0,62}$`) before using them in DDL statements -- well-documented with comments explaining why parameterization isn't possible for DDL
- No string interpolation found in any SQL query

### Authentication & Authorization (Strong)
- All controllers have either `[Authorize]` or `[AllowAnonymous]` (with justification)
- Fine-grained `[HasPermission(PermissionType.XXX)]` on sensitive Identity, Configuration, and Billing endpoints
- API key authentication properly validates keys and sets tenant context
- SCIM uses custom Bearer token authentication via middleware
- Rate limiting on auth endpoints and upload endpoints
- `[EnableRateLimiting("auth")]` on `AuthController` to prevent brute force

### Security Headers (Strong)
- `SecurityHeadersMiddleware` sets: X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, CSP
- HSTS in production with 1-year max-age
- HTTPS redirection enabled in non-development

### Input Validation (Strong)
- FluentValidation middleware in Wolverine pipeline validates commands before handlers
- File upload validator includes magic byte checking for content type verification
- File name sanitization via `FileNameSanitizer`
- Path traversal protection in `LocalStorageProvider` using `GetFullPath` + base directory check
- HTML sanitization service used for admin-facing content (changelog, announcements)
- SignalR dispatcher sanitizes all outgoing payloads recursively

### Error Handling (Good)
- `GlobalExceptionHandler` follows RFC 7807 Problem Details
- Exception details (message + stack trace) only exposed in Development
- Production returns generic "An unexpected error occurred" for unhandled exceptions

### Secrets Management (Good)
- Production/Staging configs use `OVERRIDE_VIA_ENV_VAR` placeholders
- Base `appsettings.json` uses `SET_VIA_*` / `REPLACE_IN_PRODUCTION` markers
- Development config uses user secrets for Keycloak credentials
- No real secrets found hardcoded in source (only test fixtures)

### CORS Configuration (Good)
- Default policy uses explicitly configured origins from `Cors:AllowedOrigins`
- Development policy limited to specific localhost ports
- `AllowCredentials()` is paired with `WithOrigins()` (never with `AllowAnyOrigin`)

---

## Dependency Audit Notes

Dependency versions are centrally managed in `Directory.Packages.props`. A full CVE scan was not performed as part of this code-level audit. Recommend running `dotnet list package --vulnerable` periodically or integrating with GitHub Dependabot / OWASP Dependency-Check.

---

## Recommendations Summary (Priority Order)

1. **HIGH** -- Sanitize SCIM error responses; do not return `ex.Message` in production
2. **HIGH** -- Sanitize message body content or enforce plain-text rendering contract
3. **MEDIUM** -- Add participant authorization check to `GetMessagesAsync`
4. **MEDIUM** -- Add `[HasPermission]` to controllers currently using only `[Authorize]`
5. **MEDIUM** -- Restrict AsyncAPI endpoints to development or authenticated users
6. **MEDIUM** -- Harden Hangfire dashboard with standard ASP.NET Core authorization
7. **MEDIUM** -- Add startup validation for Keycloak SSL configuration
8. **LOW** -- Fix `StorageController.GetCurrentUserId()` to return 401 instead of `Guid.Empty`
9. **LOW** -- Add integration tests for SCIM authentication enforcement
10. **LOW** -- Audit domain exception messages for information safety
