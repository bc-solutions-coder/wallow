# Phase 2: Email Verification — Browser-Based Email Verification Implementation Plan

## 1. Objective

Add E2E coverage for the browser-based email verification flow. Tests cover the happy path (register, see verify-email prompt, click Mailpit link, confirm), plus negative cases for invalid tokens, already-verified emails, and unverified login attempts. Establishes the `[Trait("E2EGroup", "EmailVerification")]` regression group.

## 2. Prerequisites

- Phase 1 regression gate passes: `dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth"` — all pass
- Infrastructure and apps running

## 3. Step-by-Step Plan

### 3.1 Add data-testid Attributes to Blazor Components

#### VerifyEmail.razor (`src/Wallow.Auth/Components/Pages/VerifyEmail.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| Card title ("Check your email") | `verify-email-title` | Page heading |
| Description text ("We've sent a verification link...") | `verify-email-description` | Instruction text |
| Body paragraph ("Click the link...") | `verify-email-body` | Detail text |
| "Back to sign in" footer link | `verify-email-back-to-login` | Footer nav link |

#### VerifyEmailConfirm.razor (`src/Wallow.Auth/Components/Pages/VerifyEmailConfirm.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| Card title ("Email Verification") | `verify-email-confirm-title` | Page heading |
| Loading spinner container | `verify-email-confirm-loading` | Loading state |
| Success alert (contains "Email verified!") | `verify-email-confirm-success` | Success state |
| Error alert (contains verification failed message) | `verify-email-confirm-error` | Error state |
| "Continue" button (only when ReturnUrl present) | `verify-email-confirm-continue` | Continue button |
| "Go to sign in" footer link | `verify-email-confirm-back-to-login` | Footer nav link |

### 3.2 New Infrastructure: `TestUserFactory.CreateUnverifiedAsync`

Add to `tests/Wallow.E2E.Tests/Infrastructure/TestUserFactory.cs`:

```csharp
/// <summary>
/// Creates a registered but unverified user. Does NOT click the verification link.
/// Returns the user credentials along with the verification link from Mailpit
/// (so tests can use or manipulate it).
/// </summary>
public static async Task<UnverifiedTestUser> CreateUnverifiedAsync(
    string apiBaseUrl, string mailpitBaseUrl)
```

**Implementation:**
1. Generate email `e2e-{Guid}@test.local`
2. Call `RegisterUserAsync(apiBaseUrl, email)` — same as existing private method
3. Search Mailpit for the verification link via `MailpitHelper.SearchForLinkAsync(mailpitBaseUrl, email, "verify")`
4. Return `UnverifiedTestUser(email, password, verificationLink)`

Add new record:

```csharp
public sealed record UnverifiedTestUser(string Email, string Password, string VerificationLink);
```

### 3.3 New Page Objects

#### VerifyEmailPage (`tests/Wallow.E2E.Tests/PageObjects/VerifyEmailPage.cs`)

```csharp
public sealed class VerifyEmailPage
{
    public VerifyEmailPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to /verify-email with optional returnUrl.
    /// </summary>
    public async Task NavigateAsync(string? returnUrl = null)

    /// <summary>
    /// Returns true if the verify-email page is loaded (title visible).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Returns the page title text.
    /// </summary>
    public async Task<string> GetTitleTextAsync()

    /// <summary>
    /// Returns the description text.
    /// </summary>
    public async Task<string> GetDescriptionTextAsync()

    /// <summary>
    /// Returns the body paragraph text.
    /// </summary>
    public async Task<string> GetBodyTextAsync()

    /// <summary>
    /// Clicks "Back to sign in" link.
    /// </summary>
    public async Task ClickBackToLoginAsync()
}
```

**Selectors:**
- `[data-testid='verify-email-title']`
- `[data-testid='verify-email-description']`
- `[data-testid='verify-email-body']`
- `[data-testid='verify-email-back-to-login']`

#### VerifyEmailConfirmPage (`tests/Wallow.E2E.Tests/PageObjects/VerifyEmailConfirmPage.cs`)

```csharp
public sealed class VerifyEmailConfirmPage
{
    public VerifyEmailConfirmPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to /verify-email/confirm with token and email params.
    /// </summary>
    public async Task NavigateAsync(string token, string email, string? returnUrl = null)

    /// <summary>
    /// Returns true if the page is loaded (title visible, no longer loading).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Returns true if the loading spinner is displayed.
    /// </summary>
    public async Task<bool> IsLoadingAsync()

    /// <summary>
    /// Returns true if the success state is visible ("Email verified!").
    /// </summary>
    public async Task<bool> IsSuccessAsync()

    /// <summary>
    /// Returns true if the error state is visible.
    /// </summary>
    public async Task<bool> IsErrorAsync()

    /// <summary>
    /// Returns the error message text, or null if no error is displayed.
    /// </summary>
    public async Task<string?> GetErrorMessageAsync()

    /// <summary>
    /// Returns the success message text, or null if not in success state.
    /// </summary>
    public async Task<string?> GetSuccessMessageAsync()

    /// <summary>
    /// Returns true if the "Continue" button is visible (only when returnUrl is set).
    /// </summary>
    public async Task<bool> HasContinueButtonAsync()

    /// <summary>
    /// Clicks the "Continue" button.
    /// </summary>
    public async Task ClickContinueAsync()

    /// <summary>
    /// Clicks "Go to sign in" footer link.
    /// </summary>
    public async Task ClickBackToLoginAsync()

    /// <summary>
    /// Waits for the page to finish the verification call (loading disappears).
    /// </summary>
    public async Task WaitForVerificationCompleteAsync(int timeoutMs = 15_000)
}
```

