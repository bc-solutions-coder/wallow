# Phase 6: App Registration — Branding, Scopes, List Verification Implementation Plan

## 1. Objective

Expand E2E coverage for the app registration flow to include branding fields, scope toggles, logo upload, client secret handling, app list verification, and negative cases. Retag the existing test and add 6 new tests. Establishes the `[Trait("E2EGroup", "AppRegistration")]` regression group.

## 2. Prerequisites

- Phase 1-5 regression gates pass:
  ```bash
  dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification|E2EGroup=MFA|E2EGroup=AltAuth|E2EGroup=Organizations|E2EGroup=Invitations"
  ```
- Infrastructure and apps running

## 3. Step-by-Step Plan

### 3.1 Add data-testid Attributes to Blazor Components

#### RegisterApp.razor (`src/Wallow.Web/Components/Pages/Dashboard/RegisterApp.razor`)

Most form fields already have `data-testid` attributes. The following are missing:

| Element | data-testid | Description |
|---------|-------------|-------------|
| Scope toggle buttons (each) | `register-app-scope-{scopeName}` | Individual scope toggle buttons. The `scopeName` is kebab-cased, e.g., `register-app-scope-inquiries-read` |
| Branding section heading | `register-app-branding-heading` | "Branding (Optional)" heading |
| Company Display Name input | `register-app-branding-display-name` | Branding display name field |
| Tagline input | `register-app-branding-tagline` | Branding tagline field |
| Logo file input | `register-app-branding-logo` | Logo upload input |
| Logo preview image | `register-app-branding-logo-preview` | Logo preview thumbnail |
| Client secret display (on success page) | `register-app-client-secret` | Client secret text in success view |
| Client secret copy button | `register-app-copy-secret` | Copy button for secret |
| Client ID copy button | `register-app-copy-client-id` | Copy button for client ID |

**Scope toggle testid pattern:** Each scope button gets `data-testid="register-app-scope-{scope.Replace(".", "-")}"`. For the available scopes `["inquiries.read", "inquiries.write", "announcements.read", "storage.read"]`, the testids would be:
- `register-app-scope-inquiries-read`
- `register-app-scope-inquiries-write`
- `register-app-scope-announcements-read`
- `register-app-scope-storage-read`

#### Apps.razor (`src/Wallow.Web/Components/Pages/Dashboard/Apps.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| App row name cell | `apps-row-name` | Display name in each row |
| App row client-id cell | `apps-row-client-id` | Client ID in each row |
| App row type cell | `apps-row-type` | Client type badge in each row |
| App row created cell | `apps-row-created` | Created date in each row |

**Note:** The `apps-row` testid already exists on `<tr>` elements. Add testids to the `<td>` cells within each row for precise data extraction.

### 3.2 Retag Existing Test

In `tests/Wallow.E2E.Tests/Flows/DashboardFlowTests.cs`:

| Test Method | Trait to Add |
|-------------|-------------|
| `AppRegistrationFlow_RegistersNewApplication` | `[Trait("E2EGroup", "AppRegistration")]` |

### 3.3 Page Object Changes

#### AppRegistrationPage (`tests/Wallow.E2E.Tests/PageObjects/AppRegistrationPage.cs`)

Add scope toggle methods:

```csharp
/// <summary>
/// Toggles a scope on or off by clicking its button.
/// </summary>
/// <param name="scopeName">The scope name, e.g., "inquiries.read"</param>
public async Task ToggleScopeAsync(string scopeName)

/// <summary>
/// Returns true if the given scope is currently selected (button has active styling).
/// </summary>
/// <param name="scopeName">The scope name, e.g., "inquiries.read"</param>
public async Task<bool> IsScopeSelectedAsync(string scopeName)

/// <summary>
/// Selects specific scopes (deselects default ones first if needed).
/// </summary>
/// <param name="scopes">List of scope names to select</param>
public async Task SetScopesAsync(IReadOnlyList<string> scopes)
```

**Selector pattern:** `[data-testid='register-app-scope-{scope.Replace(".", "-")}']`

Add branding field methods:

```csharp
/// <summary>
/// Fills the branding company display name field.
/// </summary>
public async Task FillBrandingDisplayNameAsync(string name)

/// <summary>
/// Fills the branding tagline field.
/// </summary>
public async Task FillBrandingTaglineAsync(string tagline)

/// <summary>
/// Uploads a logo file.
/// </summary>
public async Task UploadLogoAsync(string filePath)

/// <summary>
/// Returns true if the logo preview image is visible.
/// </summary>
public async Task<bool> IsLogoPreviewVisibleAsync()
```

