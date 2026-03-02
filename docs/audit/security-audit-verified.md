# Security Audit Report -- Verified

**Date:** 2026-03-02
**Verified by:** security-verifier
**Original audit by:** security-auditor (automated)
**Scope:** All source files under `src/`
**Branch:** `expansion`

---

## Verification Summary

Each finding from the original security audit was verified against the actual source code. Results are categorized as CONFIRMED, FALSE POSITIVE, or SEVERITY ADJUSTED. Additional findings discovered during verification are listed at the end.

**Verified Finding Summary:**
| Severity | Original Count | Verified Count (incl. new) |
|----------|---------------|---------------------------|
| CRITICAL | 0 | 0 |
| HIGH | 2 | 2 |
| MEDIUM | 5 | 4 |
| LOW | 4 | 5 |

---

## Verified Findings

### HIGH-1: SCIM Controller Exposes Internal Exception Messages

**Status: CONFIRMED**
**Severity: HIGH (unchanged)**

Verified at `src/Modules/Identity/Foundry.Identity.Api/Controllers/ScimController.cs`. Seven catch blocks return `ex.Message` directly in `ScimError.Detail`:

- Line 96 (`CreateUser`)
- Line 122 (`UpdateUser`)
- Line 148 (`PatchUser`)
- Line 171 (`DeleteUser`)
- Line 232 (`CreateGroup`)
- Line 258 (`UpdateGroup`)
- Line 281 (`DeleteGroup`)

All catch generic `Exception` and pass `ex.Message` directly. The original audit listed 8 line numbers but only 7 catch blocks exist -- the line numbers in the original report were slightly off (e.g., it listed line 92 but the `Detail = ex.Message` is on line 96), but all locations are real.

The controller is marked `[AllowAnonymous]` (line 18) with custom Bearer token auth via `ScimAuthenticationMiddleware`. The middleware correctly enforces auth for non-discovery endpoints, but the `ex.Message` leak is confirmed and could expose Keycloak admin API errors, database constraint names, etc.

**Remediation:** Original suggestion is correct -- use environment-aware messages as in `GlobalExceptionHandler`.

---

### HIGH-2: Message Body Not HTML-Sanitized (Stored XSS Risk)

**Status: CONFIRMED**
**Severity: HIGH (unchanged)**

Verified at `src/Modules/Communications/Foundry.Communications.Api/Controllers/ConversationsController.cs:130-131`. The `SendMessage` endpoint passes `request.Body` directly into `SendMessageCommand` without sanitization.

The domain entity `Conversation.SendMessage()` at `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Entities/Conversation.cs:67-89` also performs no sanitization -- it only validates sender is an active participant.

Confirmed that `AdminAnnouncementsController` and `AdminChangelogController` both inject and use `IHtmlSanitizationService` (via grep), while `ConversationsController` does not. The `SignalRRealtimeDispatcher` does sanitize outgoing payloads, so real-time delivery is partially protected, but the stored message body in the database remains unsanitized, and direct API reads (`GetMessages`) would return raw HTML/script content.

**Remediation:** Original suggestion is correct. Option 1 (sanitize in controller) is the more robust approach.

---

### MEDIUM-1: Hangfire Dashboard Accessible in Non-Development Without Explicit Auth Gate

**Status: CONFIRMED, SEVERITY ADJUSTED to LOW**
**Severity: LOW (downgraded from MEDIUM)**

Verified at `src/Foundry.Api/Middleware/HangfireDashboardAuthFilter.cs` and `src/Foundry.Api/Extensions/HangfireExtensions.cs:36-39`.

The auth filter correctly requires `IsAuthenticated && IsInRole("Admin")` in non-development environments. While the audit correctly notes that this bypasses the standard ASP.NET Core authorization pipeline and uses Hangfire's own `IDashboardAuthorizationFilter`, the practical risk is low:

1. The dashboard is behind authentication (401 for unauthenticated, 403 for non-admin)
2. The auth check is straightforward and hard to bypass
3. The dashboard being discoverable (401 instead of 404) is a minimal information disclosure

The concern about CORS and rate limiting not applying is valid but low-impact for an admin-only dashboard. Downgrading to LOW.

