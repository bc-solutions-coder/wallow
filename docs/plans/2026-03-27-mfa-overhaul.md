# MFA Overhaul Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix all broken MFA flows and implement full MFA lifecycle with partial-auth sessions, external login enforcement, admin controls, and E2E testing.

**Architecture:** Introduce a partial-auth cookie (`Identity.MfaPartial`) that replaces challenge tokens and sign-in tickets for MFA flows. This cookie authenticates users to MFA-specific endpoints without granting full app access. On successful MFA verification or enrollment, the partial session upgrades to a full ASP.NET Identity cookie.

**Tech Stack:** ASP.NET Core Identity, OpenIddict, Redis (Valkey), Blazor Server, Playwright, OtpNet, qrcode.js

**Design doc:** `docs/plans/2026-03-27-mfa-overhaul-design.md`

---

## Phase 1: Partial-Auth Session Infrastructure

### Task 1: Create MFA Partial-Auth Cookie Service

Build the core service that issues and validates partial-auth cookies for MFA flows.

**Files:**
- Create: `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/MfaPartialAuthService.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IMfaPartialAuthService.cs`
- Test: `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/MfaPartialAuthServiceTests.cs`

**Step 1: Write the failing tests**

Create tests for the partial-auth service covering: issue cookie, validate cookie, upgrade to full auth, expired cookie rejection.

Key test cases:
- `IssuePartialCookie_SetsHttpOnlyCookieWithCorrectExpiry`
- `ValidatePartialCookie_ReturnsMfaPartialAuthPayload`
- `ValidatePartialCookie_ReturnsNull_WhenExpired`
- `ValidatePartialCookie_ReturnsNull_WhenMissing`
- `UpgradeToFullAuth_CallsSignInAsync_AndDeletesPartialCookie`

The service should use `IDataProtectionProvider` with purpose `"Identity.MfaPartial"` and a 5-minute time-limited protector. The cookie payload is a `MfaPartialAuthPayload` record:

```csharp
public sealed record MfaPartialAuthPayload(
    string UserId,
    string Email,
    string AuthMethod, // "password", "magic_link", "otp", "external:{provider}"
    bool RememberMe,
    DateTimeOffset IssuedAt);
```

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh identity`

**Step 3: Implement the interface and service**

Interface at `IMfaPartialAuthService.cs`:
```csharp
public interface IMfaPartialAuthService
{
    void IssuePartialCookie(HttpContext httpContext, MfaPartialAuthPayload payload);
    MfaPartialAuthPayload? ValidatePartialCookie(HttpContext httpContext);
    Task UpgradeToFullAuthAsync(HttpContext httpContext, SignInManager<WallowUser> signInManager);
    void DeletePartialCookie(HttpContext httpContext);
}
```

Implementation at `MfaPartialAuthService.cs`:
- Cookie name: `Identity.MfaPartial`
- `HttpOnly = true`, `Secure = true`, `SameSite = Lax`, `MaxAge = 5 minutes`
- Protect/unprotect payload via `ITimeLimitedDataProtector`
- `UpgradeToFullAuthAsync`: reads payload, finds user by ID, calls `signInManager.SignInAsync(user, payload.RememberMe)`, deletes partial cookie

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh identity`

**Step 5: Commit**

```bash
git add src/Modules/Identity/Wallow.Identity.Application/Interfaces/IMfaPartialAuthService.cs \
        src/Modules/Identity/Wallow.Identity.Infrastructure/Services/MfaPartialAuthService.cs \
        tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/MfaPartialAuthServiceTests.cs
git commit -m "feat(identity): add MFA partial-auth cookie service"
```

---

### Task 2: Create AuthorizeMfaPartial Attribute and Handler

Create a custom authorization attribute that validates the partial-auth cookie.

**Files:**
- Create: `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/AuthorizeMfaPartialAttribute.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/MfaPartialAuthorizationHandler.cs`
- Modify: `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs:356`
- Test: `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/MfaPartialAuthorizationHandlerTests.cs`

**Step 1: Write the failing tests**

Test cases:
- `HandleRequirementAsync_Succeeds_WhenValidPartialCookiePresent`
- `HandleRequirementAsync_Succeeds_WhenFullyAuthenticated` (dual access)
- `HandleRequirementAsync_Fails_WhenNoPartialCookieAndNotAuthenticated`
- `HandleRequirementAsync_Fails_WhenPartialCookieExpired`

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh identity`

**Step 3: Implement the attribute and handler**

`AuthorizeMfaPartialAttribute` extends `AuthorizeAttribute` with policy `"MfaPartial"`.

`MfaPartialAuthorizationHandler` implements `AuthorizationHandler<MfaPartialRequirement>`:
- Check if `context.User.Identity?.IsAuthenticated == true` -> succeed (full auth)
- Otherwise check `IMfaPartialAuthService.ValidatePartialCookie(httpContext)` -> succeed if valid payload
- Otherwise fail

Register in DI:
```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("MfaPartial", policy =>
        policy.Requirements.Add(new MfaPartialRequirement()));
});
services.AddScoped<IAuthorizationHandler, MfaPartialAuthorizationHandler>();
```

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh identity`