**Selectors:**
- `[data-testid='register-app-branding-display-name']`
- `[data-testid='register-app-branding-tagline']`
- `[data-testid='register-app-branding-logo']`
- `[data-testid='register-app-branding-logo-preview']`

Add client secret methods:

```csharp
/// <summary>
/// Returns the client secret text from the success page, or null if not displayed.
/// </summary>
public async Task<string?> GetClientSecretAsync()

/// <summary>
/// Returns true if a client secret is displayed (confidential app).
/// </summary>
public async Task<bool> HasClientSecretAsync()
```

**Selector:** `[data-testid='register-app-client-secret']`

Add error helper:

```csharp
/// <summary>
/// Returns the error message text, or null if no error is displayed.
/// Waits for the error element to appear.
/// </summary>
public async Task<string?> GetVisibleErrorAsync(int timeoutMs = 5_000)
```

**Selector:** `[data-testid='register-app-error']` (already exists)

Update `GetResultAsync` to also extract client secret:

```csharp
/// <summary>
/// Gets the full registration result including client secret (if present).
/// </summary>
public async Task<AppRegistrationResult> GetResultAsync()
// Updated to also check for [data-testid='register-app-client-secret']
```

#### DashboardPage (`tests/Wallow.E2E.Tests/PageObjects/DashboardPage.cs`)

Add app list verification methods:

```csharp
/// <summary>
/// Returns all app rows currently displayed in the apps table.
/// </summary>
public async Task<IReadOnlyList<AppRow>> GetAppRowsAsync()

/// <summary>
/// Finds an app row by display name, or returns null if not found.
/// </summary>
public async Task<AppRow?> FindAppByNameAsync(string displayName)

/// <summary>
/// Returns true if the empty state is displayed (no apps registered).
/// </summary>
public async Task<bool> IsEmptyStateAsync()

/// <summary>
/// Clicks "Register New App" link.
/// </summary>
public async Task ClickRegisterNewAppAsync()
```

Add `AppRow` record:

```csharp
public sealed record AppRow(string Name, string ClientId, string ClientType, string CreatedDate);
```

**Selectors:**
- `[data-testid='apps-row']` — row locator
- `[data-testid='apps-row-name']` — name cell
- `[data-testid='apps-row-client-id']` — client ID cell
- `[data-testid='apps-row-type']` — type cell
- `[data-testid='apps-row-created']` — created date cell
- `[data-testid='apps-empty-state']` — empty state
- `[data-testid='apps-register-link']` — register link

### 3.4 Test Logo Fixture File

Create a small test PNG file for logo upload tests:

**File:** `tests/Wallow.E2E.Tests/Fixtures/test-logo.png`

This can be a minimal valid PNG (1x1 pixel, transparent). Generate it programmatically or commit a tiny PNG file.

### 3.5 New Test Methods

All tests go in `tests/Wallow.E2E.Tests/Flows/AppRegistrationFlowTests.cs` with `[Trait("E2EGroup", "AppRegistration")]`. Tests use `AuthenticatedE2ETestBase` (user is already logged in).

#### Test 1: `AppRegistration_WithBranding_RegistersSuccessfully`

- **Type:** Happy path
- **Base class:** `AuthenticatedE2ETestBase`
- **Flow:**
  1. Navigate to app registration page
  2. Fill basic form (display name "branded-e2e-app", client type "public", redirect URIs)
  3. Fill branding fields: display name "E2E Company", tagline "Testing all the things"
  4. Upload test logo via `AppRegistrationPage.UploadLogoAsync("tests/Wallow.E2E.Tests/Fixtures/test-logo.png")`
  5. Submit
  6. Assert success with client ID
- **Page objects:** `AppRegistrationPage`
- **Assertions:**
  - Registration succeeds
  - Client ID returned

#### Test 2: `AppRegistration_WithScopeToggles_RegistersWithCorrectScopes`

- **Type:** Happy path
- **Base class:** `AuthenticatedE2ETestBase`
- **Flow:**
  1. Navigate to app registration page
  2. Fill basic form
  3. Deselect default scope (`inquiries.read`) by toggling it off
  4. Select `inquiries.write` and `announcements.read`
  5. Submit
  6. Assert success
- **Page objects:** `AppRegistrationPage`
- **Assertions:**
  - Registration succeeds with selected scopes

#### Test 3: `AppRegistration_WithInvalidInput_ShowsErrors`

- **Type:** Negative
- **Base class:** `AuthenticatedE2ETestBase`
- **Flow:**
  1. Navigate to app registration page
  2. Leave display name empty
  3. Fill redirect URIs with invalid value (e.g., "not-a-uri")
  4. Submit
  5. Assert error message displayed
