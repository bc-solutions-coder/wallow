# Phase 7: Settings — Profile, Landing, and Remaining Pages Implementation Plan

## 1. Objective

Add E2E coverage for the settings profile section, landing page behavior, and remaining static/utility pages (Error, Terms, Privacy, AcceptTerms). This is the final phase — establishes the `[Trait("E2EGroup", "Settings")]` regression group with 9 new tests. After completion, the full suite should have ~61 tests.

## 2. Prerequisites

- All previous phase regression gates pass (full suite):
  ```bash
  ./scripts/run-e2e.sh
  ```
- Infrastructure and apps running

## 3. Step-by-Step Plan

### 3.1 data-testid Audit and Additions

#### Settings.razor (`src/Wallow.Web/Components/Pages/Dashboard/Settings.razor`)

Currently has NO data-testid attributes. Add:

| Element | data-testid | Description |
|---------|-------------|-------------|
| Page heading ("Settings") | `settings-heading` | Main page title |
| Profile section heading ("Profile") | `settings-profile-heading` | Profile section title |
| Name value paragraph | `settings-profile-name` | User's display name |
| Email value paragraph | `settings-profile-email` | User's email |
| Roles container (the `<div class="flex flex-wrap gap-2">` or the "No roles" fallback) | `settings-profile-roles` | Roles display area |
| Individual role badge | `settings-profile-role` | Each role badge span |
| Loading state text | `settings-profile-loading` | "Loading profile..." text |
| MfaSettingsSection container (already has child testids) | `settings-mfa-section` | MFA section wrapper — add to the `<MfaSettingsSection />` component's root div |

**Total: 8 new data-testid attributes**

#### Home.razor (`src/Wallow.Web/Components/Pages/Home.razor`)

Currently has NO data-testid attributes. Add:

| Element | data-testid | Description |
|---------|-------------|-------------|
| Hero section | `home-hero` | Main hero `<section>` |
| Hero heading (h1 with tagline) | `home-heading` | Main heading text |
| "Get Started" link | `home-get-started` | CTA button |
| Features section | `home-features` | Features grid section |

**Total: 4 new data-testid attributes**

#### Error.razor (`src/Wallow.Auth/Components/Pages/Error.razor`)

Currently has NO data-testid attributes. Add:

| Element | data-testid | Description |
|---------|-------------|-------------|
| Card title ("Something went wrong") | `error-title` | Error page heading |
| Error message alert description | `error-message` | The dynamic error message text |
| "Sign out and try a different account" link (conditional) | `error-signout-link` | Only shown for `not_a_member` reason |
| "Back to home" link | `error-back-home` | Footer navigation link |

**Total: 4 new data-testid attributes**

#### Terms.razor (`src/Wallow.Auth/Components/Pages/Terms.razor`)

Currently has NO data-testid attributes. Add:

| Element | data-testid | Description |
|---------|-------------|-------------|
| Card title ("Terms of Service") | `terms-title` | Page heading |
| Content area (the BbCardContent with all sections) | `terms-content` | Main content body |
| "Back to Register" button | `terms-back` | Footer navigation button |

**Total: 3 new data-testid attributes**

#### Privacy.razor (`src/Wallow.Auth/Components/Pages/Privacy.razor`)

Currently has NO data-testid attributes. Add:

| Element | data-testid | Description |
|---------|-------------|-------------|
| Card title ("Privacy Policy") | `privacy-title` | Page heading |
| Content area | `privacy-content` | Main content body |
| "Back to Register" button | `privacy-back` | Footer navigation button |

**Total: 3 new data-testid attributes**

#### AcceptTerms.razor (`src/Wallow.Auth/Components/Pages/AcceptTerms.razor`)

Currently has NO data-testid attributes. Add:

| Element | data-testid | Description |
|---------|-------------|-------------|
| Card title ("Almost there!") | `accept-terms-title` | Page heading |
| Error alert (conditional on `Error` param) | `accept-terms-error` | Error message display |
| Email display container | `accept-terms-email-info` | Shows email and name |
| Terms checkbox | `accept-terms-checkbox` | Terms of Service checkbox |
| Privacy checkbox | `accept-terms-privacy-checkbox` | Privacy Policy checkbox |
| "Create Account" submit button | `accept-terms-submit` | Submit button (disabled until both checked) |
| "Back to sign in" link | `accept-terms-back-to-login` | Footer navigation |
| "Changed your mind?" text container | `accept-terms-footer` | Footer text area |