**Step 5: Commit**

```bash
git add src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/ \
        src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs \
        tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/MfaPartialAuthorizationHandlerTests.cs
git commit -m "feat(identity): add AuthorizeMfaPartial attribute and handler"
```

---

## Phase 2: Fix Login Flow (P0 Bugs)

### Task 3: Rewrite AccountController Login to Use Partial-Auth Cookies

Replace challenge token and sign-in ticket responses with partial-auth cookie issuance.

**Files:**
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs:59-96` (Login method)
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs:98-121` (VerifyMfaChallenge method)
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs:30-43` (constructor - add IMfaPartialAuthService)
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/MfaLoginVerifyRequest.cs` (simplify to Code + UseBackupCode)
- Test: `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/AccountControllerTests.cs`

**Step 1: Write the failing tests**

Test cases for Login:
- `Login_WithMfaEnabled_SetsPartialCookieAndReturnsMfaRequired`
- `Login_WithOrgMfaRequired_AndExpiredGrace_SetsPartialCookieAndReturnsMfaEnrollmentRequired`
- `Login_WithOrgMfaRequired_AndActiveGrace_SetsFullCookieAndReturnsBanner`
- `Login_WithoutMfa_SetsFullCookieAndReturnsSuccess`

Test cases for VerifyMfaChallenge:
- `VerifyMfaChallenge_WithValidCode_UpgradesToFullAuth`
- `VerifyMfaChallenge_WithValidBackupCode_UpgradesToFullAuth`
- `VerifyMfaChallenge_WithInvalidCode_Returns401`
- `VerifyMfaChallenge_WithoutPartialCookie_Returns401`

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh identity`

**Step 3: Implement the changes**

Login method changes:
- Inject `IMfaPartialAuthService` via constructor
- After successful password check, if MFA required:
  - Call `mfaPartialAuthService.IssuePartialCookie(HttpContext, payload)` with `AuthMethod = "password"`
  - Return `Ok(new { succeeded = false, mfaRequired = true })` - no `challengeToken` in response
- If org requires MFA + not enrolled + outside grace:
  - Issue partial cookie, return `Ok(new { succeeded = false, mfaEnrollmentRequired = true })`
- If org requires MFA + not enrolled + inside grace:
  - Call `signInManager.SignInAsync(user, rememberMe)` directly (full cookie)
  - Return `Ok(new { succeeded = true, mfaEnrollmentRequired = true, mfaGraceDeadline = deadline })`
- If no MFA: call `signInManager.SignInAsync(user, rememberMe)`, return `Ok(new { succeeded = true })`

VerifyMfaChallenge changes:
- Change attribute from `[AllowAnonymous]` to `[Authorize(Policy = "MfaPartial")]`
- Read user ID from partial cookie: `mfaPartialAuthService.ValidatePartialCookie(HttpContext)`
- Simplify request to `MfaVerifyRequest(string Code, bool UseBackupCode = false)` - no Email, ChallengeToken, RememberMe
- On success: call `mfaPartialAuthService.UpgradeToFullAuthAsync(HttpContext, signInManager)`
- Remove `CreateSignInTicket` call from this method

Add tenant MFA enforcement:
- After password validation succeeds, check org settings:
  ```csharp
  bool orgRequiresMfa = await CheckOrgMfaRequirementAsync(user, ct);
  ```
- If org requires MFA and user not enrolled, check grace period
- If grace deadline is null, set it: `user.SetMfaGraceDeadline(...)` and save

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh identity`

**Step 5: Commit**

```bash
git commit -m "feat(identity): rewrite login flow to use partial-auth cookies"
```

---

### Task 4: Rewrite MfaController to Support Dual Auth (Partial + Full)

Update enrollment endpoints to work with both partial-auth (during login) and full auth (from dashboard).

**Files:**
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/MfaController.cs` (entire file)
- Test: `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/MfaControllerTests.cs`

**Step 1: Write the failing tests**

Test cases:
- `EnrollTotp_WithPartialCookie_ReturnsSecretAndQrUri`
- `EnrollTotp_WithFullAuth_ReturnsSecretAndQrUri`
- `ConfirmEnrollment_WithPartialCookie_EnablesMfaAndUpgradesToFullAuth`
- `ConfirmEnrollment_WithFullAuth_EnablesMfaAndReturnsBackupCodes`
- `GetStatus_ReturnsCurrentMfaState`

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh identity`

**Step 3: Implement the changes**

