# Phase 3: MFA — Harden MFA with Negative Cases Implementation Plan

## 1. Objective

Add negative/error E2E tests for MFA enrollment, challenge, and settings management. Retag all 7 existing MFA tests with `[Trait("E2EGroup", "MFA")]`. Add 6 new negative tests to ensure error handling works correctly. Establishes the `[Trait("E2EGroup", "MFA")]` regression group.

## 2. Prerequisites

- Phase 1 and Phase 2 regression gates pass:
  ```bash
  dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification"
  ```
- Infrastructure and apps running

## 3. Step-by-Step Plan

### 3.1 Add data-testid Attributes to Blazor Components

#### MfaSettingsSection.razor (`src/Wallow.Web/Components/Shared/MfaSettingsSection.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| Error message paragraph (`_errorMessage`) | `settings-mfa-error` | Error text shown after failed disable/regenerate |

Currently the error message is rendered as:
```html
<p class="text-sm text-red-600 mt-2">@_errorMessage</p>
```
Add `data-testid="settings-mfa-error"` to this `<p>` element.

#### MfaEnroll.razor (`src/Wallow.Auth/Components/Pages/MfaEnroll.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| "Cancel" footer link | `mfa-enroll-cancel` | Cancel link at bottom of enrollment page |

Currently the cancel link is:
```html
<a href="/" class="text-sm text-muted-foreground hover:text-primary underline-offset-4 hover:underline">Cancel</a>
```
Add `data-testid="mfa-enroll-cancel"` to this `<a>` element.

### 3.2 Retag Existing Tests with `[Trait("E2EGroup", "MFA")]`

Tests in `tests/Wallow.E2E.Tests/Flows/MfaFlowTests.cs`:

| Test Method | Trait to Add |
|-------------|-------------|
| `EnrollmentDuringLogin_ShowsSetupPageAndActivatesMfa` | `[Trait("E2EGroup", "MFA")]` |
| `ChallengeDuringLogin_AcceptsValidTotpCode` | `[Trait("E2EGroup", "MFA")]` |
| `ChallengeDuringLogin_AcceptsBackupCode` | `[Trait("E2EGroup", "MFA")]` |
| `GracePeriodFlow_AllowsLoginWithoutEnrollmentRedirect` | `[Trait("E2EGroup", "MFA")]` |
| `DisableMfa_FromSettingsPage` | `[Trait("E2EGroup", "MFA")]` |
| `RegenerateBackupCodes_FromSettingsPage` | `[Trait("E2EGroup", "MFA")]` |

Test in `tests/Wallow.E2E.Tests/Flows/DashboardFlowTests.cs`:

| Test Method | Trait to Add |
|-------------|-------------|
| `MfaEnrollmentFlow_ShowsSetupPageAndAcceptsCode` | `[Trait("E2EGroup", "MFA")]` |

### 3.3 Page Object Changes

#### MfaChallengePage (`tests/Wallow.E2E.Tests/PageObjects/MfaChallengePage.cs`)

Add the following methods:

```csharp
/// <summary>
/// Waits for the error element to become visible and returns its text.
/// Returns null if no error appears within the timeout.
/// </summary>
public async Task<string?> GetVisibleErrorAsync(int timeoutMs = 5_000)

/// <summary>
/// Returns true if the page URL still contains /mfa/challenge (user was NOT redirected).
/// Useful after submitting an invalid code to verify the user stays on the page.
/// </summary>
public async Task<bool> IsStillOnChallengePageAsync()
```

**Implementation notes:**
- `GetVisibleErrorAsync` waits for `[data-testid='mfa-challenge-error']` to become visible, then returns `InnerTextAsync()`. Returns null on timeout.
- `IsStillOnChallengePageAsync` simply checks `_page.Url.Contains("/mfa/challenge", StringComparison.OrdinalIgnoreCase)`.
- The existing `GetErrorAsync()` method checks visibility but does not wait. `GetVisibleErrorAsync` adds a wait for Blazor re-render after submit.

#### MfaEnrollPage (`tests/Wallow.E2E.Tests/PageObjects/MfaEnrollPage.cs`)

Add the following methods:

```csharp
/// <summary>
/// Submits the enrollment form without throwing on error (unlike SubmitAsync which throws).
/// Returns true if backup codes appeared, false if error appeared.
/// </summary>
public async Task<bool> TrySubmitAsync(int timeoutMs = 45_000)

/// <summary>
/// Waits for the error element to become visible and returns its text.
/// Returns null if no error appears within the timeout.
/// </summary>
public async Task<string?> GetVisibleErrorAsync(int timeoutMs = 5_000)

/// <summary>
/// Clicks the "Cancel" link at the bottom of the enrollment page.
/// </summary>
public async Task ClickCancelAsync()
```

**Implementation notes:**
- `TrySubmitAsync` clicks `[data-testid='mfa-enroll-submit']`, waits for either `[data-testid='mfa-enroll-backup-codes']` or `[data-testid='mfa-enroll-error']`, returns true for backup codes, false for error. Does NOT throw on error (unlike existing `SubmitAsync`).
- `GetVisibleErrorAsync` waits for `[data-testid='mfa-enroll-error']` to become visible, returns text. Returns null on timeout.
- `ClickCancelAsync` clicks `[data-testid='mfa-enroll-cancel']`.