- **Page objects:** `AppRegistrationPage`
- **Assertions:**
  - Error visible about missing/invalid input
  - URL still on registration page

#### Test 4: `AppRegistration_ThenAppsList_NewAppAppearsWithCorrectDetails`

- **Type:** Happy path
- **Base class:** `AuthenticatedE2ETestBase`
- **Flow:**
  1. Register a new app with name "list-verify-e2e-app", type "public"
  2. Assert success with client ID
  3. Navigate to apps list (`/dashboard/apps`)
  4. Wait for `DashboardPage.IsLoadedAsync()`
  5. Find the app by name via `DashboardPage.FindAppByNameAsync("list-verify-e2e-app")`
  6. Assert app row found
  7. Assert client type is "public"
  8. Assert client ID matches the one from registration
- **Page objects:** `AppRegistrationPage`, `DashboardPage`
- **Assertions:**
  - New app appears in list
  - Name, client ID, and type match

#### Test 5: `AppRegistration_ConfidentialApp_DisplaysClientSecret`

- **Type:** Happy path
- **Base class:** `AuthenticatedE2ETestBase`
- **Flow:**
  1. Navigate to app registration page
  2. Fill form with client type "confidential", valid name and redirect URIs
  3. Submit
  4. Assert success
  5. Assert `AppRegistrationPage.HasClientSecretAsync()` returns true
  6. Assert `AppRegistrationPage.GetClientSecretAsync()` returns a non-empty string
- **Page objects:** `AppRegistrationPage`
- **Assertions:**
  - Client secret is displayed for confidential app
  - Secret is non-empty

#### Test 6: `AppRegistration_PublicApp_NoClientSecretShown`

- **Type:** Happy path
- **Base class:** `AuthenticatedE2ETestBase`
- **Flow:**
  1. Navigate to app registration page
  2. Fill form with client type "public", valid name and redirect URIs
  3. Submit
  4. Assert success
  5. Assert `AppRegistrationPage.HasClientSecretAsync()` returns false
- **Page objects:** `AppRegistrationPage`
- **Assertions:**
  - No client secret displayed for public app

## 4. Regression Verification Commands

```bash
# Phase 1-5 gate
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification|E2EGroup=MFA|E2EGroup=AltAuth|E2EGroup=Organizations|E2EGroup=Invitations"

# Phase 6 tests
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=AppRegistration"

# Full E2E
./scripts/run-e2e.sh
```

## 5. Complete File List

### Modified Files

| File | Changes |
|------|---------|
| `src/Wallow.Web/Components/Pages/Dashboard/RegisterApp.razor` | Add ~9 `data-testid` attributes (scope toggles, branding fields, logo, secret) |
| `src/Wallow.Web/Components/Pages/Dashboard/Apps.razor` | Add 4 `data-testid` attributes to row cells |
| `tests/Wallow.E2E.Tests/Flows/DashboardFlowTests.cs` | Add `[Trait("E2EGroup", "AppRegistration")]` to existing test |
| `tests/Wallow.E2E.Tests/PageObjects/AppRegistrationPage.cs` | Add scope toggles (3), branding (4), client secret (2), error helper (1) methods |
| `tests/Wallow.E2E.Tests/PageObjects/DashboardPage.cs` | Add `GetAppRowsAsync`, `FindAppByNameAsync`, `IsEmptyStateAsync`, `ClickRegisterNewAppAsync` methods and `AppRow` record |

### Created Files

| File | Description |
|------|-------------|
| `tests/Wallow.E2E.Tests/Flows/AppRegistrationFlowTests.cs` | 6 new test methods |
| `tests/Wallow.E2E.Tests/Fixtures/test-logo.png` | Small test PNG file for logo upload tests |

## 6. Implementation Sequence

1. **Add data-testid attributes** to RegisterApp.razor (~9 new) and Apps.razor (4 new to row cells)
2. **Create test logo fixture** file (`test-logo.png`)
3. **Add AppRegistrationPage methods** — scope toggles (3), branding (4), client secret (2), error helper (1)
4. **Add DashboardPage methods** — `GetAppRowsAsync`, `FindAppByNameAsync`, `IsEmptyStateAsync`, `ClickRegisterNewAppAsync`, `AppRow` record
5. **Retag existing test** with `[Trait("E2EGroup", "AppRegistration")]`
6. **Write 6 new tests** (branding, scopes, invalid input, list verification, confidential secret, public no-secret)
7. **Run regression:** all previous phases pass, then `./scripts/run-e2e.sh` — all ~52 tests pass