- Change class-level `[Authorize]` to per-endpoint attributes
- `EnrollTotp` and `ConfirmEnrollment`: `[Authorize(Policy = "MfaPartial")]` (works with both)
- `ConfirmEnrollment`: after enabling MFA, if partial cookie present -> upgrade to full auth
- Add `GetStatus` endpoint: `[Authorize(Policy = "MfaPartial")]`
  - Returns `{ mfaEnabled, method, backupCodeCount }`
- Add helper to get user ID from either partial cookie or full auth claims
- Remove `IssueChallenge` and `VerifyChallenge` endpoints (challenge logic moved to AccountController with partial cookies)

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh identity`

**Step 5: Commit**

```bash
git commit -m "feat(identity): update MfaController for dual partial/full auth"
```

---

## Phase 3: Fix Auth App (Blazor)

### Task 5: Update Auth App Models and API Client

Update the AuthResponse model and AuthApiClient to match new API contracts.

**Files:**
- Modify: `src/Wallow.Auth/Models/AuthResponse.cs`
- Modify: `src/Wallow.Auth/Services/IAuthApiClient.cs`
- Modify: `src/Wallow.Auth/Services/AuthApiClient.cs:235-284` (MFA methods)
- Test: `tests/Wallow.Auth.Tests/Services/AuthApiClientTests.cs`

**Step 1: Update AuthResponse model**

Remove `MfaChallengeToken`, `MfaMethod`, and `SignInTicket` fields:

```csharp
public sealed record AuthResponse(
    bool Succeeded,
    string? Error = null,
    bool MfaChallengeRequired = false,
    bool MfaEnrollmentRequired = false,
    DateTimeOffset? MfaGraceDeadline = null);