**Total: 8 new data-testid attributes**

### 3.2 New Page Objects

#### SettingsProfileSection (`tests/Wallow.E2E.Tests/PageObjects/SettingsProfileSection.cs`)

```csharp
public sealed class SettingsProfileSection
{
    public SettingsProfileSection(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to /dashboard/settings and waits for profile to load.
    /// </summary>
    public async Task NavigateAsync()

    /// <summary>
    /// Returns true if the profile section is loaded (name or email visible).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Returns the displayed name, or null if "Not set".
    /// </summary>
    public async Task<string?> GetNameAsync()

    /// <summary>
    /// Returns the displayed email, or null if "Not set".
    /// </summary>
    public async Task<string?> GetEmailAsync()

    /// <summary>
    /// Returns the list of displayed role names.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetRolesAsync()

    /// <summary>
    /// Returns true if any roles are displayed.
    /// </summary>
    public async Task<bool> HasRolesAsync()

    /// <summary>
    /// Returns true if the MFA settings section is also present on the page.
    /// </summary>
    public async Task<bool> IsMfaSectionPresentAsync()
}
```

**Selectors:**
- `[data-testid='settings-heading']`
- `[data-testid='settings-profile-name']`
- `[data-testid='settings-profile-email']`
- `[data-testid='settings-profile-roles']`
- `[data-testid='settings-profile-role']`
- `[data-testid='settings-mfa-status']` (existing, to verify MFA section presence)

#### ErrorPage (`tests/Wallow.E2E.Tests/PageObjects/ErrorPage.cs`)

```csharp
public sealed class ErrorPage
{
    public ErrorPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to /error with optional reason query param.
    /// </summary>
    public async Task NavigateAsync(string? reason = null)

    /// <summary>
    /// Returns true if the error page is loaded (title visible).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Returns the page title text.
    /// </summary>
    public async Task<string> GetTitleAsync()

    /// <summary>
    /// Returns the error message text.
    /// </summary>
    public async Task<string> GetErrorMessageAsync()

    /// <summary>
    /// Returns true if the "Sign out and try a different account" link is visible.
    /// </summary>
    public async Task<bool> HasSignOutLinkAsync()

    /// <summary>
    /// Clicks the "Back to home" link.
    /// </summary>
    public async Task ClickBackToHomeAsync()

    /// <summary>
    /// Clicks the "Sign out and try a different account" link.
    /// </summary>
    public async Task ClickSignOutLinkAsync()
}
```

**Selectors:**
- `[data-testid='error-title']`
- `[data-testid='error-message']`
- `[data-testid='error-signout-link']`
- `[data-testid='error-back-home']`

#### TermsPage (`tests/Wallow.E2E.Tests/PageObjects/TermsPage.cs`)

```csharp
public sealed class TermsPage
{
    public TermsPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to /terms.
    /// </summary>
    public async Task NavigateAsync()

    /// <summary>
    /// Returns true if the terms page is loaded (title visible).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Returns the page title text.
    /// </summary>
    public async Task<string> GetTitleAsync()

    /// <summary>
    /// Returns true if the content area has text content.
    /// </summary>
    public async Task<bool> HasContentAsync()

    /// <summary>
    /// Clicks "Back to Register" button.
    /// </summary>
    public async Task ClickBackAsync()
}
```

**Selectors:**
- `[data-testid='terms-title']`
- `[data-testid='terms-content']`
- `[data-testid='terms-back']`

#### PrivacyPage (`tests/Wallow.E2E.Tests/PageObjects/PrivacyPage.cs`)

```csharp
public sealed class PrivacyPage
{
    public PrivacyPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to /privacy.
    /// </summary>
    public async Task NavigateAsync()

    /// <summary>
    /// Returns true if the privacy page is loaded (title visible).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Returns the page title text.
    /// </summary>
    public async Task<string> GetTitleAsync()

    /// <summary>
    /// Returns true if the content area has text content.
    /// </summary>
    public async Task<bool> HasContentAsync()

    /// <summary>
    /// Clicks "Back to Register" button.
    /// </summary>
    public async Task ClickBackAsync()
}
```