#### SettingsMfaSection (`tests/Wallow.E2E.Tests/PageObjects/SettingsMfaSection.cs`)

Add the following method:

```csharp
/// <summary>
/// Fills password and clicks confirm, but expects an error instead of success.
/// Waits for the error element to appear rather than the confirm dialog to disappear.
/// Returns the error message text.
/// </summary>
public async Task<string?> ConfirmPasswordExpectingErrorAsync(string password, int timeoutMs = 10_000)
```

**Implementation notes:**
- Fills `[data-testid='settings-mfa-confirm-password']` with the provided password
- Clicks `[data-testid='settings-mfa-confirm-submit']`
- Waits for `[data-testid='settings-mfa-error']` to become visible (instead of waiting for confirm dialog to disappear)
- Returns the error text from `[data-testid='settings-mfa-error']`

### 3.4 Invalid Input Strategy Table

| Test Scenario | Invalid Input | Expected Error Text | data-testid Checked |
|---------------|---------------|--------------------|--------------------|
| MFA challenge — invalid TOTP | `"000000"` (known-bad 6-digit code) | "Invalid verification code. Please try again." | `mfa-challenge-error` |
| MFA challenge — invalid backup code | `"INVALID-BACKUP-CODE"` | "Invalid backup code. Please try again." | `mfa-challenge-error` |
| MFA enrollment — invalid code | `"999999"` (wrong 6-digit code) | "Invalid verification code. Please try again." | `mfa-enroll-error` |
| MFA enrollment — cancel | N/A (click cancel link) | N/A (navigates away) | URL check |
| Disable MFA — wrong password | `"WrongP@ss123"` | "Failed to disable MFA. Please check your password and try again." | `settings-mfa-error` |
| Regenerate backup codes — wrong password | `"WrongP@ss123"` | "Failed to regenerate backup codes. Please check your password and try again." | `settings-mfa-error` |

### 3.5 New Test Methods

New negative tests go in `tests/Wallow.E2E.Tests/Flows/MfaFlowTests.cs` with `[Trait("E2EGroup", "MFA")]`.

#### Test 1: `ChallengeDuringLogin_InvalidTotpCode_ShowsErrorAndStaysOnPage`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create MFA-enabled user via `TestUserFactory.CreateWithMfaAsync`
  2. Navigate to login, fill credentials, submit
  3. Wait for URL to contain `mfa/challenge`
  4. Assert `MfaChallengePage.IsLoadedAsync()` returns true
  5. Fill code with `"000000"` (invalid TOTP)
  6. Click submit via `MfaChallengePage.SubmitAsync()`
  7. Assert `MfaChallengePage.GetVisibleErrorAsync()` returns text containing "Invalid verification code"
  8. Assert `MfaChallengePage.IsStillOnChallengePageAsync()` returns true
- **Page objects:** `LoginPage`, `MfaChallengePage`
- **Assertions:**
  - Error message visible with "Invalid verification code"
  - User stays on challenge page (not redirected)

#### Test 2: `ChallengeDuringLogin_InvalidBackupCode_ShowsError`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create MFA-enabled user via `TestUserFactory.CreateWithMfaAsync`
  2. Login, land on MFA challenge page
  3. Toggle to backup code mode via `MfaChallengePage.ToggleBackupCodeAsync()`
  4. Fill backup code with `"INVALID-BACKUP-CODE"`
  5. Submit
  6. Assert `MfaChallengePage.GetVisibleErrorAsync()` returns text containing "Invalid backup code"
  7. Assert `MfaChallengePage.IsStillOnChallengePageAsync()` returns true
- **Page objects:** `LoginPage`, `MfaChallengePage`
- **Assertions:**
  - Error message visible with "Invalid backup code"
  - User stays on challenge page

#### Test 3: `EnrollmentDuringLogin_InvalidVerificationCode_ShowsErrorAndCanRetry`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create user in MFA-required org via `TestUserFactory.CreateInMfaRequiredOrgAsync`
  2. Login, land on MFA enrollment page
  3. Click begin setup (or wait for auto-start)
  4. Assert secret is displayed
  5. Fill code with `"999999"` (invalid)
  6. Call `MfaEnrollPage.TrySubmitAsync()` — returns false
  7. Assert `MfaEnrollPage.GetVisibleErrorAsync()` returns text containing "Invalid verification code"
  8. Verify the form is still visible (code input still present) — user can retry
- **Page objects:** `LoginPage`, `MfaEnrollPage`
- **Assertions:**
  - Error message visible with "Invalid verification code"
  - Code input still present (can retry)
  - Secret still displayed

#### Test 4: `EnrollmentCancel_ReturnsWithoutEnablingMfa`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create user in MFA-required org via `TestUserFactory.CreateInMfaRequiredOrgAsync`
  2. Login, land on MFA enrollment page
  3. Assert `MfaEnrollPage.IsLoadedAsync()` returns true
  4. Click cancel via `MfaEnrollPage.ClickCancelAsync()`
  5. Wait for navigation away from `/mfa/enroll`
  6. Assert URL no longer contains `/mfa/enroll`
