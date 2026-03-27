# MFA Overhaul Design

## Problem

The MFA implementation has several critical bugs that prevent it from working end-to-end, plus missing features for a complete MFA story.

### Critical Bugs (P0)

1. **Query parameter mismatch** — `Login.razor` navigates to `/mfa/challenge?token=...` but `MfaChallenge.razor` binds to `[SupplyParameterFromQuery] ChallengeToken`. The challenge token is never received.
2. **Contract mismatch on mfa/verify** — Auth app sends `{ ChallengeToken, Code, UseBackupCode }` but API expects `MfaLoginVerifyRequest(Email, ChallengeToken, Code, RememberMe, UseBackupCode)`. Missing `Email` and `RememberMe` causes binding failure.
3. **MfaChallenge ignores sign-in ticket** — After successful MFA verify, the API returns `{ succeeded: true, signInTicket }` but MfaChallenge just redirects to `ReturnUrl` without extracting the ticket or doing the exchange. User is never authenticated.
4. **MFA enrollment requires `[Authorize]`** — `MfaController` has `[Authorize]` on the class, but users redirected to enrollment aren't authenticated yet. All enrollment API calls return 401.

### Security Gaps (P1)

5. **External login bypasses MFA** — `ExternalLoginCallback` passes `bypassTwoFactor: true`. Users with MFA enabled can bypass it entirely by logging in via OAuth.
6. **Tenant MFA enforcement missing** — `OrganizationSettings.RequireMfa` exists but the login flow only checks `user.MfaEnabled`, never org policy.

### Missing Features (P2-P3)

7. Missing `data-testid` attributes on MfaChallenge page.
8. No MFA disable endpoint (domain method `DisableMfa()` exists but no controller).
9. No QR code rendering in enrollment (API returns `qrUri` but page shows secret as text).
10. `IMfaChallengeHandler` extension point is dead code — never called.
11. No backup code regeneration.
12. No Web app dashboard MFA management page.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Pre-auth MFA access | Partial-auth session (cookie) | All endpoints have proper authentication. Avoids anonymous endpoints and untrackable calls. |
| Post-MFA sign-in completion | Server-side session upgrade | Partial cookie upgrades to full cookie on MFA verify. No ticket round-trips through query strings. |
| Dashboard MFA management | Full self-service + admin controls | Users manage their own MFA; admins can force-disable, clear lockouts, view compliance. |
| External login MFA | Always enforce if enabled | If user has MFA enabled OR org requires it, MFA is enforced regardless of login method. |
| QR code rendering | Client-side via qrcode.js | API already returns `otpauth://` URI. QR rendering is a UI concern. JS interop in Blazor. |
| E2E TOTP generation | OtpNet NuGet in test project | Tests read secret from enrollment page, generate valid TOTP codes at runtime. No test-only API endpoints. |
| IMfaChallengeHandler | Remove | Dead code. Forks can override `IMfaService` instead. |

## Architecture

### Partial-Auth Session

The core design change introduces a **partial authentication session** that gates MFA endpoints without granting full application access.

**Flow:**

1. User provides valid credentials (password, magic link, OTP, or external OAuth).
2. If MFA is required, the API creates a **partial-auth cookie** (`Identity.MfaPartial`) containing:
   - User ID
   - Email
   - Authentication method used
   - RememberMe preference
   - Expiry: 5 minutes
3. This cookie grants access only to endpoints decorated with `[AuthorizeMfaPartial]`.
4. On successful MFA verification or enrollment confirmation, the partial session upgrades to a full ASP.NET Identity auth cookie. The partial cookie is deleted.
5. If the partial cookie expires, the user must re-authenticate from scratch.

**Endpoint access matrix:**

| Endpoint | Partial Cookie | Full Cookie | Purpose |
|----------|---------------|-------------|---------|
| `POST /mfa/enroll/totp` | Yes | Yes | Generate TOTP secret |
| `POST /mfa/enroll/confirm` | Yes | Yes | Confirm enrollment |
| `POST /auth/mfa/verify` | Yes | No | Verify TOTP/backup during login |
| `GET /mfa/status` | Yes | Yes | Check MFA state |
| `POST /mfa/disable` | No | Yes + password | Disable own MFA |
| `POST /mfa/backup-codes/regenerate` | No | Yes + password | Regenerate backup codes |
| `POST /mfa/admin/{userId}/disable` | No | Yes + admin perm | Force-disable user MFA |
| `POST /mfa/admin/{userId}/clear-lockout` | No | Yes + admin perm | Clear lockout counter |
| `GET /mfa/admin/compliance` | No | Yes + admin perm | Org enrollment overview |

If MFA is not enabled on the user and their org does not require it, the login flow works exactly as today — valid credentials produce a full auth cookie immediately. No partial session involved.

### Login Flow