**Selectors:**
- `[data-testid='privacy-title']`
- `[data-testid='privacy-content']`
- `[data-testid='privacy-back']`

#### AcceptTermsPage (`tests/Wallow.E2E.Tests/PageObjects/AcceptTermsPage.cs`)

```csharp
public sealed class AcceptTermsPage
{
    public AcceptTermsPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to /accept-terms with optional query params.
    /// </summary>
    public async Task NavigateAsync(string? email = null, string? name = null, string? returnUrl = null, string? error = null)

    /// <summary>
    /// Returns true if the page is loaded (title visible).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Returns the page title text.
    /// </summary>
    public async Task<string> GetTitleAsync()

    /// <summary>
    /// Returns the error message text, or null if no error is shown.
    /// </summary>
    public async Task<string?> GetErrorMessageAsync()

    /// <summary>
    /// Returns the displayed email from the info box, or null.
    /// </summary>
    public async Task<string?> GetDisplayedEmailAsync()

    /// <summary>
    /// Checks the Terms of Service checkbox.
    /// </summary>
    public async Task CheckTermsAsync()

    /// <summary>
    /// Checks the Privacy Policy checkbox.
    /// </summary>
    public async Task CheckPrivacyAsync()

    /// <summary>
    /// Returns true if the submit button is enabled.
    /// </summary>
    public async Task<bool> IsSubmitEnabledAsync()

    /// <summary>
    /// Clicks the "Create Account" submit button.
    /// </summary>
    public async Task SubmitAsync()

    /// <summary>
    /// Clicks "Back to sign in" link.
    /// </summary>
    public async Task ClickBackToLoginAsync()

    /// <summary>
    /// Returns true if both checkboxes are checked.
    /// </summary>
    public async Task<bool> AreBothCheckboxesCheckedAsync()
}
```

**Selectors:**
- `[data-testid='accept-terms-title']`
- `[data-testid='accept-terms-error']`
- `[data-testid='accept-terms-email-info']`
- `[data-testid='accept-terms-checkbox']`
- `[data-testid='accept-terms-privacy-checkbox']`
- `[data-testid='accept-terms-submit']`
- `[data-testid='accept-terms-back-to-login']`

### 3.3 New Test Methods

Tests are split across two test files:
- **`SettingsFlowTests.cs`** — authenticated tests (requires login)
- **`StaticPagesFlowTests.cs`** — unauthenticated tests

#### SettingsFlowTests (`tests/Wallow.E2E.Tests/Flows/SettingsFlowTests.cs`)

All tagged with `[Trait("E2EGroup", "Settings")]`. Extends `AuthenticatedE2ETestBase`.

##### Test 1: `Settings_ProfileDisplaysNameEmailAndRoles`

- **Type:** Happy path
- **Base class:** `AuthenticatedE2ETestBase`
- **Flow:**
  1. Navigate to settings page via `SettingsProfileSection.NavigateAsync()`
  2. Assert `SettingsProfileSection.IsLoadedAsync()` returns true
  3. Assert `SettingsProfileSection.GetEmailAsync()` returns the test user's email
  4. Assert `SettingsProfileSection.GetNameAsync()` returns non-null (or "Not set" — depends on test user setup)
  5. Get roles via `SettingsProfileSection.GetRolesAsync()` — verify list is returned (may be empty for basic user)
- **Page objects:** `SettingsProfileSection`
- **Assertions:**
  - Profile section loads
  - Email matches test user's email
  - Name is displayed (may be "Not set")
  - Roles section renders

##### Test 2: `Settings_MfaSectionCoexistsWithProfileSection`

- **Type:** Happy path
- **Base class:** `AuthenticatedE2ETestBase`
- **Flow:**
  1. Navigate to settings page
  2. Assert `SettingsProfileSection.IsLoadedAsync()` returns true
  3. Assert `SettingsProfileSection.IsMfaSectionPresentAsync()` returns true (MFA status element visible)