**Remediation:** Original suggestion is reasonable as defense-in-depth but not urgent.

---

### MEDIUM-2: Missing Granular Permissions on Several Controllers

**Status: CONFIRMED**
**Severity: MEDIUM (unchanged)**

Verified by grepping all `[Authorize]` and `[HasPermission]` attributes across all module controllers. The following controllers use only `[Authorize]` with no `[HasPermission]` at either class or method level:

**Billing:**
- `UsageController.cs` (line 18)
- `MetersController.cs` (line 16)
- `QuotasController.cs` (line 19)
- `SubscriptionsController.cs` (line 20) -- **missed by original audit**
- `InvoicesController.cs` (line 23) -- **missed by original audit**
- `PaymentsController.cs` (line 19) -- **missed by original audit**

**Communications:**
- `NotificationsController.cs` (line 21)
- `EmailPreferencesController.cs` (line 20)
- `AnnouncementsController.cs` (line 19)
- `ConversationsController.cs` (line 23)

**Storage:**
- `StorageController.cs` (line 30)

**Identity:**
- `ApiKeysController.cs` (line 20)
- `ScopesController.cs` (line 17)

The original audit listed 10 controllers. There are actually 13 controllers with only `[Authorize]` -- the audit missed `SubscriptionsController`, `InvoicesController`, and `PaymentsController` in the Billing module. These are arguably higher risk since they deal with financial data.

Controllers that DO have proper `[HasPermission]` at method level: `UsersController`, `SsoController`, `ServiceAccountsController`, `OrganizationsController`, `RolesController`, `CustomFieldsController`, `FeatureFlagsController`.

**Remediation:** Original suggestion is correct. The 3 missed billing controllers should be prioritized alongside the originally identified ones.

---

### MEDIUM-3: Conversations Endpoint Lacks Participant Authorization Check (IDOR)

**Status: CONFIRMED**
**Severity: MEDIUM (unchanged)**

Verified at `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/MessagingQueryService.cs:41-95`. The `GetMessagesAsync` Dapper query joins only on `conversations` for tenant filtering but does NOT join on `participants` to verify the requesting user is in the conversation. The `@UserId` parameter is accepted by the method but is not used in the SQL query at all.

Compare with `GetConversationsAsync` (line 97+) which correctly uses `WHERE p.user_id = @UserId` via the `user_conversations` CTE.

Also compare with `GetUnreadConversationCountAsync` (line 15+) which correctly joins on `participants` with `AND p.user_id = @UserId`.

The write path (`SendMessage`) is protected at the domain level -- `Conversation.SendMessage()` at line 74 validates sender is an active participant. But the read path has the IDOR.

**Remediation:** Original SQL suggestion is correct. Add participant check to both branches of the cursor/no-cursor query in `GetMessagesAsync`.

---

### MEDIUM-4: AsyncAPI Endpoints Publicly Accessible

**Status: FALSE POSITIVE**
**Severity: N/A**

Verified at `src/Foundry.Api/Extensions/AsyncApiEndpointExtensions.cs:10-13`. The method begins with:

```csharp
if (!app.Environment.IsDevelopment())
{
    return app;
}
```

The endpoints are **already restricted to development only**. They are never registered in production/staging. The `.AllowAnonymous()` on lines 25/29/33 only applies in development, which is the expected behavior (same pattern as OpenAPI/Scalar at `Program.cs:292-302`).

This finding is a **false positive** -- the original auditor missed the environment guard at the top of the method.

---

### MEDIUM-5: Keycloak `ssl-required: none` in Development Config

**Status: CONFIRMED, SEVERITY ADJUSTED to LOW**
**Severity: LOW (downgraded from MEDIUM)**

Verified at `src/Foundry.Api/appsettings.Development.json:29`. The value is `"ssl-required": "none"`. This file is environment-specific (`appsettings.Development.json`) and only loads when `ASPNETCORE_ENVIRONMENT=Development`.

The original audit itself acknowledged this is "well-handled" since production uses `"ssl-required": "external"`. The risk of "configuration drift" is real but the standard ASP.NET Core configuration layering provides strong protection -- `appsettings.Development.json` simply does not load in production.

