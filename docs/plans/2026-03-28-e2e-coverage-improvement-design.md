# E2E Test Coverage Improvement Design

## Problem

The E2E suite covers ~40% of Wallow.Auth pages and ~67% of Wallow.Web pages. Two entire login methods (Magic Link, OTP) have zero coverage. No negative or error-handling tests exist. Organization detail, invitation flow, and several settings pages are untested.

## Goals

- Reach comprehensive E2E coverage for both Wallow.Auth and Wallow.Web
- Add negative/error tests alongside every happy path
- Build incrementally so each phase acts as a regression foundation for the next
- Use composable xUnit trait groups so teams can run targeted regression

## Trait-Based Regression Strategy

Every E2E test gets one or more `[Trait("E2EGroup", "<name>")]` tags. Tests that span features get multiple tags.

**Available groups:**

| Group | Scope |
|-------|-------|
| `Auth` | Login, registration, logout, password reset, protected route redirects |
| `EmailVerification` | Browser-based email verification flow |
| `MFA` | Enrollment, challenge, grace period, settings management |
| `AltAuth` | Magic Link and OTP login methods |
| `Organizations` | Org list, detail, members, bound clients, client registration |
| `Invitations` | Invitation landing, accept/decline, token expiry |
| `AppRegistration` | OAuth app registration, branding, scopes, app list |
| `Settings` | Profile display, landing page, error/terms/privacy/accept-terms pages |

**Running targeted regression:**

```bash
# Single group
dotnet test --filter "E2EGroup=Auth"

# Multiple groups
dotnet test --filter "E2EGroup=Auth|E2EGroup=MFA"

# All E2E
./scripts/run-tests.sh e2e
```

**Cross-cutting example:** A test that invites a user and verifies they appear in the org member list gets both `[Trait("E2EGroup", "Organizations")]` and `[Trait("E2EGroup", "Invitations")]`.

## Phase 1: `Auth` — Login, Registration, Logout

**Regression gate:** None (first phase).

**Retag existing tests:**

| Test | Group |
|------|-------|
| `RegistrationAndLoginFlow_CompletesSuccessfully` | `Auth` |
| `LoginToDashboard_RedirectsAuthenticatedUser` | `Auth` |
| `ForgotPasswordFlow_SendsResetEmailViaMailpit` | `Auth` |
| `AuthRedirectFlow_ProtectedRouteRedirectsToLoginThenReturns` | `Auth` |

**New tests:**

| Test | Type |
|------|------|
| Login with invalid credentials → error message displayed | Negative |
| Login with unverified email → appropriate error/redirect | Negative |
| Registration with duplicate email → error message | Negative |
| Registration with weak password → validation errors shown | Negative |
| Registration with missing required fields → validation errors | Negative |
| Logout from dashboard → redirected to login, session cleared | Happy path |
| Logout confirmation page → confirm and redirect behavior | Happy path |

**Page object changes:**
- `LoginPage`: add error message assertion helpers
- `RegisterPage`: add validation error helpers
- New `LogoutPage`: navigate, confirm, assert redirect

## Phase 2: `EmailVerification` — Browser-Based Email Verification

**Regression gate:** Run `E2EGroup=Auth` — all pass before starting.

**New tests:**

| Test | Type |
|------|------|
| Register → verify-email page → Mailpit link → confirm page → success | Happy path |
| Visit verify-email/confirm with expired/invalid token → error state | Negative |
| Visit verify-email/confirm with already-verified email → handled | Negative |
| Unverified user attempts login → redirected to verify-email prompt | Negative |

**Page objects:**
- New `VerifyEmailPage`: assert prompt content, navigation
- New `VerifyEmailConfirmPage`: assert success/error states, continue button

**Infrastructure:** Reuses existing `MailpitHelper.SearchForLinkAsync`.

## Phase 3: `MFA` — Harden MFA with Negative Cases

**Regression gate:** Run `E2EGroup=Auth,EmailVerification` — all pass before starting.

**Retag existing tests:**

| Test | Group |
|------|-------|
| `EnrollmentDuringLogin_ShowsSetupPageAndActivatesMfa` | `MFA` |
| `ChallengeDuringLogin_AcceptsValidTotpCode` | `MFA` |
| `ChallengeDuringLogin_AcceptsBackupCode` | `MFA` |
| `GracePeriodFlow_AllowsLoginWithoutEnrollmentRedirect` | `MFA` |
| `DisableMfa_FromSettingsPage` | `MFA` |
| `RegenerateBackupCodes_FromSettingsPage` | `MFA` |
| `MfaEnrollmentFlow_ShowsSetupPageAndAcceptsCode` | `MFA` |

**New tests:**

| Test | Type |
|------|------|
| MFA challenge with invalid TOTP code → error, stays on page | Negative |
| MFA challenge with already-used backup code → error | Negative |
| MFA enrollment with invalid verification code → error, can retry | Negative |
| MFA enrollment cancel → returns without enabling MFA | Negative |
| Disable MFA with wrong password → error, MFA stays enabled | Negative |
| Regenerate backup codes with wrong password → error, codes unchanged | Negative |

**Page object changes:**
- `MfaChallengePage`: add error assertion helpers
- `MfaEnrollPage`: add error/cancel assertion helpers
- `SettingsMfaSection`: add error state helpers for password confirmation

## Phase 4: `AltAuth` — Magic Link and OTP Login

**Regression gate:** Run `E2EGroup=Auth,EmailVerification,MFA` — all pass before starting.

**New tests:**

