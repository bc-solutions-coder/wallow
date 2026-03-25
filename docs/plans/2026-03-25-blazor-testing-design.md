# Blazor Testing Strategy Design

**Date:** 2026-03-25
**Scope:** Wallow.Auth and Wallow.Web test coverage across four layers

## Overview

Add comprehensive testing for both Blazor Server apps (Wallow.Auth, Wallow.Web) covering service unit tests, bUnit component tests, and Playwright end-to-end tests.

## Testing Layers

### 1. Service Unit Tests

**Wallow.Auth.Tests** (existing — expand):
- Add tests for `ClientBrandingApiClient`
- Existing: `AuthApiClientTests`, `BrandingOptionsTests`

**Wallow.Web.Tests** (new):
- `AppRegistrationService`, `OrganizationApiService`, `InquiryService`
- Test happy paths, error responses (400, 401, 404, 500), model deserialization

**Libraries:** xUnit, FluentAssertions, NSubstitute, RichardSzalay.MockHttp (matches existing pattern).

### 2. bUnit Component Tests

**Approach:** Stub BlazorBlueprint components (`BbButton`, `BbInput`, `BbCard`, etc.) with lightweight test doubles. Stubs are duplicated in each component test project (no shared project).

**Test infrastructure per project:**
- Stub components for BlazorBlueprint in `Stubs/` directory
- Mock services registered via bUnit's `Services` collection (NSubstitute)
- Test `BrandingOptions` instance with known values
- bUnit's built-in `FakeNavigationManager` for navigation assertions

**Wallow.Auth.Component.Tests:**

| Page | Key assertions |
|------|---------------|
| Login | Renders form, submits credentials, shows errors on failure, navigates on success, handles ReturnUrl |
| Register | Multi-field validation (password match, terms checkbox), success → verify email flow, membership request step |
| ForgotPassword | Submits email, shows confirmation |
| ResetPassword | Token from query, password validation, success navigation |
| VerifyEmail / VerifyEmailConfirm | Token handling, success/error states |
| MfaChallenge | Code input, submit, error on bad code |
| MfaEnroll | QR/setup flow, verification |
| InvitationLanding | Loads invitation details, accept/decline |
| AcceptTerms | Checkbox + submit gate |
| Logout | Calls logout, navigates away |
| Terms, Privacy, Home, Error | Basic render tests — correct content present |
| AuthLayout | Branding options reflected (logo, tagline, theme) |
| MfaEnrollmentBanner | Shows/hides based on grace period state |

**Wallow.Web.Component.Tests:**

| Page | Key assertions |
|------|---------------|
| Apps | Lists apps from service, handles empty state |
| RegisterApp | EditForm validation, file upload for logo, submit success/failure |
| Inquiries | Lists inquiries, handles loading/empty states |
| Organizations | Lists orgs |
| OrganizationDetail | Loads detail by ID, shows members |
| Settings | Renders settings form |
| Home | Basic render |
| DashboardLayout | Sidebar nav links present, auth state reflected |
| PublicLayout | Renders without auth |
| RedirectToLogin | Triggers navigation to login URL |

### 3. Playwright E2E Tests

**Infrastructure:**
- `docker-compose.test.yml` spins up full stack: Postgres, Valkey, GarageHQ, Mailpit, API, Auth, Web
- Test fixture handles `docker compose up -d` on suite start, `docker compose down` on teardown
- Microsoft.Playwright NuGet package with xUnit integration

**Test flows:**

| Flow | Steps |
|------|-------|
| Registration → Login | Register → Mailpit verification email → verify → login → dashboard |
| Login → Dashboard | Login with seeded user → see apps/orgs |
| Forgot/Reset Password | Request reset → Mailpit → follow link → new password → login |
| MFA Enrollment | Login → enroll MFA → verify TOTP → subsequent login requires MFA |
| App Registration | Login → register app → see it in list |
| Organization Management | Login → view orgs → view detail with members |
| Inquiry Submission | Submit inquiry → see it listed |
| Auth Redirects | Hit protected Web route → redirected to Auth login → login → back to original page |

**Design decisions:**
- Seed test data via API calls (not direct DB)
- Unique user/tenant per test for isolation
- Mailpit API (`localhost:8025/api/v1/messages`) for email verification
- Page Object Model pattern — one class per page
- Tests tagged `[Trait("Category", "E2E")]` and excluded from default test runs

### 4. CI Pipeline

```
┌─────────────────────────────────────┐
│           CI Trigger                │
└──────────────┬──────────────────────┘
               │
       ┌───────┴───────┐
       │  parallel      │
       ▼               ▼
┌──────────────┐  ┌──────────────────┐
│ Unit +       │  │ Docker build     │
│ Integration  │  │ (API, Auth, Web) │
│ tests        │  │                  │
└──────┬───────┘  └────────┬─────────┘
       │                   │
       └───────┬───────────┘
               │ both must pass
               ▼
       ┌───────────────┐
       │ E2E tests     │
       │ (Playwright)  │
       └───────────────┘
```

- Unit/integration and Docker build run in parallel
- E2E gated on both succeeding — no E2E if either fails
- E2E job: `docker compose up -d`, wait for health checks, run Playwright

## Project Structure

```
tests/
├── Wallow.Auth.Tests/                    # (existing) Auth service unit tests
│   ├── Configuration/
│   ├── Services/
│   └── Wallow.Auth.Tests.csproj
├── Wallow.Auth.Component.Tests/          # (new) Auth bUnit component tests
│   ├── Pages/
│   ├── Layout/
│   ├── Shared/
│   ├── Stubs/
│   └── Wallow.Auth.Component.Tests.csproj
├── Wallow.Web.Tests/                     # (new) Web service unit tests
│   ├── Services/
│   └── Wallow.Web.Tests.csproj
├── Wallow.Web.Component.Tests/           # (new) Web bUnit component tests
│   ├── Pages/
│   ├── Layout/
│   ├── Stubs/
│   └── Wallow.Web.Component.Tests.csproj
├── Wallow.E2E.Tests/                     # (new) Playwright end-to-end tests
│   ├── Fixtures/
│   ├── PageObjects/
│   ├── Flows/
│   └── Wallow.E2E.Tests.csproj
```

## run-tests.sh Integration

New module shorthands:

| Shorthand | Target |
|-----------|--------|
| `auth` | `Wallow.Auth.Tests` (existing) |
| `auth-components` | `Wallow.Auth.Component.Tests` |
| `web` | `Wallow.Web.Tests` |
| `web-components` | `Wallow.Web.Component.Tests` |
| `e2e` | `Wallow.E2E.Tests` |

- Default run (no args) excludes E2E via `--filter "Category!=E2E"`
- `./scripts/run-tests.sh e2e` runs only E2E (expects Docker stack running)
