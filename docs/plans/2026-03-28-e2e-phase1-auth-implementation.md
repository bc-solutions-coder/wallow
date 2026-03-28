# Phase 1: Auth — Login, Registration, Logout Implementation Plan

## 1. Objective

Achieve comprehensive E2E coverage for core auth flows: login, registration, logout, password reset, and protected route redirects. Establish the `[Trait("E2EGroup", "Auth")]` regression group as the foundation for all subsequent phases.

## 2. Prerequisites

- All infrastructure running (Postgres, Valkey, GarageHQ, Mailpit)
- All three apps running (API on 5001, Auth on 5002, Web on 5003) or Docker test stack via `./scripts/run-e2e.sh`
- Existing E2E tests pass: `./scripts/run-tests.sh e2e`

## 3. Step-by-Step Plan

### 3.1 Add data-testid Attributes to Blazor Components

#### Logout.razor (`src/Wallow.Auth/Components/Pages/Logout.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| Card title (signed out state) | `logout-title` | The heading text ("Signed out" or "Sign out") |
| "You have been successfully signed out." paragraph | `logout-message` | Success message after logout |
| "Sign out" confirm link/button | `logout-confirm` | The confirm sign-out action link |
| "Return to application" link | `logout-return-link` | Post-logout redirect link |
| "Back to sign in" footer link | `logout-back-to-login` | Footer navigation link |

#### Login.razor (`src/Wallow.Auth/Components/Pages/Login.razor`) — Tab Buttons

| Element | data-testid | Description |
|---------|-------------|-------------|
| Password tab button | `login-tab-password` | Tab switcher for password login |
| Magic Link tab button | `login-tab-magic-link` | Tab switcher for magic link login |
| OTP tab button | `login-tab-otp` | Tab switcher for OTP login |

#### DashboardLayout.razor (`src/Wallow.Web/Components/Layout/DashboardLayout.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| "Sign Out" sidebar link | `dashboard-signout` | Logout link in sidebar footer |

### 3.2 Retag Existing Tests with `[Trait("E2EGroup", "Auth")]`

All four tests are in `tests/Wallow.E2E.Tests/Flows/AuthFlowTests.cs`:

| Test Method | Trait to Add |
|-------------|-------------|
| `RegistrationAndLoginFlow_CompletesSuccessfully` | `[Trait("E2EGroup", "Auth")]` |
| `LoginToDashboard_RedirectsAuthenticatedUser` | `[Trait("E2EGroup", "Auth")]` |
| `ForgotPasswordFlow_SendsResetEmailViaMailpit` | `[Trait("E2EGroup", "Auth")]` |
| `AuthRedirectFlow_ProtectedRouteRedirectsToLoginThenReturns` | `[Trait("E2EGroup", "Auth")]` |

### 3.3 Page Object Changes

#### LoginPage (`tests/Wallow.E2E.Tests/PageObjects/LoginPage.cs`)

Add the following methods:

```csharp
/// <summary>
/// Returns the visible error message text, or null if no error is displayed.
/// Waits briefly for Blazor to re-render after form submission.
/// </summary>
public async Task<string?> GetVisibleErrorAsync(int timeoutMs = 5_000)

/// <summary>
/// Returns true if the login error element is currently visible.
/// </summary>
public async Task<bool> HasErrorAsync()
```

**Implementation notes:**
- `GetVisibleErrorAsync` waits up to `timeoutMs` for `[data-testid='login-error']` to become visible, then returns `InnerTextAsync()`. Returns `null` if timeout expires without the element appearing.
- `HasErrorAsync` is a non-waiting check that returns `IsVisibleAsync()` on the error locator.
- The existing `GetErrorMessageAsync()` method already works but does not wait; `GetVisibleErrorAsync` adds a wait for Blazor re-render.

#### RegisterPage (`tests/Wallow.E2E.Tests/PageObjects/RegisterPage.cs`)

Add the following methods:

```csharp
/// <summary>
/// Waits for validation errors to appear after submission, then returns them.
/// </summary>
public async Task<IReadOnlyList<string>> GetVisibleValidationErrorsAsync(int timeoutMs = 5_000)

/// <summary>
/// Returns the single error message text (for server-side errors like duplicate email).
/// Waits for the error element to appear.
/// </summary>
public async Task<string?> GetVisibleErrorAsync(int timeoutMs = 5_000)

/// <summary>
/// Returns true if any validation error or server error is displayed.
/// </summary>
public async Task<bool> HasErrorAsync()
```

**Implementation notes:**
- `GetVisibleValidationErrorsAsync` waits for `[data-testid='register-error']` to appear, then delegates to existing `GetValidationErrorsAsync()`.
- `GetVisibleErrorAsync` waits for `[data-testid='register-error']` and returns its text. Useful for server-side errors (e.g., "An account with this email already exists.").
- `HasErrorAsync` checks visibility of `[data-testid='register-error']` without waiting.