| Test | Type |
|------|------|
| Magic Link: request → Mailpit → click link → dashboard | Happy path |
| Magic Link: unregistered email → no info leak message | Negative |
| Magic Link: expired/invalid link → error page | Negative |
| Magic Link: login via password before clicking link → graceful | Edge case |
| OTP: request code → Mailpit → enter code → dashboard | Happy path |
| OTP: enter wrong code → error, can retry | Negative |
| OTP: unregistered email → no info leak message | Negative |
| OTP: enter expired code → error message | Negative |

**Page object changes:**
- `LoginPage`: add `SwitchToMagicLinkTabAsync()`, `SwitchToOtpTabAsync()`, magic link email field, OTP email/code fields, send button helpers

**Infrastructure:**
- Add `MailpitHelper.SearchForCodeAsync(mailpitBaseUrl, email, keyword)` — extracts a code (not a link) from email body, for OTP flow

## Phase 5: `Organizations` + `Invitations` — Org Management and Invitation Flow

**Regression gate:** Run `E2EGroup=Auth,EmailVerification,MFA,AltAuth` — all pass before starting.

### `Organizations` tests

| Test | Type |
|------|------|
| Navigate to org detail → member list displays | Happy path |
| Org detail → bound clients list displays (or empty state) | Happy path |
| Org detail → register client → appears in bound clients | Happy path |
| Org detail → register client with invalid input → errors | Negative |
| Organizations list → reflects newly created org | Happy path |

### `Invitations` tests

| Test | Type |
|------|------|
| Valid invitation (unauthenticated) → shows org name, options | Happy path |
| Valid invitation (authenticated) → accept → dashboard | Happy path |
| Expired invitation token → error state | Negative |
| Invalid/already-used invitation token → error state | Negative |
| Decline invitation → appropriate redirect | Negative |

### Cross-tagged tests (`Organizations` + `Invitations`)

| Test | Type |
|------|------|
| Invite user → accept → user appears in org member list | Happy path |
| Invite to MFA-required org → accept → forced into MFA enrollment | Happy path |

**Page objects:**
- New `OrganizationDetailPage`: member list, bound clients, client registration form, validation helpers
- New `InvitationLandingPage`: assert org name, accept/decline/create-account buttons, error states
- Update `OrganizationPage`: row click/navigation helpers

**Infrastructure:**
- New `InvitationHelper` or extend `TestUserFactory`: create invitations via API for test setup

## Phase 6: `AppRegistration` — Branding, Scopes, List Verification

**Regression gate:** Run `E2EGroup=Auth,EmailVerification,MFA,AltAuth,Organizations,Invitations` — all pass before starting.

**Retag existing tests:**

| Test | Group |
|------|-------|
| `AppRegistrationFlow_RegistersNewApplication` | `AppRegistration` |

**New tests:**

| Test | Type |
|------|------|
| Register app with branding (company name, tagline, logo) → success | Happy path |
| Register app with scope toggles → success, scopes applied | Happy path |
| Register app with invalid input (empty name, bad URI) → errors | Negative |
| Register app → apps list → new app appears with correct details | Happy path |
| Register confidential app → client secret displayed, copy works | Happy path |
| Register public app → no client secret shown | Happy path |

**Page object changes:**
- `AppRegistrationPage`: add scope toggle helpers, branding field helpers, logo upload
- `DashboardPage` (apps list): add row lookup/verification by app name

## Phase 7: `Settings` — Profile, Landing, and Remaining Pages

**Regression gate:** Run all groups — full suite must pass before starting.

**New tests:**

| Test | Type |
|------|------|
| Settings → profile displays name, email, roles correctly | Happy path |
| Settings → MFA section coexists with profile section | Happy path |
| Landing page → authenticated user redirected to dashboard | Happy path |
| Landing page → unauthenticated user sees landing or login | Happy path |
| Error page → correct message per `reason` param | Happy path |
| Terms page → content renders, back link works | Happy path |
| Privacy page → content renders, back link works | Happy path |
| AcceptTerms → check both boxes → submit → proceeds | Happy path |
| AcceptTerms → submit without checking boxes → validation | Negative |

**Page objects:**
- New `SettingsProfileSection`: assert name, email, roles
- New `AcceptTermsPage`: checkbox helpers, submit, validation
- New `ErrorPage`: assert message by reason param
- New `TermsPage`, `PrivacyPage`: content and navigation helpers

**Regression gate:** Run full suite. This is the final phase — all ~62 tests pass.

## Test Count Summary

| Phase | Group(s) | New | Retagged | Running Total |
|-------|----------|-----|----------|---------------|
| 1 | `Auth` | 7 | 4 | ~16 |
| 2 | `EmailVerification` | 4 | 0 | ~20 |
| 3 | `MFA` | 6 | 7 | ~26 |
| 4 | `AltAuth` | 8 | 0 | ~34 |
| 5 | `Organizations`, `Invitations` | 12 | 0 | ~46 |
| 6 | `AppRegistration` | 6 | 1 | ~52 |
| 7 | `Settings` | 9 | 0 | ~61 |

## Conventions

- Every test gets at least one `[Trait("E2EGroup", "<name>")]`
- Happy path and negative tests always written together — never leave a flow with only the happy path
- Each phase starts by running all previous groups as a regression gate
- Page objects own all selector knowledge — tests never reference `data-testid` directly
- New page objects follow existing patterns: async methods, `IsLoadedAsync()`, error assertion helpers
- `data-testid` attributes must be added to any Blazor component that lacks them before writing tests against it