- **Page objects:** `SettingsProfileSection`
- **Assertions:**
  - Both profile and MFA sections render on the same page

##### Test 3: `LandingPage_AuthenticatedUser_RedirectedToDashboard`

- **Type:** Happy path
- **Base class:** `AuthenticatedE2ETestBase`
- **Flow:**
  1. Navigate to `/` (home page)
  2. Wait for URL to change
  3. Assert URL contains `/dashboard` (authenticated users are redirected)
- **Page objects:** None (direct navigation)
- **Assertions:**
  - Authenticated user is redirected from landing to dashboard

#### StaticPagesFlowTests (`tests/Wallow.E2E.Tests/Flows/StaticPagesFlowTests.cs`)

All tagged with `[Trait("E2EGroup", "Settings")]`. Extends `E2ETestBase` (unauthenticated).

##### Test 4: `LandingPage_UnauthenticatedUser_SeesLandingOrRedirectsToLogin`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to Web app root (`/`)
  2. Wait for page to settle
  3. Assert either:
     - Landing page is visible (hero heading with branding tagline, `[data-testid='home-heading']`)
     - OR URL redirected to `/authentication/login` (if landing page is disabled in config)
- **Page objects:** None (direct selectors or URL check)
- **Assertions:**
  - Unauthenticated user sees landing page or is redirected to login

##### Test 5: `ErrorPage_DisplaysCorrectMessagePerReasonParam`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Approach:** Use `[Theory]` with `[InlineData]` to test multiple reason params
- **Flow (per reason):**
  1. Navigate to `/error?reason={reason}` via `ErrorPage.NavigateAsync(reason)`
  2. Assert `ErrorPage.IsLoadedAsync()` returns true
  3. Assert `ErrorPage.GetErrorMessageAsync()` matches expected message

**Theory data:**

```csharp
[Theory]
[InlineData("not_a_member", "You don't have access to this application.")]
[InlineData("invalid_redirect_uri", "The redirect destination is not permitted.")]
[InlineData("access_denied", "Access was denied. Please try again or contact support.")]
[InlineData("invalid_request", "The request was invalid. Please try again.")]
[InlineData(null, "An unexpected error occurred. Please try again.")]
[InlineData("unknown_reason", "An unexpected error occurred. Please try again.")]
```

- **Page objects:** `ErrorPage`
- **Assertions:**
  - Error message matches expected text for each reason
  - For `not_a_member`, assert `ErrorPage.HasSignOutLinkAsync()` returns true
  - For other reasons, assert `ErrorPage.HasSignOutLinkAsync()` returns false

##### Test 6: `TermsPage_ContentRendersAndBackLinkWorks`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to `/terms` via `TermsPage.NavigateAsync()`
  2. Assert `TermsPage.IsLoadedAsync()` returns true
  3. Assert `TermsPage.GetTitleAsync()` returns "Terms of Service"
  4. Assert `TermsPage.HasContentAsync()` returns true
  5. Click back via `TermsPage.ClickBackAsync()`
  6. Assert URL navigates to `/register`
- **Page objects:** `TermsPage`
- **Assertions:**
  - Page loads with title and content
  - Back link navigates to register page

##### Test 7: `PrivacyPage_ContentRendersAndBackLinkWorks`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to `/privacy` via `PrivacyPage.NavigateAsync()`
  2. Assert `PrivacyPage.IsLoadedAsync()` returns true
  3. Assert `PrivacyPage.GetTitleAsync()` returns "Privacy Policy"
  4. Assert `PrivacyPage.HasContentAsync()` returns true
  5. Click back via `PrivacyPage.ClickBackAsync()`
  6. Assert URL navigates to `/register`
- **Page objects:** `PrivacyPage`
- **Assertions:**
  - Page loads with title and content
  - Back link navigates to register page