### 3.4 New Page Objects

#### LogoutPage (`tests/Wallow.E2E.Tests/PageObjects/LogoutPage.cs`)

```csharp
public sealed class LogoutPage
{
    public LogoutPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to the logout page directly.
    /// </summary>
    public async Task NavigateAsync()

    /// <summary>
    /// Returns true if the logout page is loaded (title element visible).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Clicks the "Sign out" confirmation link/button.
    /// </summary>
    public async Task ConfirmLogoutAsync()

    /// <summary>
    /// Returns the page title text ("Signed out" or "Sign out").
    /// </summary>
    public async Task<string> GetTitleTextAsync()

    /// <summary>
    /// Returns the message text displayed after logout.
    /// </summary>
    public async Task<string?> GetMessageAsync()

    /// <summary>
    /// Returns true if the "Return to application" link is visible.
    /// </summary>
    public async Task<bool> HasReturnLinkAsync()

    /// <summary>
    /// Clicks "Back to sign in" footer link.
    /// </summary>
    public async Task ClickBackToLoginAsync()

    /// <summary>
    /// Returns true if the page shows the signed-out confirmation state.
    /// </summary>
    public async Task<bool> IsSignedOutAsync()
}
```

**Selectors used:**
- `[data-testid='logout-title']` — page heading
- `[data-testid='logout-message']` — success message
- `[data-testid='logout-confirm']` — sign-out action
- `[data-testid='logout-return-link']` — return to app link
- `[data-testid='logout-back-to-login']` — back to login link

#### ForgotPasswordPage (`tests/Wallow.E2E.Tests/PageObjects/ForgotPasswordPage.cs`)

```csharp
public sealed class ForgotPasswordPage
{
    public ForgotPasswordPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to the forgot password page.
    /// </summary>
    public async Task NavigateAsync()

    /// <summary>
    /// Returns true if the forgot password form is loaded.
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Fills the email field.
    /// </summary>
    public async Task FillEmailAsync(string email)

    /// <summary>
    /// Submits the forgot password form.
    /// </summary>
    public async Task SubmitAsync()

    /// <summary>
    /// Returns the success/info message after submission, or null.
    /// </summary>
    public async Task<string?> GetSuccessMessageAsync()

    /// <summary>
    /// Returns the error message if displayed, or null.
    /// </summary>
    public async Task<string?> GetErrorMessageAsync()
}
```

**Selectors used:**
- `[data-testid='forgot-password-email']` — already exists in Login.razor's forgot password link target
- `[data-testid='forgot-password-submit']` — already exists
- `[data-testid='forgot-password-success']` — needs adding to ForgotPassword.razor if not present
- `[data-testid='forgot-password-error']` — needs adding to ForgotPassword.razor if not present

### 3.5 New Test Methods

All new tests go in `tests/Wallow.E2E.Tests/Flows/AuthFlowTests.cs` and must have `[Trait("E2EGroup", "Auth")]`.

#### Test 1: `Login_WithInvalidCredentials_ShowsErrorMessage`

- **Type:** Negative
- **Base class:** `E2ETestBase` (unauthenticated)
- **Flow:**
  1. Create a verified user via `TestUserFactory.CreateAsync`
  2. Navigate to login page via `LoginPage.NavigateAsync()`
  3. Fill correct email, wrong password ("WrongP@ss123")
  4. Submit via `LoginPage.SubmitAsync()`
  5. Assert `LoginPage.GetVisibleErrorAsync()` returns non-null text containing "Invalid email or password"
- **Page objects:** `LoginPage`
- **Assertions:**
  - Error message is visible and contains expected text
  - Page URL still contains `/login` (no redirect)

#### Test 2: `Login_WithUnverifiedEmail_ShowsVerificationError`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Register a user via API but do NOT verify email (call `RegisterUserAsync` only, skip `VerifyEmailAsync` — requires new `TestUserFactory.CreateUnverifiedAsync` or inline registration)
  2. Navigate to login page
  3. Fill email and password
  4. Submit
  5. Assert error message contains "verify your email" or user is redirected to `/verify-email`
- **Page objects:** `LoginPage`
- **Assertions:**
  - Error message visible with "verify" text, OR URL redirects to verify-email page
- **Note:** This test needs `TestUserFactory.CreateUnverifiedAsync` (introduced in Phase 2). For Phase 1, create the user inline by posting to the register API endpoint without clicking the verification link.

#### Test 3: `Registration_WithDuplicateEmail_ShowsError`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create a verified user via `TestUserFactory.CreateAsync`
  2. Navigate to register page via `RegisterPage.NavigateAsync()`
  3. Fill form with the same email, a valid password, check terms/privacy
  4. Submit via `RegisterPage.SubmitAsync()`
  5. Wait for Blazor re-render (`Page.WaitForLoadStateAsync(LoadState.NetworkIdle)`)
  6. Assert `RegisterPage.GetVisibleErrorAsync()` returns text containing "already exists"