Downgrading to LOW because:
1. ASP.NET Core environment-specific config loading is deterministic
2. The production config would need to be explicitly broken
3. There are no `appsettings.Staging.json` or other files that could accidentally inherit this

**Remediation:** A startup validation check is a nice-to-have, not urgent.

---

### LOW-1: Testing Config Contains Hardcoded Credentials

**Status: CONFIRMED**
**Severity: LOW (unchanged)**

Verified at `src/Foundry.Api/appsettings.Testing.json`. Contains `Username=test;Password=test`, `amqp://guest:guest@localhost:5672`, and `"secret": "test-client-secret"`. These are clearly test-only values for Testcontainers/local development. No action required.

---

### LOW-2: SCIM Discovery Endpoints Bypass Authentication

**Status: CONFIRMED**
**Severity: LOW (unchanged)**

Verified at `src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/ScimAuthenticationMiddleware.cs:44-49`. The middleware correctly bypasses auth for discovery endpoints (ServiceProviderConfig, Schemas, ResourceTypes) per SCIM RFC 7643/7644. The `[AllowAnonymous]` on the controller (line 18 of ScimController) means ASP.NET Core authorization is bypassed for ALL SCIM endpoints, with the custom middleware providing the auth gate.

The middleware is well-implemented -- it validates Bearer tokens, creates a `ClaimsPrincipal`, and properly rejects invalid tokens. The risk is middleware ordering, which is a valid concern but mitigated by the explicit pipeline order documented in `Program.cs`.

**Remediation:** Integration tests for SCIM auth enforcement would be valuable.

---

### LOW-3: `GetCurrentUserId()` Returns `Guid.Empty` on Auth Failure (Storage)

**Status: CONFIRMED**
**Severity: LOW (unchanged)**

Verified at `src/Modules/Storage/Foundry.Storage.Api/Controllers/StorageController.cs:301-312`. The `GetCurrentUserId()` method returns `Guid.Empty` when the claim is missing or unparseable. This is used in the `Upload` endpoint (line 155) where it becomes `UploadFileCommand.UserId`.

Compare with `ConversationsController.GetCurrentUserId()` (line 171-182) which returns `null` and the caller returns `Unauthorized()`. The Storage pattern is inconsistent and could result in files being associated with `Guid.Empty` as the uploader, though the `[Authorize]` attribute should prevent unauthenticated access in practice.

**Remediation:** Original suggestion is correct.

---

### LOW-4: Domain Exception Messages Exposed in All Environments

**Status: CONFIRMED**
**Severity: LOW (unchanged)**

Verified at `src/Foundry.Api/Middleware/GlobalExceptionHandler.cs:91-94`. For `DomainException`, `exception.Message` is always returned regardless of environment. For `ValidationException` (lines 96-101), individual error messages are also always returned.

The non-domain, non-validation path correctly gates on `_environment.IsDevelopment()` (lines 103-108). The domain exception messages are designed to be user-facing (e.g., "Cannot send messages to an archived conversation"), so this is acceptable if messages are reviewed for safety.

**Remediation:** Original suggestion is correct -- review is appropriate but not urgent.

---

## NEW Findings (Missed by Original Audit)

### NEW-1: SSO Test Endpoint Returns Full Stack Trace via `DebugInfo`

