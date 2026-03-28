# Phase 4: AltAuth — Magic Link and OTP Login Implementation Plan

## 1. Objective

Add E2E coverage for the Magic Link and OTP login methods. These are the two alternative authentication tabs on the login page that currently have zero test coverage. Establishes the `[Trait("E2EGroup", "AltAuth")]` regression group with 8 new tests.

## 2. Prerequisites

- Phase 1-3 regression gates pass:
  ```bash
  dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification|E2EGroup=MFA"
  ```
- Infrastructure and apps running

### 2.1 Critical Backend Prerequisite: Notification Handlers

The Magic Link and OTP flows depend on notification handlers that send emails. These handlers may not exist yet:

- **`MagicLinkNotificationHandler`** — listens for `MagicLinkRequestedEvent` (from `Wallow.Shared.Contracts`) in the Notifications module and sends an email containing the magic link URL.
- **`OtpCodeNotificationHandler`** — listens for `OtpCodeRequestedEvent` and sends an email containing the 6-digit OTP code.

**Required backend work (if handlers don't exist):**
1. Add `MagicLinkRequestedEvent` and `OtpCodeRequestedEvent` to `Wallow.Shared.Contracts.Events`
2. Add Wolverine handlers in `Wallow.Notifications.Infrastructure`:
   - `MagicLinkNotificationHandler` — sends email with magic link
   - `OtpCodeNotificationHandler` — sends email with OTP code
3. Add email templates for magic link and OTP emails
4. Verify the Identity module's `AuthService.SendMagicLinkAsync` and `AuthService.SendOtpAsync` publish the corresponding events

**Verify before starting E2E tests:** Manually test that sending a magic link / OTP via the login page results in an email appearing in Mailpit.

### 2.2 Backend Prerequisite: Login.razor Magic Link Token Handling

Login.razor needs to handle the `magicLinkToken` query parameter:
- When a user clicks the magic link in their email, it should navigate to `/login?magicLinkToken=<token>` (or a dedicated endpoint)
- Login.razor's `OnInitializedAsync` should check for this parameter and call `AuthClient.VerifyMagicLinkAsync(token)`
- On success, call `HandleSuccessfulAuth(result)` to complete the OIDC flow

**Alternatively**, the magic link may point to an API endpoint that sets cookies and redirects. Check the existing `AuthService` implementation.

### 2.3 Backend Prerequisite: AccountController Sign-In Ticket

The magic link verify and OTP verify API endpoints must return a `signInTicket` in the `AuthResponse` so the Login.razor `HandleSuccessfulAuth` method can exchange it for an auth cookie (same pattern as password login).

## 3. Step-by-Step Plan

### 3.1 Add data-testid Attributes to Login.razor

#### Login.razor (`src/Wallow.Auth/Components/Pages/Login.razor`) — All Tabs and Fields

| Element | data-testid | Description |
|---------|-------------|-------------|
| Password tab button | `login-tab-password` | Tab switcher for password login |
| Magic Link tab button | `login-tab-magic-link` | Tab switcher for magic link login |
| OTP tab button | `login-tab-otp` | Tab switcher for OTP login |
| Magic Link email input | `login-magic-link-email` | Email field in magic link form |
| Magic Link "Send link" button | `login-magic-link-submit` | Submit button for magic link form |
| Magic Link success alert | `login-magic-link-sent` | "Check your email for a magic link" alert |
| OTP email input | `login-otp-email` | Email field in OTP request form |
| OTP "Send code" button | `login-otp-send` | Submit button for OTP request form |
| OTP code input | `login-otp-code` | 6-digit code input field |
| OTP "Verify code" button | `login-otp-verify` | Submit button for OTP verification form |
| "You are now signed in" alert | `login-signed-in` | Success state after direct login |

**Total new data-testid attributes on Login.razor: 11**

### 3.2 docker-compose.test.yml Configuration

Ensure the test Docker stack has appropriate timing configuration:
- Magic link and OTP tokens typically have short expiration times (5-15 minutes). Verify the test environment uses expiration times that are long enough for E2E test execution.
- If magic link/OTP token expiry is configurable via `appsettings`, set it to at least 5 minutes in the test environment config.

### 3.3 Page Object Changes

#### LoginPage (`tests/Wallow.E2E.Tests/PageObjects/LoginPage.cs`)

Add tab switching methods:

```csharp
/// <summary>
/// Switches to the Magic Link tab on the login page.
/// </summary>
public async Task SwitchToMagicLinkTabAsync()

/// <summary>
/// Switches to the OTP tab on the login page.
/// </summary>
public async Task SwitchToOtpTabAsync()

/// <summary>
/// Switches to the Password tab on the login page.
/// </summary>
public async Task SwitchToPasswordTabAsync()
```

**Selectors:** `[data-testid='login-tab-password']`, `[data-testid='login-tab-magic-link']`, `[data-testid='login-tab-otp']`

Add magic link form methods:

```csharp
/// <summary>
/// Fills the email field on the Magic Link tab.
/// </summary>
public async Task FillMagicLinkEmailAsync(string email)

/// <summary>
/// Clicks the "Send link" button on the Magic Link tab.
/// </summary>
public async Task SubmitMagicLinkAsync()

/// <summary>
/// Returns true if the "magic link sent" confirmation message is visible.
/// </summary>
public async Task<bool> IsMagicLinkSentAsync(int timeoutMs = 5_000)
```

**Selectors:** `[data-testid='login-magic-link-email']`, `[data-testid='login-magic-link-submit']`, `[data-testid='login-magic-link-sent']`

Add OTP form methods:

```csharp
/// <summary>
/// Fills the email field on the OTP tab (request phase).
/// </summary>
public async Task FillOtpEmailAsync(string email)

/// <summary>
/// Clicks the "Send code" button on the OTP tab.
/// </summary>
public async Task SubmitOtpRequestAsync()

/// <summary>
/// Fills the OTP code field (verification phase).
/// </summary>
public async Task FillOtpCodeAsync(string code)

/// <summary>
/// Clicks the "Verify code" button on the OTP tab.
/// </summary>
public async Task SubmitOtpVerifyAsync()

/// <summary>
/// Returns true if the OTP code input field is visible (OTP has been sent).
/// </summary>
public async Task<bool> IsOtpCodeFieldVisibleAsync(int timeoutMs = 5_000)
```

**Selectors:** `[data-testid='login-otp-email']`, `[data-testid='login-otp-send']`, `[data-testid='login-otp-code']`, `[data-testid='login-otp-verify']`

### 3.4 Infrastructure Changes

#### MailpitHelper (`tests/Wallow.E2E.Tests/Infrastructure/MailpitHelper.cs`)

Add new method:

```csharp
/// <summary>
/// Searches Mailpit for an email to the given recipient containing a keyword,
/// then extracts a numeric code (e.g., 6-digit OTP) from the email body.
/// </summary>
/// <param name="mailpitBaseUrl">Mailpit API base URL</param>
/// <param name="recipientEmail">Email address to search for</param>
/// <param name="keyword">Keyword to find the relevant email (e.g., "verification code", "one-time")</param>
/// <param name="codeLength">Expected length of the numeric code (default: 6)</param>
/// <param name="maxRetries">Max polling attempts</param>
/// <param name="pollIntervalSeconds">Seconds between polls</param>
/// <returns>The extracted code string, or empty string if not found</returns>
public static async Task<string> SearchForCodeAsync(
    string mailpitBaseUrl,
    string recipientEmail,
    string keyword,
    int codeLength = 6,
    int maxRetries = 10,
    int pollIntervalSeconds = 2)
```

**Implementation:**
- Uses same Mailpit polling pattern as `SearchForLinkAsync`
- After finding the email body, uses a regex like `\b\d{codeLength}\b` to extract the numeric code
- Returns the first match, or empty string if not found

### 3.5 Timing Concerns

- **Email delivery delay:** Magic link and OTP emails go through Wolverine event handling -> Notifications module -> SMTP (Mailpit). Allow up to 20 seconds of polling via `MailpitHelper`.
- **Token expiry:** If magic link or OTP tokens expire in < 2 minutes, the test may fail intermittently. Ensure test environment config sets expiry >= 5 minutes.
- **OIDC redirect chain:** After magic link or OTP verification, the user goes through the same OIDC redirect chain as password login. Use the same 30-second timeout for dashboard landing.

### 3.6 New Test Methods

All tests go in a new file `tests/Wallow.E2E.Tests/Flows/AltAuthFlowTests.cs` with `[Trait("E2EGroup", "AltAuth")]`.

#### Test 1: `MagicLink_HappyPath_RequestThenClickLinkThenDashboard`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create verified user via `TestUserFactory.CreateAsync`
  2. Navigate to login via OIDC: `Page.GotoAsync($"{WebBaseUrl}/authentication/login")`
  3. Wait for login page to load
  4. Switch to magic link tab via `LoginPage.SwitchToMagicLinkTabAsync()`
  5. Fill email via `LoginPage.FillMagicLinkEmailAsync(user.Email)`
  6. Submit via `LoginPage.SubmitMagicLinkAsync()`
  7. Assert `LoginPage.IsMagicLinkSentAsync()` returns true
  8. Search Mailpit for magic link: `MailpitHelper.SearchForLinkAsync(mailpitBaseUrl, user.Email, "magic")`
  9. Navigate browser to the magic link
  10. Wait for OIDC redirect chain to reach dashboard
  11. Assert URL contains `/dashboard`
- **Page objects:** `LoginPage`, `DashboardPage`
- **Assertions:**
  - "Magic link sent" confirmation displayed
  - Email received in Mailpit
  - After clicking link, user lands on dashboard

#### Test 2: `MagicLink_UnregisteredEmail_ShowsGenericMessage`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to login, switch to magic link tab
  2. Fill email with `"nonexistent-{guid}@test.local"`
  3. Submit
  4. Assert error message is shown OR a generic "check your email" message (no info leak about whether email exists)
- **Page objects:** `LoginPage`
- **Assertions:**
  - No specific "user not found" leak (or if the API returns `user_not_found`, the UI maps to "No account found with that email." — verify this is the intended behavior or if it should be generic)

#### Test 3: `MagicLink_ExpiredLink_ShowsError`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate browser to a crafted magic link URL with an invalid/expired token
  2. Assert error page or error message is displayed
- **Page objects:** `LoginPage` (or error page)
- **Assertions:**
  - Error state shown (invalid/expired token)

#### Test 4: `MagicLink_PasswordLoginBeforeClickingLink_GracefulHandling`

- **Type:** Edge case
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create verified user
  2. Request magic link (submit on magic link tab)
  3. Before clicking the link, login via password in a new context or same page (switch to password tab)
  4. Then click the magic link
  5. Assert either: already logged in (graceful), or token invalid (graceful error), or second login succeeds
- **Page objects:** `LoginPage`, `DashboardPage`
- **Assertions:**
  - No crash or unhandled error
  - User ends up authenticated or sees a clear message

#### Test 5: `Otp_HappyPath_RequestCodeThenEnterCodeThenDashboard`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create verified user via `TestUserFactory.CreateAsync`
  2. Navigate to login via OIDC flow
  3. Switch to OTP tab via `LoginPage.SwitchToOtpTabAsync()`
  4. Fill email via `LoginPage.FillOtpEmailAsync(user.Email)`
  5. Submit via `LoginPage.SubmitOtpRequestAsync()`
  6. Assert `LoginPage.IsOtpCodeFieldVisibleAsync()` returns true (OTP sent, code input appeared)
  7. Search Mailpit for OTP code: `MailpitHelper.SearchForCodeAsync(mailpitBaseUrl, user.Email, "code")`
  8. Fill code via `LoginPage.FillOtpCodeAsync(code)`
  9. Submit via `LoginPage.SubmitOtpVerifyAsync()`
  10. Wait for OIDC redirect chain to reach dashboard
  11. Assert URL contains `/dashboard`
- **Page objects:** `LoginPage`, `DashboardPage`
- **Assertions:**
  - OTP code field appears after sending
  - Code extracted from Mailpit email
  - After verification, user lands on dashboard

#### Test 6: `Otp_WrongCode_ShowsErrorAndCanRetry`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create verified user
  2. Navigate to login, switch to OTP tab, fill email, send code
  3. Wait for code input to appear
  4. Fill code with `"000000"` (wrong code)
  5. Submit verify
  6. Assert error message contains "Invalid or expired code"
  7. Assert code input is still visible (can retry)
- **Page objects:** `LoginPage`
- **Assertions:**
  - Error message visible with "Invalid or expired code"
  - Code input still present for retry

#### Test 7: `Otp_UnregisteredEmail_ShowsGenericMessage`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to login, switch to OTP tab
  2. Fill email with `"nonexistent-{guid}@test.local"`
  3. Submit
  4. Assert error or generic message (no info leak)
- **Page objects:** `LoginPage`
- **Assertions:**
  - Appropriate message shown without leaking whether email exists

#### Test 8: `Otp_ExpiredCode_ShowsErrorMessage`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create verified user
  2. Request OTP code via the API or UI
  3. Wait for code to expire (or use a previously-sent code after requesting a new one, which invalidates the old one)
  4. Enter the expired/invalidated code
  5. Assert error message about invalid/expired code
- **Page objects:** `LoginPage`
- **Assertions:**
  - Error message with "Invalid or expired code"
- **Note:** Testing true expiration may require waiting (impractical). Alternative: request OTP twice, use the first code (now invalidated by the second request).

## 4. Regression Verification Commands

```bash
# Phase 1-3 gate
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification|E2EGroup=MFA"

# Phase 4 tests
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=AltAuth"

# All phases
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification|E2EGroup=MFA|E2EGroup=AltAuth"

# Full E2E
./scripts/run-e2e.sh
```

## 5. Complete File List

### Modified Files

| File | Changes |
|------|---------|
| `src/Wallow.Auth/Components/Pages/Login.razor` | Add 11 `data-testid` attributes to tab buttons, magic link fields, OTP fields, signed-in alert |
| `tests/Wallow.E2E.Tests/PageObjects/LoginPage.cs` | Add tab switching (3), magic link form (3), OTP form (5) methods — 11 total new methods |
| `tests/Wallow.E2E.Tests/Infrastructure/MailpitHelper.cs` | Add `SearchForCodeAsync` method |
| `docker/docker-compose.test.yml` | Verify/add magic link and OTP token expiry config if needed |

### Created Files

| File | Description |
|------|-------------|
| `tests/Wallow.E2E.Tests/Flows/AltAuthFlowTests.cs` | 8 new test methods |

### Backend Prerequisites (may need creation)

| File | Description |
|------|-------------|
| `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Handlers/MagicLinkNotificationHandler.cs` | Wolverine handler for magic link emails |
| `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Handlers/OtpCodeNotificationHandler.cs` | Wolverine handler for OTP code emails |

## 6. Implementation Sequence

1. **Verify backend prerequisites** — ensure magic link and OTP email delivery works end-to-end (manually test in dev environment)
2. **Add data-testid attributes** to Login.razor (11 new attributes)
3. **Add `SearchForCodeAsync`** to MailpitHelper
4. **Add LoginPage methods** — tab switching (3), magic link form (3), OTP form (5)
5. **Write 4 magic link tests** (tests 1-4: happy path, unregistered email, expired link, password-before-link)
6. **Write 4 OTP tests** (tests 5-8: happy path, wrong code, unregistered email, expired code)
7. **Run regression:** all previous phases pass, then `./scripts/run-e2e.sh` — all ~34 tests pass