- **Page objects:** `LoginPage`, `MfaEnrollPage`
- **Assertions:**
  - Navigated away from enrollment page
  - Cancel action does not enable MFA

#### Test 5: `DisableMfa_WrongPassword_ShowsErrorAndMfaStaysEnabled`

- **Type:** Negative
- **Base class:** `E2ETestBase` (manages its own login through MFA challenge)
- **Flow:**
  1. Create MFA-enabled user via `TestUserFactory.CreateWithMfaAsync`
  2. Login through full MFA challenge (same pattern as existing `DisableMfa_FromSettingsPage`)
  3. Navigate to settings via `SettingsMfaSection.NavigateAsync()`
  4. Assert MFA status shows "Enabled"
  5. Click disable via `SettingsMfaSection.ClickDisableAsync()`
  6. Enter wrong password via `SettingsMfaSection.ConfirmPasswordExpectingErrorAsync("WrongP@ss123")`
  7. Assert error message contains "Failed to disable MFA"
  8. Assert MFA status still shows "Enabled"
- **Page objects:** `LoginPage`, `MfaChallengePage`, `SettingsMfaSection`
- **Assertions:**
  - Error message visible with "Failed to disable MFA"
  - MFA status unchanged — still "Enabled"

#### Test 6: `RegenerateBackupCodes_WrongPassword_ShowsErrorAndCodesUnchanged`

- **Type:** Negative
- **Base class:** `E2ETestBase` (manages its own login through MFA challenge)
- **Flow:**
  1. Create MFA-enabled user via `TestUserFactory.CreateWithMfaAsync`
  2. Login through full MFA challenge
  3. Navigate to settings via `SettingsMfaSection.NavigateAsync()`
  4. Note current backup code count via `SettingsMfaSection.GetBackupCodeCountAsync()`
  5. Click regenerate via `SettingsMfaSection.ClickRegenerateCodesAsync()`
  6. Enter wrong password via `SettingsMfaSection.ConfirmPasswordExpectingErrorAsync("WrongP@ss123")`
  7. Assert error message contains "Failed to regenerate backup codes"
  8. Assert backup code count unchanged
- **Page objects:** `LoginPage`, `MfaChallengePage`, `SettingsMfaSection`
- **Assertions:**
  - Error message visible with "Failed to regenerate backup codes"
  - Backup code count same as before the failed attempt

## 4. Regression Verification Commands

```bash
# Phase 1+2 gate
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification"

# Phase 3 tests
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=MFA"

# All phases so far
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification|E2EGroup=MFA"

# Full E2E
./scripts/run-e2e.sh
```

## 5. Complete File List

### Modified Files

| File | Changes |
|------|---------|
| `src/Wallow.Web/Components/Shared/MfaSettingsSection.razor` | Add `data-testid="settings-mfa-error"` to error paragraph |
| `src/Wallow.Auth/Components/Pages/MfaEnroll.razor` | Add `data-testid="mfa-enroll-cancel"` to cancel link |
| `tests/Wallow.E2E.Tests/Flows/MfaFlowTests.cs` | Add `[Trait("E2EGroup", "MFA")]` to 6 existing tests, add 6 new tests |
| `tests/Wallow.E2E.Tests/Flows/DashboardFlowTests.cs` | Add `[Trait("E2EGroup", "MFA")]` to `MfaEnrollmentFlow_ShowsSetupPageAndAcceptsCode` |
| `tests/Wallow.E2E.Tests/PageObjects/MfaChallengePage.cs` | Add `GetVisibleErrorAsync`, `IsStillOnChallengePageAsync` methods |
| `tests/Wallow.E2E.Tests/PageObjects/MfaEnrollPage.cs` | Add `TrySubmitAsync`, `GetVisibleErrorAsync`, `ClickCancelAsync` methods |
| `tests/Wallow.E2E.Tests/PageObjects/SettingsMfaSection.cs` | Add `ConfirmPasswordExpectingErrorAsync` method |

### Created Files

None — all changes are modifications to existing files.

## 6. Implementation Sequence

1. **Add data-testid attributes** to MfaSettingsSection.razor (`settings-mfa-error`) and MfaEnroll.razor (`mfa-enroll-cancel`)
2. **Add page object methods:**
   - `MfaChallengePage`: `GetVisibleErrorAsync`, `IsStillOnChallengePageAsync`
   - `MfaEnrollPage`: `TrySubmitAsync`, `GetVisibleErrorAsync`, `ClickCancelAsync`
   - `SettingsMfaSection`: `ConfirmPasswordExpectingErrorAsync`
3. **Retag 7 existing tests** across `MfaFlowTests.cs` (6) and `DashboardFlowTests.cs` (1)
4. **Write 4 enrollment/challenge negative tests** (tests 1-4: invalid TOTP, invalid backup, invalid enrollment code, cancel)
5. **Write 2 settings negative tests** (tests 5-6: disable wrong password, regenerate wrong password)
6. **Run regression:** all previous phases pass, then `./scripts/run-e2e.sh` — all ~26 tests pass