- **Page objects:** `RegisterPage`
- **Assertions:**
  - Error text contains "already exists"
  - URL still on `/register`

#### Test 4: `Registration_WithWeakPassword_ShowsValidationError`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to register page
  2. Fill form with a new email, weak password ("123"), matching confirm password, check terms/privacy
  3. Submit
  4. Assert validation error about password requirements
- **Page objects:** `RegisterPage`
- **Assertions:**
  - Error message visible containing "password" (server returns "password_too_weak" -> "Password does not meet the minimum requirements.")
  - URL still on `/register`

#### Test 5: `Registration_WithMissingRequiredFields_ShowsValidationErrors`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to register page
  2. Submit without filling any fields (leave email empty)
  3. Assert error about required email
  4. Fill email only, submit — assert error about password
  5. Fill email and password, but don't check terms — assert error about terms
- **Page objects:** `RegisterPage`
- **Assertions:**
  - Each submission shows the appropriate error: "Please enter your email address.", "Please enter a password.", "You must agree to the Terms of Service."

#### Test 6: `Logout_FromDashboard_RedirectsToLoginWithSessionCleared`

- **Type:** Happy path
- **Base class:** `E2ETestBase` (manages its own login)
- **Flow:**
  1. Create a verified user and log in via OIDC flow (same as `LoginToDashboard_RedirectsAuthenticatedUser`)
  2. Wait for dashboard to load
  3. Click the "Sign Out" link in the sidebar via `[data-testid='dashboard-signout']`
  4. Wait for URL to contain `/logout` or `/signout`
  5. If on the logout confirmation page (`LogoutPage`), click confirm
  6. Assert redirected to login page or signed-out state
  7. Attempt to navigate to dashboard again — assert redirect back to login (session cleared)
- **Page objects:** `LoginPage`, `DashboardPage`, `LogoutPage`
- **Assertions:**
  - After logout, user is shown signed-out state or redirected to login
  - Subsequent dashboard access redirects to login

#### Test 7: `Logout_ConfirmationPage_ConfirmsAndRedirects`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate directly to `/logout` page
  2. Assert `LogoutPage.IsLoadedAsync()` returns true
  3. Assert title text is "Sign out" (pre-confirmation state)
  4. Click confirm via `LogoutPage.ConfirmLogoutAsync()`
  5. Wait for navigation
  6. Assert page shows signed-out state or redirects to login
  7. Click "Back to sign in" and verify navigation to login page
- **Page objects:** `LogoutPage`, `LoginPage`
- **Assertions:**
  - Confirmation page loads correctly
  - After confirm, signed-out state shown
  - "Back to sign in" navigates to login

## 4. Regression Verification Commands

```bash
# Run only Auth group tests
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth"

# Run full E2E suite to ensure no regressions
./scripts/run-e2e.sh
```

## 5. Complete File List

### Modified Files

| File | Changes |
|------|---------|
| `src/Wallow.Auth/Components/Pages/Logout.razor` | Add 5 `data-testid` attributes |
| `src/Wallow.Auth/Components/Pages/Login.razor` | Add 3 `data-testid` attributes to tab buttons |
| `src/Wallow.Web/Components/Layout/DashboardLayout.razor` | Add `data-testid="dashboard-signout"` to sign-out link |
| `tests/Wallow.E2E.Tests/Flows/AuthFlowTests.cs` | Add `[Trait("E2EGroup", "Auth")]` to 4 existing tests, add 7 new tests |
| `tests/Wallow.E2E.Tests/PageObjects/LoginPage.cs` | Add `GetVisibleErrorAsync`, `HasErrorAsync` methods |
| `tests/Wallow.E2E.Tests/PageObjects/RegisterPage.cs` | Add `GetVisibleValidationErrorsAsync`, `GetVisibleErrorAsync`, `HasErrorAsync` methods |

### Created Files

| File | Description |
|------|-------------|
| `tests/Wallow.E2E.Tests/PageObjects/LogoutPage.cs` | New page object for logout flow |
| `tests/Wallow.E2E.Tests/PageObjects/ForgotPasswordPage.cs` | New page object for forgot password flow |

## 6. Implementation Sequence

1. **Add data-testid attributes** to Logout.razor, Login.razor tabs, and DashboardLayout.razor
2. **Create `LogoutPage`** page object
3. **Create `ForgotPasswordPage`** page object
4. **Add error helper methods** to `LoginPage` and `RegisterPage`
5. **Retag existing 4 tests** with `[Trait("E2EGroup", "Auth")]`
6. **Write new negative tests** (tests 1-5: invalid credentials, unverified email, duplicate email, weak password, missing fields)
7. **Write new happy path tests** (tests 6-7: logout from dashboard, logout confirmation page)
8. **Run regression:** `./scripts/run-e2e.sh` — all 11 Auth tests must pass
