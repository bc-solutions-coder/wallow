# Phase 5: Organizations + Invitations Implementation Plan

## 1. Objective

Add E2E coverage for organization management (detail page, member list, bound clients, client registration) and invitation flows (landing page, accept/decline, expiry). Establishes the `[Trait("E2EGroup", "Organizations")]` and `[Trait("E2EGroup", "Invitations")]` regression groups with 12 new tests (5 org, 5 invitation, 2 cross-tagged).

## 2. Prerequisites

- Phase 1-4 regression gates pass:
  ```bash
  dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification|E2EGroup=MFA|E2EGroup=AltAuth"
  ```
- Infrastructure and apps running

## 3. Step-by-Step Plan

### 3.1 Add data-testid Attributes to Blazor Components

#### OrganizationDetail.razor (`src/Wallow.Web/Components/Pages/Dashboard/OrganizationDetail.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| "Back to Organizations" link | `org-detail-back-link` | Navigation back to org list |
| Organization name heading (`<h1>`) | `org-detail-name` | Org name display |
| Domain span | `org-detail-domain` | Org domain display |
| Member count span | `org-detail-member-count` | Member count display |
| Members section heading | `org-detail-members-heading` | "Members" section title |
| Members table | `org-detail-members-table` | Members table element |
| Member row (`<tr>` in members tbody) | `org-detail-member-row` | Individual member row |
| Member email cell | `org-detail-member-email` | Email in member row |
| Member role badges | `org-detail-member-roles` | Roles container in member row |
| Members empty state | `org-detail-members-empty` | "No members found" state |
| Bound Clients section heading | `org-detail-clients-heading` | "Bound Clients" section title |
| Bound clients table | `org-detail-clients-table` | Clients table element |
| Client row (`<tr>` in clients tbody) | `org-detail-client-row` | Individual client row |
| Client ID cell | `org-detail-client-id` | Client ID in client row |
| Client name cell | `org-detail-client-name` | Client name in client row |
| Clients empty state | `org-detail-clients-empty` | "No clients bound" state |
| Register Client heading | `org-detail-register-heading` | "Register Client" section title |
| Register Client display name input | `org-detail-register-name` | App name input |
| Register Client type select | `org-detail-register-type` | Client type dropdown |
| Register Client redirect URIs textarea | `org-detail-register-uris` | Redirect URIs input |
| Register Client submit button | `org-detail-register-submit` | Submit button |
| Register Client success result | `org-detail-register-success` | Success message with client ID |
| Register Client error result | `org-detail-register-error` | Error message |

**Total: ~23 data-testid attributes**

#### InvitationLanding.razor (`src/Wallow.Auth/Components/Pages/InvitationLanding.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| Card title ("You've been invited") | `invitation-title` | Page heading |
| Loading state text | `invitation-loading` | "Loading invitation..." |
| Error alert (top-level `_errorMessage`) | `invitation-error` | General error state |
| "Back to sign in" link (in error state) | `invitation-back-to-login` | Error footer link |
| Info alert (invitation details) | `invitation-info` | Invitation details alert |
| Expired alert | `invitation-expired` | Expiration error alert |
| Accept error alert (`_acceptError`) | `invitation-accept-error` | Error on accept attempt |
| "Yes, join" button | `invitation-accept` | Accept invitation button |
| "No thanks" button/link | `invitation-decline` | Decline invitation button |
| "Create account" button (unauthenticated) | `invitation-create-account` | Register link |
| "Sign in to accept" button (unauthenticated) | `invitation-sign-in` | Login link |
| Invitation email display | `invitation-email` | The invited email address |
| Prompt text for authenticated user | `invitation-prompt` | "Would you like to join?" text |

**Total: ~13 data-testid attributes**

#### Organizations.razor (`src/Wallow.Web/Components/Pages/Dashboard/Organizations.razor`)

| Element | data-testid | Description |
|---------|-------------|-------------|
| Organization row — make clickable | `organizations-row-link` | Wrap row content in a link or add click handler with this testid |

**Note:** The existing `organizations-row` testid is on `<tr>` elements. To enable navigation to detail page, either add an `@onclick` handler or wrap the row in an anchor. The row needs a data-testid for click targeting. If the row is already clickable via an `<a>` inside the name cell, add the testid there instead.