```

**Step 2: Simplify IAuthApiClient MFA methods**

```csharp
Task<AuthResponse> VerifyMfaChallengeAsync(string code, CancellationToken ct = default);
Task<AuthResponse> UseBackupCodeAsync(string code, CancellationToken ct = default);
```

Remove `challengeToken` parameter - partial cookie handles identity.

**Step 3: Update AuthApiClient implementation**

`VerifyMfaChallengeAsync`:
- POST to `{BasePath}/mfa/verify` with `{ Code = code, UseBackupCode = false }`
- The `HttpClient` is configured with the same base URL so cookies flow automatically

`UseBackupCodeAsync`:
- Same endpoint with `UseBackupCode = true`

**Step 4: Run tests**

Run: `./scripts/run-tests.sh auth`

**Step 5: Commit**

```bash
git commit -m "refactor(auth): simplify auth models and API client for partial-auth flow"
```

---

### Task 6: Fix Login.razor MFA Handling

Simplify the login page's MFA redirect logic.

**Files:**
- Modify: `src/Wallow.Auth/Components/Pages/Login.razor:459-500` (HandleSuccessfulAuth method)
- Modify: `src/Wallow.Auth/Components/Pages/Login.razor:304-343` (HandleLogin method)

**Step 1: Update HandleSuccessfulAuth**

```csharp
private void HandleSuccessfulAuth(AuthResponse result)
{
    if (result.MfaChallengeRequired)
    {
        string returnUrl = ReturnUrl ?? "/";
        Navigation.NavigateTo($"/mfa/challenge?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    if (result.MfaEnrollmentRequired)
    {
        bool withinGracePeriod = result.MfaGraceDeadline.HasValue
            && result.MfaGraceDeadline.Value > DateTimeOffset.UtcNow;

        if (!withinGracePeriod)
        {
            string returnUrl = ReturnUrl ?? "/";
            Navigation.NavigateTo($"/mfa/enroll?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        _showMfaEnrollmentBanner = true;
        _mfaGraceDeadline = result.MfaGraceDeadline?.DateTime;
    }

    // No ticket exchange needed - cookie was set by the API
    if (!string.IsNullOrEmpty(ReturnUrl))
    {
        Navigation.NavigateTo(ReturnUrl, forceLoad: true);
    }
    else
    {
        _signedIn = true;
    }
}
```

Key changes:
- No `challengeToken` or `method` in URLs
- No `signInTicket` exchange - full cookie already set by API when no MFA needed
- Only `returnUrl` passed to MFA pages

**Step 2: Update HandleLogin response handling**

The current code checks `result.Succeeded` before calling `HandleSuccessfulAuth`. Now `mfaRequired` comes back as `succeeded = false`. Update:

```csharp
if (result.Succeeded || result.MfaChallengeRequired || result.MfaEnrollmentRequired)
{
    HandleSuccessfulAuth(result);
}
```

**Step 3: Commit**

```bash
git commit -m "fix(auth): simplify login MFA redirect to use returnUrl only"
```

---

### Task 7: Fix MfaChallenge.razor

Rewrite the challenge page to use partial-auth cookie instead of query param tokens.

**Files:**
- Modify: `src/Wallow.Auth/Components/Pages/MfaChallenge.razor` (entire file)
- Test: `tests/Wallow.Auth.Component.Tests/Pages/MfaChallengeTests.cs`

**Step 1: Write/update component tests**

Test cases:
- `Renders_CodeInputForm`
- `Submit_WithValidCode_RedirectsToReturnUrl`
- `Submit_WithInvalidCode_ShowsError`
- `ToggleBackupCode_SwitchesMode`
- `Submit_WithBackupCode_RedirectsToReturnUrl`
- `MissingReturnUrl_DefaultsToRoot`

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh auth`

**Step 3: Rewrite the page**

Key changes:
- Remove `[SupplyParameterFromQuery] public string? ChallengeToken` - no longer needed
- Keep `[SupplyParameterFromQuery] public string? ReturnUrl`
- `HandleVerify` calls `AuthClient.VerifyMfaChallengeAsync(code)` (no token param)
- On success: `Navigation.NavigateTo(ReturnUrl ?? "/", forceLoad: true)` - cookie already upgraded
- Add `data-testid` attributes:
  - `mfa-challenge-code` - code input
  - `mfa-challenge-submit` - verify button
  - `mfa-challenge-error` - error message
  - `mfa-challenge-backup-toggle` - toggle button
  - `mfa-challenge-backup-code` - backup code input (when in backup mode)
  - `mfa-challenge-success` - success message

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh auth`

**Step 5: Commit**

```bash
git commit -m "fix(auth): rewrite MfaChallenge to use partial-auth cookie"
```

---

### Task 8: Fix MfaEnroll.razor and Add QR Code

Update enrollment page to work with partial-auth cookie and add QR code rendering.

**Files:**
- Modify: `src/Wallow.Auth/Components/Pages/MfaEnroll.razor`
- Create: `src/Wallow.Auth/wwwroot/js/qrcode-interop.js`
- Test: `tests/Wallow.Auth.Component.Tests/Pages/MfaEnrollTests.cs`

**Step 1: Add qrcode.js dependency**

Add `qrcode.js` via CDN link in the Auth app layout. Create a JS interop file:

```javascript
// qrcode-interop.js
window.QrCodeInterop = {
    generate: function(elementId, text) {
        const container = document.getElementById(elementId);
        if (!container) return;
        while (container.firstChild) {
            container.removeChild(container.firstChild);
        }
        new QRCode(container, {
            text: text,
            width: 200,
            height: 200,
            correctLevel: QRCode.CorrectLevel.M
        });
    }
};
```

Reference the script in `App.razor` or the layout.

**Step 2: Update MfaEnroll.razor**

Key changes:
- Remove direct `HttpClientFactory` usage - use `IAuthApiClient` via DI
- Add QR code container with `data-testid="mfa-enroll-qr-code"`
- After receiving `qrUri` from API, call JS interop to render QR code
- Keep manual secret display as fallback
- On successful confirm:
  - If coming from login flow (returnUrl present): redirect to returnUrl (cookie upgraded by API)
  - If from dashboard: show success message with backup codes, navigate back to settings

**Step 3: Run tests**

Run: `./scripts/run-tests.sh auth`

**Step 4: Commit**

```bash
git commit -m "feat(auth): add QR code rendering to MFA enrollment page"
```

---

## Phase 4: External Login MFA Enforcement

### Task 9: Update ExternalLoginCallback for MFA

Enforce MFA for external login users instead of bypassing it.

**Files:**
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs:150-228` (ExternalLoginCallback)
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs:230-371` (CompleteExternalRegistration)
- Test: `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/AccountControllerExternalLoginTests.cs`

**Step 1: Write the failing tests**

Test cases:
- `ExternalLoginCallback_ExistingUser_MfaEnabled_SetsPartialCookieAndRedirectsToChallenge`
- `ExternalLoginCallback_ExistingUser_OrgRequiresMfa_SetsPartialCookieAndRedirectsToChallenge`
- `ExternalLoginCallback_ExistingUser_NoMfa_SignsInDirectly`
- `ExternalLoginCallback_NewUser_OrgRequiresMfa_SetsGraceDeadline`
- `CompleteExternalRegistration_OrgRequiresMfa_SetsGraceDeadlineOnNewUser`

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh identity`

**Step 3: Implement the changes**

In `ExternalLoginCallback`:
- Replace `bypassTwoFactor: true` with MFA check
- After successful `ExternalLoginSignInAsync`, find user and check MFA status:
  ```csharp
  WallowUser? user = await signInManager.UserManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
  bool orgRequiresMfa = await CheckOrgMfaRequirementAsync(user, ct);

  if (user.MfaEnabled || orgRequiresMfa)
  {
      mfaPartialAuthService.IssuePartialCookie(HttpContext, new MfaPartialAuthPayload(
          user.Id.ToString(), user.Email!, $"external:{info.LoginProvider}", false, DateTimeOffset.UtcNow));

      await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
      string authUrl = GetRequiredAuthUrl();

      if (!user.MfaEnabled && orgRequiresMfa)
          return Redirect($"{authUrl}/mfa/enroll?returnUrl={Uri.EscapeDataString(returnUrl)}");

      return Redirect($"{authUrl}/mfa/challenge?returnUrl={Uri.EscapeDataString(returnUrl)}");
  }
  ```

In `CompleteExternalRegistration`:
- After creating new user, check org MFA requirement
- If org requires MFA, set grace deadline on the new user

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh identity`

**Step 5: Commit**

```bash
git commit -m "fix(identity): enforce MFA for external login flows"
```

---

## Phase 5: MFA Management Endpoints

### Task 10: Add MFA Disable Endpoint

**Files:**
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/MfaController.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/MfaDisableRequest.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Identity/Events/UserMfaDisabledEvent.cs`
- Test: `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/MfaControllerTests.cs`

**Step 1: Write the failing tests**

- `DisableMfa_WithValidPassword_DisablesMfaAndPublishesEvent`
- `DisableMfa_WithInvalidPassword_Returns401`
- `DisableMfa_WhenMfaNotEnabled_Returns400`

**Step 2: Implement**

```csharp
[HttpPost("disable")]
[Authorize]
public async Task<IActionResult> DisableMfa([FromBody] MfaDisableRequest request, CancellationToken ct)
{
    string userId = GetUserIdClaim();
    WallowUser? user = await userManager.FindByIdAsync(userId);
    if (user is null) return NotFound();

    bool passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
    if (!passwordValid) return Unauthorized(new { succeeded = false, error = "invalid_password" });

    if (!user.MfaEnabled) return BadRequest(new { succeeded = false, error = "mfa_not_enabled" });

    user.DisableMfa();
    await userManager.UpdateAsync(user);

    await messageBus.PublishAsync(new UserMfaDisabledEvent { UserId = user.Id, Email = user.Email! });

    return Ok(new { succeeded = true });
}
```

Request: `public sealed record MfaDisableRequest(string Password);`

**Step 3: Run tests, commit**

```bash
git commit -m "feat(identity): add MFA disable endpoint with password confirmation"
```

---

### Task 11: Add Backup Code Regeneration Endpoint

**Files:**
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/MfaController.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/MfaRegenerateBackupCodesRequest.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Identity/Events/UserMfaBackupCodesRegeneratedEvent.cs`
- Test: `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/MfaControllerTests.cs`

**Step 1: Write the failing tests**

- `RegenerateBackupCodes_WithValidPassword_ReturnsNewCodes`
- `RegenerateBackupCodes_WithInvalidPassword_Returns401`
- `RegenerateBackupCodes_WhenMfaNotEnabled_Returns400`

**Step 2: Implement**

```csharp
[HttpPost("backup-codes/regenerate")]
[Authorize]
public async Task<IActionResult> RegenerateBackupCodes(
    [FromBody] MfaRegenerateBackupCodesRequest request, CancellationToken ct)
{
    string userId = GetUserIdClaim();
    WallowUser? user = await userManager.FindByIdAsync(userId);
    if (user is null) return NotFound();

    bool passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
    if (!passwordValid) return Unauthorized(new { succeeded = false, error = "invalid_password" });

    if (!user.MfaEnabled) return BadRequest(new { succeeded = false, error = "mfa_not_enabled" });

    List<string> backupCodes = await mfaService.GenerateBackupCodesAsync(ct);
    string backupCodesHash = string.Join(",", backupCodes);
    user.SetBackupCodes(backupCodesHash);
    await userManager.UpdateAsync(user);

    await messageBus.PublishAsync(new UserMfaBackupCodesRegeneratedEvent
        { UserId = user.Id, Email = user.Email! });

    return Ok(new { succeeded = true, backupCodes });
}
```

**Step 3: Run tests, commit**

```bash
git commit -m "feat(identity): add backup code regeneration endpoint"
```

---

### Task 12: Add Admin MFA Management Endpoints

**Files:**
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/MfaController.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Identity/Events/UserMfaLockoutClearedEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Identity/Events/UserMfaEnabledEvent.cs`
- Test: `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/MfaControllerTests.cs`

**Step 1: Write the failing tests**

- `AdminDisableMfa_WithManageUsersPermission_DisablesMfa`
- `AdminDisableMfa_WithoutPermission_Returns403`
- `AdminClearLockout_ClearsRedisFailureCounter`
- `AdminCompliance_ReturnsOrgMfaStats`

**Step 2: Implement three admin endpoints**

```csharp
[HttpPost("admin/{userId}/disable")]
[Authorize]
[HasPermission(PermissionType.ManageUsers)]
public async Task<IActionResult> AdminDisableMfa(Guid userId, CancellationToken ct) { ... }

[HttpPost("admin/{userId}/clear-lockout")]
[Authorize]
[HasPermission(PermissionType.ManageUsers)]
public async Task<IActionResult> AdminClearLockout(Guid userId, CancellationToken ct) { ... }

[HttpGet("admin/compliance")]
[Authorize]
[HasPermission(PermissionType.ManageUsers)]
public async Task<IActionResult> GetComplianceOverview(CancellationToken ct) { ... }
```

Compliance overview uses Dapper query scoped to current tenant:
```sql
SELECT
    COUNT(*) AS TotalUsers,
    COUNT(*) FILTER (WHERE mfa_enabled = true) AS MfaEnabledCount,
    COUNT(*) FILTER (WHERE mfa_enabled = false AND "MfaGraceDeadline" > NOW()) AS PendingCount,
    COUNT(*) FILTER (WHERE mfa_enabled = false
        AND ("MfaGraceDeadline" IS NULL OR "MfaGraceDeadline" <= NOW())) AS NonCompliantCount
FROM identity."AspNetUsers"
WHERE tenant_id = @TenantId
```

**Step 3: Run tests, commit**

```bash
git commit -m "feat(identity): add admin MFA management endpoints"
```

---

## Phase 6: Cleanup

### Task 13: Remove Dead Code and Consolidate

**Files:**
- Delete: `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IMfaChallengeHandler.cs`
- Delete: `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/ExtensionPoints/NoOpMfaChallengeHandler.cs`
- Modify: `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs:356` (remove IMfaChallengeHandler registration)
- Modify: `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IMfaService.cs` (consolidate overloads)
- Modify: `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/MfaService.cs` (remove distributed cache challenge, keep Redis only)
- Delete: `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/MfaLoginVerifyRequest.cs` (replaced by MfaVerifyRequest)

**Step 1: Delete IMfaChallengeHandler and NoOp**

Remove the interface, the NoOp implementation, and the DI registration at line 356 of `IdentityInfrastructureExtensions.cs`.

**Step 2: Consolidate IMfaService**

Remove:
- `IssueChallengeAsync(string userId, CancellationToken ct)` - distributed cache version
- `ValidateChallengeAsync(string userId, string code, CancellationToken ct)` - distributed cache version

Keep:
- `IssueMfaChallengeTokenAsync` -> rename to `IssueChallengeAsync`
- `ValidateChallengeAsync(string userId, string challengeToken, string code, CancellationToken ct)` - Redis version

Update all callers.

**Step 3: Remove MfaLoginVerifyRequest**

Delete the old request type. The new `MfaVerifyRequest(string Code, bool UseBackupCode = false)` already exists in `MfaController.cs`.

**Step 4: Run full test suite**

Run: `./scripts/run-tests.sh`

**Step 5: Commit**

```bash
git commit -m "refactor(identity): remove dead MFA code and consolidate challenge mechanisms"
```

---

## Phase 7: Web App MFA Management UI

### Task 14: Add MFA API Client to Web App

**Files:**
- Create: `src/Wallow.Web/Services/IMfaApiClient.cs`
- Create: `src/Wallow.Web/Services/MfaApiClient.cs`
- Modify: `src/Wallow.Web/Program.cs` (register HttpClient + service)

**Step 1: Implement the client**

```csharp
public interface IMfaApiClient
{
    Task<MfaStatusResponse> GetStatusAsync(CancellationToken ct = default);
    Task<MfaDisableResponse> DisableAsync(string password, CancellationToken ct = default);
    Task<MfaRegenerateResponse> RegenerateBackupCodesAsync(string password, CancellationToken ct = default);
}
```

Calls the API using the user's auth cookie (HttpClient configured with cookie forwarding).

**Step 2: Commit**

```bash
git commit -m "feat(web): add MFA API client service"
```

---

### Task 15: Add MFA Section to Dashboard Settings

**Files:**
- Modify: `src/Wallow.Web/Components/Pages/Dashboard/Settings.razor`
- Create: `src/Wallow.Web/Components/Shared/MfaSettingsSection.razor`

**Step 1: Create MfaSettingsSection component**

Shows:
- MFA status badge (enabled/disabled)
- Method (TOTP)
- Remaining backup code count
- "Enable MFA" button -> navigates to Auth app's `/mfa/enroll`
- "Disable MFA" button -> password confirmation dialog -> calls disable endpoint
- "Regenerate Backup Codes" button -> password confirmation -> calls regenerate endpoint -> shows new codes

Add `data-testid` attributes:
- `settings-mfa-status`
- `settings-mfa-enable`
- `settings-mfa-disable`
- `settings-mfa-regenerate`
- `settings-mfa-backup-count`

**Step 2: Add to Settings.razor**

Include `<MfaSettingsSection />` in the settings page layout.

**Step 3: Commit**

```bash
git commit -m "feat(web): add MFA management section to dashboard settings"
```

---

## Phase 8: Audit Events

### Task 16: Add MFA Audit Events

**Files:**
- Create: `src/Shared/Wallow.Shared.Contracts/Identity/Events/UserMfaEnabledEvent.cs` (if not already from Task 12)
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/MfaController.cs` (publish events)
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs` (publish on upgrade)

**Step 1: Create event records**

```csharp
public sealed record UserMfaEnabledEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
}
```

Similar for `UserMfaDisabledEvent`, `UserMfaLockoutClearedEvent`, `UserMfaBackupCodesRegeneratedEvent` (some may already exist from Tasks 10-12).

**Step 2: Publish events from controllers**

- `ConfirmEnrollment` -> publish `UserMfaEnabledEvent`
- `DisableMfa` -> publish `UserMfaDisabledEvent`
- `AdminDisableMfa` -> publish `UserMfaDisabledEvent` with admin context
- `AdminClearLockout` -> publish `UserMfaLockoutClearedEvent`
- `RegenerateBackupCodes` -> publish `UserMfaBackupCodesRegeneratedEvent`

**Step 3: Run tests, commit**

```bash
git commit -m "feat(identity): publish MFA audit events via Wolverine"
```

---

## Phase 9: E2E Tests

### Task 17: Add OtpNet and MFA Test Helpers

**Files:**
- Modify: `tests/Wallow.E2E.Tests/Wallow.E2E.Tests.csproj` (add OtpNet package)
- Create: `tests/Wallow.E2E.Tests/Infrastructure/TotpHelper.cs`
- Modify: `tests/Wallow.E2E.Tests/Infrastructure/TestUserFactory.cs` (add MFA-enabled user creation)

**Step 1: Add OtpNet package**

```bash
dotnet add tests/Wallow.E2E.Tests package OtpNet
```

**Step 2: Create TotpHelper**

```csharp
using OtpNet;

public static class TotpHelper
{
    public static string GenerateCode(string base32Secret)
    {
        byte[] secretBytes = Base32Encoding.ToBytes(base32Secret);
        Totp totp = new(secretBytes, step: 30, totpSize: 6);
        return totp.ComputeTotp();
    }
}
```

**Step 3: Extend TestUserFactory**

Add `CreateWithMfaAsync(string apiBaseUrl, string mailpitBaseUrl)`:
- Creates a verified user
- Calls enroll/totp endpoint to get secret
- Calls enroll/confirm with a valid TOTP code
- Returns `TestUser` with `TotpSecret` property for code generation in tests

Add `CreateInMfaRequiredOrgAsync(string apiBaseUrl, string mailpitBaseUrl)`:
- Creates a user in an org with `RequireMfa = true`
- Returns `TestUser` without MFA enabled (for enrollment testing)

**Step 4: Commit**

```bash
git commit -m "test(e2e): add OtpNet TOTP helper and MFA test user factory"
```

---

### Task 18: Create MfaChallengePage Page Object

**Files:**
- Create: `tests/Wallow.E2E.Tests/PageObjects/MfaChallengePage.cs`

**Step 1: Implement page object**

```csharp
public sealed class MfaChallengePage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public MfaChallengePage(IPage page, string baseUrl) { ... }

    public async Task NavigateAsync(string? returnUrl = null) { ... }
    public async Task<bool> IsLoadedAsync() { ... }
    public async Task FillCodeAsync(string code) { ... }
    public async Task SubmitAsync() { ... }
    public async Task ToggleBackupCodeAsync() { ... }
    public async Task FillBackupCodeAsync(string code) { ... }
    public async Task<string?> GetErrorAsync() { ... }
    public async Task<bool> IsSuccessAsync() { ... }
}
```

Uses `data-testid` selectors: `mfa-challenge-code`, `mfa-challenge-submit`, `mfa-challenge-backup-toggle`, `mfa-challenge-backup-code`, `mfa-challenge-error`, `mfa-challenge-success`.

**Step 2: Commit**

```bash
git commit -m "test(e2e): add MfaChallengePage page object"
```

---

### Task 19: Update MfaEnrollPage Page Object

**Files:**
- Modify: `tests/Wallow.E2E.Tests/PageObjects/MfaEnrollPage.cs`

**Step 1: Add methods**

- `GetSecretTextAsync()` -> reads the TOTP secret text from `data-testid="mfa-enroll-secret"`
- Update `GetQrCodeAsync()` -> verify QR code container is visible via `data-testid="mfa-enroll-qr-code"`
- Update `SubmitAsync()` to optionally wait for success instead of error (for valid code submissions)
- Add `WaitForBackupCodesAsync()` -> waits for backup codes to appear after successful enrollment

**Step 2: Commit**

```bash
git commit -m "test(e2e): update MfaEnrollPage with secret extraction and QR check"
```

---

### Task 20: Create SettingsMfaSection Page Object

**Files:**
- Create: `tests/Wallow.E2E.Tests/PageObjects/SettingsMfaSection.cs`

**Step 1: Implement**

```csharp
public sealed class SettingsMfaSection
{
    public async Task<string> GetMfaStatusAsync() { ... }
    public async Task<int> GetBackupCodeCountAsync() { ... }
    public async Task ClickEnableAsync() { ... }
    public async Task ClickDisableAsync() { ... }
    public async Task ClickRegenerateCodesAsync() { ... }
    public async Task ConfirmPasswordAsync(string password) { ... }
}
```

**Step 2: Commit**

```bash
git commit -m "test(e2e): add SettingsMfaSection page object"
```

---

### Task 21: Write MFA E2E Test Scenarios

**Files:**
- Create: `tests/Wallow.E2E.Tests/Flows/MfaFlowTests.cs`
- Modify: `tests/Wallow.E2E.Tests/Flows/DashboardFlowTests.cs:16` (unskip and update)

**Step 1: Create MfaFlowTests**

```csharp
[Trait("Category", "E2E")]
public sealed class MfaFlowTests : E2ETestBase
{
    // Test 1: MFA enrollment during login (org requires MFA, expired grace)
    [Fact]
    public async Task MfaEnrollment_OrgRequiresMfa_CompletesEnrollmentAndLandsOnDashboard() { ... }

    // Test 2: MFA challenge during login (user has MFA enabled)
    [Fact]
    public async Task MfaChallenge_UserHasMfa_VerifiesCodeAndLandsOnDashboard() { ... }

    // Test 3: MFA challenge with backup code
    [Fact]
    public async Task MfaChallenge_WithBackupCode_VerifiesAndLandsOnDashboard() { ... }

    // Test 4: Grace period flow (banner shown, can access dashboard)
    [Fact]
    public async Task GracePeriod_ShowsBannerAndAllowsDashboardAccess() { ... }

    // Test 5: Disable MFA from settings
    [Fact]
    public async Task DisableMfa_FromSettings_DisablesMfaSuccessfully() { ... }

    // Test 6: Regenerate backup codes from settings
    [Fact]
    public async Task RegenerateBackupCodes_FromSettings_ShowsNewCodes() { ... }
}
```

Each test uses `TestUserFactory` to create appropriate test users, `TotpHelper` to generate valid codes, and page objects for interaction.

**Step 2: Update DashboardFlowTests**

Remove `[Fact(Skip = "...")]` from `MfaEnrollmentFlow_ShowsSetupPageAndAcceptsCode`. Update to use the new partial-auth flow and `TotpHelper` for valid code generation.

**Step 3: Run E2E tests**

Run: `./scripts/run-tests.sh`

**Step 4: Commit**

```bash
git commit -m "test(e2e): add comprehensive MFA flow E2E tests"
```

---

## Phase 10: Final Verification

### Task 22: Run Full Test Suite and Fix Any Failures

**Step 1: Run all unit and integration tests**

```bash
./scripts/run-tests.sh
```

Fix any failures from contract changes (updated AuthResponse, removed MfaLoginVerifyRequest, etc.).

**Step 2: Run architecture tests**

Ensure new files follow Clean Architecture rules. Check that:
- `MfaPartialAuthService` is in Infrastructure layer
- `IMfaPartialAuthService` is in Application layer
- Events are in `Shared.Contracts`
- No cross-module references

**Step 3: Verify existing tests still pass**

Pay special attention to:
- `AccountControllerTests` - login flow changed significantly
- `MfaControllerTests` - endpoints restructured
- `MfaChallengeTests` - component tests for updated page
- `MfaEnrollTests` - component tests for updated page
- `AuthApiClientTests` - simplified client methods

**Step 4: Commit any fixes**

```bash
git commit -m "fix: resolve test failures from MFA overhaul"
```

---

## Dependency Order

```
Phase 1 (Tasks 1-2): Partial-Auth Infrastructure
    |
Phase 2 (Tasks 3-4): Fix Login Flow - depends on Phase 1
    |
Phase 3 (Tasks 5-8): Fix Auth App - depends on Phase 2
    |
Phase 4 (Task 9): External Login MFA - depends on Phase 1
    | (can run parallel with Phase 3)
Phase 5 (Tasks 10-12): Management Endpoints - depends on Phase 2
    | (can run parallel with Phases 3-4)
Phase 6 (Task 13): Cleanup - depends on Phases 2-5
    |
Phase 7 (Tasks 14-15): Web App UI - depends on Phase 5
    |
Phase 8 (Task 16): Audit Events - depends on Phase 5
    | (can run parallel with Phase 7)
Phase 9 (Tasks 17-21): E2E Tests - depends on all previous phases
    |
Phase 10 (Task 22): Final Verification - last
```

## Parallelization Opportunities

These phases can be worked on concurrently:
- **Phase 3 + Phase 4** (Auth app fixes + External login) - independent code paths
- **Phase 5 + Phase 3** (Management endpoints + Auth app fixes) - different files
- **Phase 7 + Phase 8** (Web UI + Audit events) - independent