##### Test 8: `AcceptTerms_CheckBothBoxesThenSubmit_Proceeds`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to `/accept-terms?email=test@test.local&name=Test+User` via `AcceptTermsPage.NavigateAsync`
  2. Assert `AcceptTermsPage.IsLoadedAsync()` returns true
  3. Assert `AcceptTermsPage.IsSubmitEnabledAsync()` returns false (neither checkbox checked)
  4. Check terms via `AcceptTermsPage.CheckTermsAsync()`
  5. Assert submit still disabled (only one checked)
  6. Check privacy via `AcceptTermsPage.CheckPrivacyAsync()`
  7. Assert `AcceptTermsPage.IsSubmitEnabledAsync()` returns true
  8. Click submit via `AcceptTermsPage.SubmitAsync()`
  9. Assert page navigates away (to API endpoint for completing registration)
- **Page objects:** `AcceptTermsPage`
- **Assertions:**
  - Button disabled until both boxes checked
  - Button enabled after both checked
  - Submit navigates away

##### Test 9: `AcceptTerms_SubmitWithoutCheckingBoxes_ButtonDisabled`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to `/accept-terms`
  2. Assert `AcceptTermsPage.IsLoadedAsync()` returns true
  3. Assert `AcceptTermsPage.IsSubmitEnabledAsync()` returns false
  4. Check only terms (not privacy)
  5. Assert `AcceptTermsPage.IsSubmitEnabledAsync()` returns false
  6. Uncheck terms, check only privacy
  7. Assert `AcceptTermsPage.IsSubmitEnabledAsync()` returns false
- **Page objects:** `AcceptTermsPage`
- **Assertions:**
  - Submit button remains disabled unless both checkboxes are checked

## 4. Regression Verification Commands

```bash
# Full suite gate (all previous phases)
./scripts/run-e2e.sh

# Phase 7 tests only
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Settings"

# Final full suite — all ~61 tests
./scripts/run-e2e.sh
```

## 5. Complete File List

### Modified Files

| File | Changes |
|------|---------|
| `src/Wallow.Web/Components/Pages/Dashboard/Settings.razor` | Add 8 `data-testid` attributes |
| `src/Wallow.Web/Components/Pages/Home.razor` | Add 4 `data-testid` attributes |
| `src/Wallow.Auth/Components/Pages/Error.razor` | Add 4 `data-testid` attributes |
| `src/Wallow.Auth/Components/Pages/Terms.razor` | Add 3 `data-testid` attributes |
| `src/Wallow.Auth/Components/Pages/Privacy.razor` | Add 3 `data-testid` attributes |
| `src/Wallow.Auth/Components/Pages/AcceptTerms.razor` | Add 8 `data-testid` attributes |

### Created Files

| File | Description |
|------|-------------|
| `tests/Wallow.E2E.Tests/PageObjects/SettingsProfileSection.cs` | Page object for settings profile section |
| `tests/Wallow.E2E.Tests/PageObjects/ErrorPage.cs` | Page object for error page |
| `tests/Wallow.E2E.Tests/PageObjects/TermsPage.cs` | Page object for terms page |
| `tests/Wallow.E2E.Tests/PageObjects/PrivacyPage.cs` | Page object for privacy page |
| `tests/Wallow.E2E.Tests/PageObjects/AcceptTermsPage.cs` | Page object for accept-terms page |
| `tests/Wallow.E2E.Tests/Flows/SettingsFlowTests.cs` | 3 authenticated tests |
| `tests/Wallow.E2E.Tests/Flows/StaticPagesFlowTests.cs` | 6 unauthenticated tests |

## 6. Implementation Sequence

1. **Add data-testid attributes** to all 6 Blazor files:
   - Settings.razor (8)
   - Home.razor (4)
   - Error.razor (4)
   - Terms.razor (3)
   - Privacy.razor (3)
   - AcceptTerms.razor (8)
2. **Create 5 page objects:**
   - `SettingsProfileSection`
   - `ErrorPage`
   - `TermsPage`
   - `PrivacyPage`
   - `AcceptTermsPage`
3. **Write `SettingsFlowTests.cs`** with 3 authenticated tests (profile display, MFA coexistence, landing redirect)
4. **Write `StaticPagesFlowTests.cs`** with 6 unauthenticated tests (landing unauthenticated, error page Theory, terms, privacy, accept-terms happy path, accept-terms validation)
5. **Run final regression:** `./scripts/run-e2e.sh` — all ~61 tests pass. This is the final phase.