**Total: ~1 new data-testid attribute (the rest already exist)**

### 3.2 New Infrastructure

#### InvitationHelper (`tests/Wallow.E2E.Tests/Infrastructure/InvitationHelper.cs`)

```csharp
internal static class InvitationHelper
{
    /// <summary>
    /// Creates an invitation for the given email in the given org via the API.
    /// Returns the invitation token.
    /// </summary>
    public static async Task<string> CreateInvitationAsync(
        string apiBaseUrl, Guid orgId, string inviteeEmail, HttpClient authenticatedClient)

    /// <summary>
    /// Creates an invitation and returns the full landing page URL.
    /// </summary>
    public static async Task<string> CreateInvitationUrlAsync(
        string authBaseUrl, string apiBaseUrl, Guid orgId, string inviteeEmail, HttpClient authenticatedClient)

    /// <summary>
    /// Retrieves the invitation token from Mailpit (if the invitation sends an email).
    /// </summary>
    public static async Task<string> GetInvitationTokenFromMailpitAsync(
        string mailpitBaseUrl, string inviteeEmail)
}
```

#### OrgAdminTestUser Record

```csharp
public sealed record OrgAdminTestUser(
    string Email, string Password, Guid OrganizationId, string OrganizationName);
```

#### TestUserFactory.CreateOrgAdminAsync

Add to `tests/Wallow.E2E.Tests/Infrastructure/TestUserFactory.cs`:

```csharp
/// <summary>
/// Creates a verified test user who owns an organization.
/// Returns the user credentials along with the org ID and name.
/// </summary>
public static async Task<OrgAdminTestUser> CreateOrgAdminAsync(
    string apiBaseUrl, string mailpitBaseUrl)
```

**Implementation:**
1. Create verified user via existing `CreateAsync`
2. Login via `LoginAndExchangeTicketAsync`
3. Create an organization via `POST /api/v1/identity/organizations` with a unique name
4. Return `OrgAdminTestUser` with org details

### 3.3 New Page Objects

#### OrganizationDetailPage (`tests/Wallow.E2E.Tests/PageObjects/OrganizationDetailPage.cs`)

```csharp
public sealed class OrganizationDetailPage
{
    public OrganizationDetailPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to /dashboard/organizations/{orgId}.
    /// </summary>
    public async Task NavigateAsync(Guid orgId)

    /// <summary>
    /// Returns true if the detail page is loaded (org name heading visible).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Returns the organization name from the heading.
    /// </summary>
    public async Task<string> GetOrganizationNameAsync()

    /// <summary>
    /// Returns the domain text.
    /// </summary>
    public async Task<string> GetDomainAsync()

    /// <summary>
    /// Returns the member count text.
    /// </summary>
    public async Task<string> GetMemberCountAsync()

    /// <summary>
    /// Returns the list of members displayed in the table.
    /// </summary>
    public async Task<IReadOnlyList<MemberRow>> GetMembersAsync()

    /// <summary>
    /// Returns true if the members empty state is shown.
    /// </summary>
    public async Task<bool> IsMembersEmptyAsync()

    /// <summary>
    /// Returns the list of bound clients displayed in the table.
    /// </summary>
    public async Task<IReadOnlyList<ClientRow>> GetBoundClientsAsync()

    /// <summary>
    /// Returns true if the clients empty state is shown.
    /// </summary>
    public async Task<bool> IsClientsEmptyAsync()

    /// <summary>
    /// Fills the register client form.
    /// </summary>
    public async Task FillRegisterClientFormAsync(string displayName, string clientType, string redirectUris)

    /// <summary>
    /// Submits the register client form.
    /// </summary>
    public async Task SubmitRegisterClientAsync()

    /// <summary>
    /// Returns the register client result (success or error).
    /// </summary>
    public async Task<RegisterClientResult> GetRegisterClientResultAsync()

    /// <summary>
    /// Clicks "Back to Organizations" link.
    /// </summary>
    public async Task ClickBackAsync()
}

public sealed record MemberRow(string Email, IReadOnlyList<string> Roles);
public sealed record ClientRow(string ClientId, string Name);
public sealed record RegisterClientResult(bool Success, string? ClientId, string? ClientSecret, string? ErrorMessage);
```

**Selectors:** All `org-detail-*` testids listed in section 3.1.