```
Password/OTP/MagicLink valid?
  ├─ NO → return 401 (unchanged)
  └─ YES
      ├─ MFA enabled + not exempt?
      │   └─ Set partial-auth cookie → return { mfaRequired: true }
      ├─ Org requires MFA + not enrolled + outside grace?
      │   └─ Set partial-auth cookie → return { mfaEnrollmentRequired: true }
      ├─ Org requires MFA + not enrolled + inside grace?
      │   └─ Set full cookie → return { succeeded: true, mfaEnrollmentRequired: true, graceDeadline }
      └─ No MFA needed
          └─ Set full cookie → return { succeeded: true }
```

Key changes from current implementation:
- No `signInTicket` for MFA flows — the partial cookie replaces it.
- No `challengeToken` in the JSON response — challenge is managed server-side, tied to the partial session.
- `mfa/verify` reads user identity from the partial cookie. No `Email` or `ChallengeToken` in the request body.
- The contract mismatch (missing Email/RememberMe) is eliminated entirely.

### External Login MFA Enforcement

Current flow sets `bypassTwoFactor: true` and signs in immediately. New flow:

```
OAuth callback → GetExternalLoginInfoAsync()
  ├─ New user (registration) → Create account, sign in
  │   └─ If org requires MFA → full cookie + enrollment banner (grace period starts)
  └─ Existing user
      ├─ MFA enabled or org requires MFA?
      │   └─ Set partial-auth cookie → redirect to /mfa/challenge?returnUrl=...
      └─ No MFA needed
          └─ Set full cookie → redirect to returnUrl (unchanged)
```

- New users from external login get the grace period — MFA enrollment is not forced during first OAuth sign-up.
- Existing users with MFA are always challenged, even via external login.
- The partial cookie stores external login info so the user does not re-OAuth after MFA verify.
- The upgrade from partial to full cookie calls `SignInAsync` with external login provider info preserved.

### Tenant MFA Enforcement

The login flow (both password and external) checks `OrganizationSettings`:

```csharp
bool orgRequiresMfa = await organizationService.GetMfaRequirementAsync(user, ct);

if (user.MfaEnabled || orgRequiresMfa)
{
    if (!user.MfaEnabled && orgRequiresMfa)
    {
        // User hasn't enrolled but org requires it — check grace period
        if (user.MfaGraceDeadline is null)
        {
            user.SetMfaGraceDeadline(timeProvider.GetUtcNow().AddDays(orgSettings.MfaGracePeriodDays));
        }

        if (user.MfaGraceDeadline > timeProvider.GetUtcNow())
            → full cookie + enrollment banner
        else
            → partial cookie + redirect to /mfa/enroll
    }
    else
        → partial cookie + redirect to /mfa/challenge
}
```

### MFA Verify & Enrollment Endpoints

**Simplified verify** — `POST /api/v1/identity/auth/mfa/verify`:

```
[AuthorizeMfaPartial]
Request: { code: string, useBackupCode: bool }
```

- Reads user ID from partial-auth cookie.
- Validates TOTP code against encrypted secret (or backup code).
- On success: upgrades partial cookie to full auth cookie, returns `{ succeeded: true }`.
- On failure: increments Redis failure counter, returns error.
- Lockout after 5 failures (configurable). Partial cookie stays valid but verify is blocked.

**Simplified enrollment** — `POST /api/v1/identity/mfa/enroll/totp`:

```
[AuthorizeMfaPartial] + [Authorize] (dual — works with either)
Request: (none)
Response: { secret, qrUri }
```

- Accessible during login flow (partial cookie) and from dashboard (full cookie).
- `POST /mfa/enroll/confirm` on success during login: enables MFA, upgrades to full cookie, returns backup codes.

**Consolidated request type:**

```
Before: MfaLoginVerifyRequest(Email, ChallengeToken, Code, RememberMe, UseBackupCode)
After:  MfaVerifyRequest(Code, UseBackupCode)
```

The existing `MfaVerifyRequest` in `MfaController` already has this shape. Consolidate to one shared type.

### New Endpoints

| Endpoint | Auth | Purpose |
|----------|------|---------|
| `GET /mfa/status` | Partial or Full | MFA enabled, method, backup code count |
| `POST /mfa/disable` | Full + password | Disable MFA for current user |
| `POST /mfa/backup-codes/regenerate` | Full + password | New backup codes, invalidate old |
| `POST /mfa/admin/{userId}/disable` | Full + ManageUsers perm | Admin force-disable |
| `POST /mfa/admin/{userId}/clear-lockout` | Full + ManageUsers perm | Clear failed attempt counter |
| `GET /mfa/admin/compliance` | Full + ManageUsers perm | Org enrollment status overview |

Password confirmation for sensitive operations (disable, regenerate) prevents session hijacking from completing MFA changes.

## UI Changes

### Auth App (Wallow.Auth)

**MfaChallenge.razor — Fix & simplify:**
- Remove `ChallengeToken` query parameter binding. Only accept `ReturnUrl`.
- Code input + verify calls `POST /mfa/verify` with `{ code, useBackupCode }`.
- On success: redirect to `ReturnUrl` with `forceLoad: true` (cookie already set by API).
- Add `data-testid` attributes: `mfa-challenge-code`, `mfa-challenge-submit`, `mfa-challenge-error`, `mfa-challenge-backup-toggle`, `mfa-challenge-backup-code`.