**Selectors:**
- `[data-testid='verify-email-confirm-title']`
- `[data-testid='verify-email-confirm-loading']`
- `[data-testid='verify-email-confirm-success']`
- `[data-testid='verify-email-confirm-error']`
- `[data-testid='verify-email-confirm-continue']`
- `[data-testid='verify-email-confirm-back-to-login']`

### 3.4 New Test Methods

All tests go in a new file `tests/Wallow.E2E.Tests/Flows/EmailVerificationFlowTests.cs` and use `[Trait("E2EGroup", "EmailVerification")]`.

#### Test 1: `EmailVerification_HappyPath_RegisterThenVerifyViaMailpit`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Register a user via the browser: `RegisterPage.NavigateAsync()`, fill form, submit
  2. Assert URL navigates to `/verify-email`
  3. Assert `VerifyEmailPage.IsLoadedAsync()` returns true
  4. Assert title text contains "Check your email"
  5. Search Mailpit for verification link: `MailpitHelper.SearchForLinkAsync(mailpitBaseUrl, email, "verify")`
  6. Navigate browser to the verification link (`Page.GotoAsync(verificationLink)`)
  7. Wait for URL to contain `/verify-email/confirm`
  8. Assert `VerifyEmailConfirmPage.WaitForVerificationCompleteAsync()` completes
  9. Assert `VerifyEmailConfirmPage.IsSuccessAsync()` returns true
  10. Assert success message contains "Email verified"
- **Page objects:** `RegisterPage`, `VerifyEmailPage`, `VerifyEmailConfirmPage`
- **Assertions:**
  - Navigate through full flow: register -> verify-email prompt -> Mailpit link -> confirm -> success
  - Success state visible on confirm page

#### Test 2: `EmailVerification_InvalidToken_ShowsError`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate directly to `/verify-email/confirm?token=invalid-token-123&email=fake@test.local`
  2. Wait for verification to complete
  3. Assert `VerifyEmailConfirmPage.IsErrorAsync()` returns true
  4. Assert error message contains "invalid" or "expired"
- **Page objects:** `VerifyEmailConfirmPage`
- **Assertions:**
  - Error state displayed
  - Error message references invalid/expired token

#### Test 3: `EmailVerification_AlreadyVerified_HandledGracefully`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create a fully verified user via `TestUserFactory.CreateAsync` (email already verified)
  2. Create an unverified user to get a real verification link format, OR craft a URL with the verified user's email and a token
  3. Alternative approach: create unverified user, visit verification link (success), then visit the same link again
  4. On second visit, assert either success (idempotent) or a graceful error (not a crash)
- **Page objects:** `VerifyEmailConfirmPage`
- **Assertions:**
  - No unhandled error or crash
  - Page shows either success (idempotent verify) or a clear "already verified" style message

#### Test 4: `EmailVerification_UnverifiedUserLogin_ShowsVerificationPrompt`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create an unverified user (register via API, do NOT verify)
  2. Navigate to login page
  3. Fill email and password, submit
  4. Assert error message contains "verify your email" (Login.razor maps `email_not_confirmed` to "Please verify your email before signing in.")
  5. URL stays on `/login` (no redirect to dashboard)
- **Page objects:** `LoginPage`
- **Assertions:**
  - Error message visible: "Please verify your email before signing in."
  - User is NOT logged in

## 4. Regression Verification Commands

```bash
# Run Phase 1 gate first
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth"

# Run Phase 2 tests
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=EmailVerification"

# Run both phases
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification"

# Full E2E
./scripts/run-e2e.sh
```

## 5. Complete File List

### Modified Files

| File | Changes |
|------|---------|
| `src/Wallow.Auth/Components/Pages/VerifyEmail.razor` | Add 4 `data-testid` attributes |
| `src/Wallow.Auth/Components/Pages/VerifyEmailConfirm.razor` | Add 6 `data-testid` attributes |
| `tests/Wallow.E2E.Tests/Infrastructure/TestUserFactory.cs` | Add `CreateUnverifiedAsync` method and `UnverifiedTestUser` record |

### Created Files

| File | Description |
|------|-------------|
| `tests/Wallow.E2E.Tests/PageObjects/VerifyEmailPage.cs` | Page object for `/verify-email` |
| `tests/Wallow.E2E.Tests/PageObjects/VerifyEmailConfirmPage.cs` | Page object for `/verify-email/confirm` |
| `tests/Wallow.E2E.Tests/Flows/EmailVerificationFlowTests.cs` | 4 new test methods |

## 6. Implementation Sequence

1. **Add data-testid attributes** to VerifyEmail.razor (4) and VerifyEmailConfirm.razor (6)
2. **Add `CreateUnverifiedAsync`** to `TestUserFactory` with `UnverifiedTestUser` record
3. **Create `VerifyEmailPage`** page object
4. **Create `VerifyEmailConfirmPage`** page object
5. **Create `EmailVerificationFlowTests.cs`** with all 4 tests
6. **Run regression:** `dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth"` passes, then `./scripts/run-e2e.sh` — all tests pass