#### InvitationLandingPage (`tests/Wallow.E2E.Tests/PageObjects/InvitationLandingPage.cs`)

```csharp
public sealed class InvitationLandingPage
{
    public InvitationLandingPage(IPage page, string baseUrl)

    /// <summary>
    /// Navigates to /invitation?token={token}.
    /// </summary>
    public async Task NavigateAsync(string token)

    /// <summary>
    /// Returns true if the page is loaded (not in loading state).
    /// </summary>
    public async Task<bool> IsLoadedAsync()

    /// <summary>
    /// Returns true if the loading state is displayed.
    /// </summary>
    public async Task<bool> IsLoadingAsync()

    /// <summary>
    /// Returns true if an error is displayed.
    /// </summary>
    public async Task<bool> HasErrorAsync()

    /// <summary>
    /// Returns the error message text, or null.
    /// </summary>
    public async Task<string?> GetErrorMessageAsync()

    /// <summary>
    /// Returns true if the invitation is expired.
    /// </summary>
    public async Task<bool> IsExpiredAsync()

    /// <summary>
    /// Returns the invited email address shown on the page.
    /// </summary>
    public async Task<string?> GetInvitedEmailAsync()

    /// <summary>
    /// Clicks the "Yes, join" (accept) button. Only visible when authenticated.
    /// </summary>
    public async Task ClickAcceptAsync()

    /// <summary>
    /// Clicks the "No thanks" (decline) button. Only visible when authenticated.
    /// </summary>
    public async Task ClickDeclineAsync()

    /// <summary>
    /// Clicks "Create account" button. Only visible when unauthenticated.
    /// </summary>
    public async Task ClickCreateAccountAsync()

    /// <summary>
    /// Clicks "Sign in to accept" button. Only visible when unauthenticated.
    /// </summary>
    public async Task ClickSignInAsync()

    /// <summary>
    /// Returns true if the accept/decline buttons are visible (authenticated state).
    /// </summary>
    public async Task<bool> IsAuthenticatedStateAsync()

    /// <summary>
    /// Returns true if the create account/sign in buttons are visible (unauthenticated state).
    /// </summary>
    public async Task<bool> IsUnauthenticatedStateAsync()

    /// <summary>
    /// Returns the accept error message, or null.
    /// </summary>
    public async Task<string?> GetAcceptErrorAsync()

    /// <summary>
    /// Waits for the page to finish loading the invitation.
    /// </summary>
    public async Task WaitForLoadCompleteAsync(int timeoutMs = 10_000)
}
```

**Selectors:** All `invitation-*` testids listed in section 3.1.

### 3.4 OrganizationPage Navigation Helpers

Add to existing `tests/Wallow.E2E.Tests/PageObjects/OrganizationPage.cs`:

```csharp
/// <summary>
/// Clicks on an organization row by name to navigate to the detail page.
/// </summary>
public async Task ClickOrganizationByNameAsync(string orgName)

/// <summary>
/// Returns the organization row matching the given name, or null.
/// </summary>
public async Task<OrganizationRow?> FindOrganizationByNameAsync(string orgName)
```

### 3.5 New Test Methods

#### Organizations Tests (`tests/Wallow.E2E.Tests/Flows/OrganizationFlowTests.cs`)

All tagged with `[Trait("E2EGroup", "Organizations")]`.

##### Test 1: `OrganizationDetail_NavigateAndShowMembers`

- **Type:** Happy path
- **Base class:** `AuthenticatedE2ETestBase` (or custom setup with org admin)
- **Flow:**
  1. Create org admin user via `TestUserFactory.CreateOrgAdminAsync`
  2. Login, navigate to organizations list
  3. Click the org row to navigate to detail page
  4. Assert `OrganizationDetailPage.IsLoadedAsync()` returns true
  5. Assert org name matches
  6. Assert member list is not empty (at least the admin user)
  7. Assert admin user's email appears in the member list
- **Page objects:** `OrganizationPage`, `OrganizationDetailPage`
- **Assertions:**
  - Detail page loads with correct org name
  - At least one member (the creator) in the list

##### Test 2: `OrganizationDetail_BoundClientsListDisplaysOrEmptyState`