**MfaEnroll.razor — Fix & simplify:**
- Remove direct `HttpClientFactory` usage. Use `IAuthApiClient` consistently.
- API calls authenticated via partial cookie (during login) or full cookie (from dashboard).
- Add QR code rendering via JS interop with `qrcode.js`.
- On confirm during login: redirect to `ReturnUrl` (cookie upgraded by API).
- On confirm from dashboard: show success, navigate back to settings.
- Add `data-testid="mfa-enroll-qr-code"` for QR container.

**Login.razor — Simplify MFA handling:**
- `HandleSuccessfulAuth` navigates to `/mfa/challenge?returnUrl=...` or `/mfa/enroll?returnUrl=...`.
- No token or method params in URL.
- Remove `MfaChallengeToken` and `MfaMethod` from response handling.

### Web App (Wallow.Web) — New MFA Settings Section

Add MFA section to `/dashboard/settings`:
- Shows: MFA status (enabled/disabled), method, remaining backup code count.
- Actions: Enable MFA (navigates to enrollment), Disable MFA (password confirmation modal), Regenerate backup codes (password confirmation modal).
- Calls new API endpoints via an `IMfaApiClient` service in Web app.

### Admin MFA Management

New section in org admin settings (alongside existing `RequireMfa` toggle):
- Table showing users with MFA status: enrolled, pending (grace period), non-compliant.
- Action buttons: force-disable, clear lockout.
- Password confirmation required for force-disable.

## Audit Events

Wolverine in-memory bus events via `Shared.Contracts`:

| Event | Trigger |
|-------|---------|
| `UserMfaEnabledEvent` | User completes enrollment |
| `UserMfaDisabledEvent` | User or admin disables MFA |
| `UserMfaLockoutClearedEvent` | Admin clears lockout |
| `UserMfaBackupCodesRegeneratedEvent` | Backup codes regenerated |

Cross-module consumers (e.g., Notifications) can react to these events.

## Cleanup

- **Remove `IMfaChallengeHandler`** and `NoOpMfaChallengeHandler` — dead code, never called. Forks override `IMfaService` instead.
- **Remove `challengeToken` from `AuthResponse`** — no longer passed in JSON.
- **Remove `MfaChallengeToken` and `MfaMethod`** from `AuthResponse`.
- **Remove `signInTicket` handling** from MfaChallenge page.
- **Remove `method` query param** construction in Login.razor.
- **Consolidate `ValidateChallengeAsync` overloads** on `IMfaService` — one method that reads user from partial session.
- **Standardize on Redis** for challenge state — remove distributed cache challenge mechanism.
- **Simplify `MfaLoginVerifyRequest`** to `MfaVerifyRequest(Code, UseBackupCode)`.

## E2E Testing

### Dependencies

- `OtpNet` NuGet package added to `Wallow.E2E.Tests`.

### Page Objects

| Page Object | Target | Key Methods |
|-------------|--------|-------------|
| `MfaChallengePage` (new) | `/mfa/challenge` | `FillCodeAsync()`, `SubmitAsync()`, `ToggleBackupCodeAsync()`, `GetErrorAsync()` |
| `MfaEnrollPage` (update) | `/mfa/enroll` | Existing + `GetQrCodeAsync()`, `GetSecretTextAsync()` |
| `SettingsMfaSection` (new) | `/dashboard/settings` | `GetMfaStatusAsync()`, `ClickEnableAsync()`, `ClickDisableAsync()`, `ClickRegenerateCodesAsync()` |

### Test Scenarios

1. **MFA enrollment during login** — Login as user with org MFA required + expired grace → redirected to `/mfa/enroll` → complete TOTP setup → backup codes shown → lands on dashboard.
2. **MFA challenge during login** — Login as MFA-enabled user → redirected to `/mfa/challenge` → enter TOTP code → lands on dashboard.
3. **MFA challenge with backup code** — Toggle to backup code mode, use one from enrollment.
4. **External login with MFA** — OAuth login → redirected to `/mfa/challenge` → verify → dashboard.
5. **Grace period flow** — Login with org MFA required + active grace → enrollment banner → can access dashboard → enroll from settings.
6. **Disable MFA from settings** — Settings → disable MFA → password confirmation → MFA disabled.
7. **Regenerate backup codes** — Settings → regenerate → password confirmation → new codes shown.
8. **Admin force-disable** — Admin → org settings → find user → force-disable MFA.

### Test Infrastructure

- `TestUserFactory.CreateWithMfaAsync()` — creates verified user with MFA enrolled (sets TOTP secret directly, returns secret for code generation).
- `TestUserFactory.CreateInMfaRequiredOrgAsync()` — creates user in org with `RequireMfa = true`.
- Unskip `DashboardFlowTests.MfaEnrollmentFlow_ShowsSetupPageAndAcceptsCode()`.