**Severity: MEDIUM**
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakSsoService.cs:237`
**Controller:** `src/Modules/Identity/Foundry.Identity.Api/Controllers/SsoController.cs:113-117`

**Issue:** The `TestConnectionAsync` method catches `Exception` and creates:
```csharp
return new SsoTestResult(false, ex.Message, ex.ToString());
```

The third parameter (`ex.ToString()`) contains the full exception stack trace, inner exceptions, and type information. This `DebugInfo` field is returned directly to the client via `SsoController.TestConnection()`:
```csharp
SsoTestResult result = await _ssoService.TestConnectionAsync(ct);
return Ok(result);
```

The `SsoTestResult` DTO (at `SsoConfigurationDto.cs:76-79`) includes `string? DebugInfo` which is serialized to JSON. This is worse than `ex.Message` alone -- it exposes full stack traces including internal paths, library versions, and connection details.

While the endpoint requires `[HasPermission(PermissionType.SsoManage)]` (admin-only), defense-in-depth dictates that stack traces should never leave the server even for admin users.

**Remediation:** Remove `DebugInfo` from the DTO or gate it behind `IsDevelopment()`. Log the full exception server-side instead.

---

### NEW-2: Three Additional Billing Controllers Missing `[HasPermission]`

**Severity: MEDIUM (included in MEDIUM-2 verified count)**
**Files:**
- `src/Modules/Billing/Foundry.Billing.Api/Controllers/SubscriptionsController.cs` (line 20)
- `src/Modules/Billing/Foundry.Billing.Api/Controllers/InvoicesController.cs` (line 23)
- `src/Modules/Billing/Foundry.Billing.Api/Controllers/PaymentsController.cs` (line 19)

**Issue:** These controllers handle subscription management, invoice generation, and payment processing -- sensitive financial operations -- but only require `[Authorize]` without granular `[HasPermission]` checks. The original audit identified Billing's `UsageController`, `MetersController`, and `QuotasController` but missed these three.

This is noted as an addendum to MEDIUM-2 rather than a fully separate finding.

---

## What the Original Audit Got Right

The following positive assessments were spot-checked and confirmed:

- **Multi-tenancy isolation**: `TenantAwareDbContext` query filters and Dapper `@TenantId` parameterization confirmed
- **SQL injection prevention**: All Dapper queries use parameterized values; no string interpolation in SQL confirmed
- **Security headers**: `SecurityHeadersMiddleware` confirmed
- **File upload validation**: Magic byte checking and filename sanitization confirmed
- **CORS configuration**: `AllowCredentials()` paired with `WithOrigins()` confirmed
- **Rate limiting**: `[EnableRateLimiting("auth")]` and `[EnableRateLimiting("upload")]` confirmed
- **Secrets management**: `SET_VIA_USER_SECRETS` and `OVERRIDE_VIA_ENV_VAR` patterns confirmed

---

## Final Verified Count

| Severity | Count | Findings |
|----------|-------|----------|
| CRITICAL | 0 | -- |
| HIGH | 2 | HIGH-1 (SCIM exception leak), HIGH-2 (message body XSS) |
| MEDIUM | 4 | MEDIUM-2 (missing permissions, expanded to 13 controllers), MEDIUM-3 (GetMessages IDOR), NEW-1 (SSO stack trace leak) |
| LOW | 5 | LOW-1 (test creds), LOW-2 (SCIM discovery bypass), LOW-3 (Storage Guid.Empty), LOW-4 (domain exception messages), MEDIUM-1 downgraded (Hangfire), MEDIUM-5 downgraded (Keycloak SSL) |

**Changes from original:**
- MEDIUM-4 (AsyncAPI endpoints) removed as **false positive** (already dev-only gated)
- MEDIUM-1 (Hangfire) downgraded to LOW
- MEDIUM-5 (Keycloak SSL) downgraded to LOW
- NEW-1 added: SSO test endpoint returns full stack trace (MEDIUM)
- MEDIUM-2 expanded: 3 additional billing controllers identified (SubscriptionsController, InvoicesController, PaymentsController)

## Priority Remediation Order

1. **HIGH** -- Sanitize SCIM error responses; do not return `ex.Message` in production
2. **HIGH** -- Sanitize message body content or enforce plain-text rendering contract
3. **MEDIUM** -- Add participant authorization check to `GetMessagesAsync` (IDOR)
4. **MEDIUM** -- Add `[HasPermission]` to all 13 controllers currently using only `[Authorize]`
5. **MEDIUM** -- Remove `DebugInfo` (full stack trace) from `SsoTestResult` API response
6. **LOW** -- Fix `StorageController.GetCurrentUserId()` to return 401 instead of `Guid.Empty`
7. **LOW** -- Add integration tests for SCIM authentication enforcement
8. **LOW** -- Review domain exception messages for information safety
9. **LOW** -- Consider additional auth hardening for Hangfire dashboard (defense-in-depth)
10. **LOW** -- Add startup validation for Keycloak SSL configuration (nice-to-have)