- **Type:** Happy path
- **Base class:** Custom (org admin)
- **Flow:**
  1. Create org admin, login, navigate to org detail
  2. Check bound clients section
  3. Assert either empty state ("No clients bound") or a list of clients
- **Page objects:** `OrganizationDetailPage`
- **Assertions:**
  - Clients section renders without error

##### Test 3: `OrganizationDetail_RegisterClient_AppearsInBoundClients`

- **Type:** Happy path
- **Base class:** Custom (org admin)
- **Flow:**
  1. Create org admin, login, navigate to org detail
  2. Fill register client form with name "e2e-test-client", type "public", redirect URIs
  3. Submit
  4. Assert success result with client ID
  5. Assert new client appears in bound clients list
- **Page objects:** `OrganizationDetailPage`
- **Assertions:**
  - Registration succeeds with client ID
  - Client appears in bound clients table

##### Test 4: `OrganizationDetail_RegisterClientInvalidInput_ShowsErrors`

- **Type:** Negative
- **Base class:** Custom (org admin)
- **Flow:**
  1. Create org admin, login, navigate to org detail
  2. Submit register client form with empty display name
  3. Assert error message displayed
- **Page objects:** `OrganizationDetailPage`
- **Assertions:**
  - Error visible about missing required fields

##### Test 5: `OrganizationsList_ReflectsNewlyCreatedOrg`

- **Type:** Happy path
- **Base class:** Custom (org admin)
- **Flow:**
  1. Create org admin (which creates an org)
  2. Login, navigate to organizations list
  3. Assert the organization appears in the list
  4. Assert name matches
- **Page objects:** `OrganizationPage`
- **Assertions:**
  - New org visible in list with correct name

#### Invitations Tests (`tests/Wallow.E2E.Tests/Flows/InvitationFlowTests.cs`)

All tagged with `[Trait("E2EGroup", "Invitations")]`.

##### Test 6: `InvitationLanding_Unauthenticated_ShowsOrgNameAndOptions`

- **Type:** Happy path
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Create org admin, create invitation for a new email
  2. Navigate to invitation URL (unauthenticated browser)
  3. Assert page loads, shows invitation details
  4. Assert `InvitationLandingPage.IsUnauthenticatedStateAsync()` returns true
  5. Assert "Create account" and "Sign in to accept" buttons visible
- **Page objects:** `InvitationLandingPage`
- **Assertions:**
  - Invitation details visible
  - Unauthenticated options shown

##### Test 7: `InvitationLanding_Authenticated_AcceptAndRedirect`

- **Type:** Happy path
- **Base class:** `E2ETestBase` (manages login manually)
- **Flow:**
  1. Create org admin, create invitation for a known email
  2. Create and login as the invited user
  3. Navigate to invitation URL
  4. Assert `InvitationLandingPage.IsAuthenticatedStateAsync()` returns true
  5. Click accept
  6. Assert redirect to dashboard or home
- **Page objects:** `InvitationLandingPage`
- **Assertions:**
  - Authenticated options shown
  - Accept succeeds, user redirected

##### Test 8: `InvitationLanding_ExpiredToken_ShowsError`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to `/invitation?token=expired-invalid-token-123`
  2. Wait for load
  3. Assert `InvitationLandingPage.HasErrorAsync()` returns true
  4. Assert error message about invalid/expired invitation
- **Page objects:** `InvitationLandingPage`
- **Assertions:**
  - Error displayed about invalid invitation

##### Test 9: `InvitationLanding_InvalidToken_ShowsError`

- **Type:** Negative
- **Base class:** `E2ETestBase`
- **Flow:**
  1. Navigate to `/invitation?token=` (empty or garbage)
  2. Assert error about missing/invalid token
- **Page objects:** `InvitationLandingPage`
- **Assertions:**
  - Error message: "No invitation token provided." or "not valid or has already been used"

##### Test 10: `InvitationLanding_Decline_Redirects`

- **Type:** Negative
- **Base class:** `E2ETestBase` (manages login manually)
- **Flow:**
  1. Create org admin, create invitation
  2. Login as invited user
  3. Navigate to invitation URL
  4. Click decline ("No thanks")
  5. Assert redirect away from invitation page (to `/` or login)
- **Page objects:** `InvitationLandingPage`
- **Assertions:**
  - Navigated away from invitation page

#### Cross-Tagged Tests (`tests/Wallow.E2E.Tests/Flows/OrganizationInvitationFlowTests.cs`)

Tagged with both `[Trait("E2EGroup", "Organizations")]` and `[Trait("E2EGroup", "Invitations")]`.

##### Test 11: `InviteUser_AcceptInvitation_UserAppearsInOrgMemberList`

- **Type:** Happy path
- **Flow:**
  1. Create org admin
  2. Create invitation for a new user's email
  3. Create and verify the invited user
  4. Login as invited user, navigate to invitation URL, accept
  5. Login as org admin, navigate to org detail
  6. Assert invited user's email appears in member list
- **Page objects:** `InvitationLandingPage`, `OrganizationDetailPage`, `LoginPage`
- **Assertions:**
  - Invited user visible in org member list after accepting

##### Test 12: `InviteToMfaRequiredOrg_AcceptInvitation_ForcedIntoMfaEnrollment`

- **Type:** Happy path
- **Flow:**
  1. Create org admin with MFA-required org
  2. Create invitation
  3. Create and verify invited user (no MFA set up)
  4. Login as invited user, accept invitation
  5. On next login, assert redirect to MFA enrollment (since org requires MFA)
- **Page objects:** `InvitationLandingPage`, `MfaEnrollPage`, `LoginPage`
- **Assertions:**
  - After accepting invitation to MFA-required org, user is redirected to MFA enrollment on login

## 4. Regression Verification Commands

```bash
# Phase 1-4 gate
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Auth|E2EGroup=EmailVerification|E2EGroup=MFA|E2EGroup=AltAuth"

# Phase 5 tests
dotnet test tests/Wallow.E2E.Tests --filter "E2EGroup=Organizations|E2EGroup=Invitations"

# Full E2E
./scripts/run-e2e.sh
```

## 5. Complete File List

### Modified Files

| File | Changes |
|------|---------|
| `src/Wallow.Web/Components/Pages/Dashboard/OrganizationDetail.razor` | Add ~23 `data-testid` attributes |
| `src/Wallow.Auth/Components/Pages/InvitationLanding.razor` | Add ~13 `data-testid` attributes |
| `src/Wallow.Web/Components/Pages/Dashboard/Organizations.razor` | Add ~1 `data-testid` for row navigation |
| `tests/Wallow.E2E.Tests/PageObjects/OrganizationPage.cs` | Add `ClickOrganizationByNameAsync`, `FindOrganizationByNameAsync` methods |
| `tests/Wallow.E2E.Tests/Infrastructure/TestUserFactory.cs` | Add `CreateOrgAdminAsync` method and `OrgAdminTestUser` record |

### Created Files

| File | Description |
|------|-------------|
| `tests/Wallow.E2E.Tests/PageObjects/OrganizationDetailPage.cs` | Page object for org detail page |
| `tests/Wallow.E2E.Tests/PageObjects/InvitationLandingPage.cs` | Page object for invitation landing |
| `tests/Wallow.E2E.Tests/Infrastructure/InvitationHelper.cs` | Helper for creating invitations via API |
| `tests/Wallow.E2E.Tests/Flows/OrganizationFlowTests.cs` | 5 organization tests |
| `tests/Wallow.E2E.Tests/Flows/InvitationFlowTests.cs` | 5 invitation tests |
| `tests/Wallow.E2E.Tests/Flows/OrganizationInvitationFlowTests.cs` | 2 cross-tagged tests |

## 6. Implementation Sequence

1. **Add data-testid attributes** to OrganizationDetail.razor (~23), InvitationLanding.razor (~13), Organizations.razor (~1)
2. **Add `CreateOrgAdminAsync`** to TestUserFactory with `OrgAdminTestUser` record
3. **Create `InvitationHelper`** infrastructure class
4. **Add navigation helpers** to `OrganizationPage`
5. **Create `OrganizationDetailPage`** page object
6. **Create `InvitationLandingPage`** page object
7. **Write 5 organization tests** (detail, members, clients, register client, list)
8. **Write 5 invitation tests** (unauthenticated, authenticated accept, expired, invalid, decline)
9. **Write 2 cross-tagged tests** (invite+accept member list, MFA-required org)
10. **Run regression:** all previous phases pass, then `./scripts/run-e2e.sh` — all ~46 tests pass
