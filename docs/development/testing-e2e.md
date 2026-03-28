# E2E Testing Guide

This guide covers the end-to-end testing infrastructure, patterns, and debugging techniques for Wallow. E2E tests drive a Chromium browser via Playwright against the full running application stack.

## Prerequisites

- .NET 10 SDK (see `global.json` for exact version)
- Docker (for the test compose stack)
- Playwright browsers:

```bash
dotnet build tests/Wallow.E2E.Tests
pwsh tests/Wallow.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
```

> PowerShell is required. Install via `brew install powershell` (macOS) or the [Microsoft guide](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-linux) (Linux).

## Running E2E Tests

```bash
./scripts/run-tests.sh e2e
```

E2E tests are excluded from the default `./scripts/run-tests.sh` run and must be requested explicitly.

To run a specific test class or method:

```bash
dotnet test tests/Wallow.E2E.Tests --settings tests/coverage.runsettings \
  --filter "FullyQualifiedName~AuthFlowTests"
```

## Project Structure

```
tests/Wallow.E2E.Tests/
├── Fixtures/                   # xUnit fixtures for Docker and Playwright lifecycle
│   ├── DockerComposeFixture.cs
│   └── PlaywrightFixture.cs
├── Infrastructure/             # Base classes, helpers, test user management
│   ├── E2ETestBase.cs
│   ├── AuthenticatedE2ETestBase.cs
│   ├── TestUserFactory.cs
│   ├── TestUser.cs
│   ├── MfaTestUser.cs
│   ├── MailpitHelper.cs
│   └── TotpHelper.cs
├── PageObjects/                # Page Object Model classes
│   ├── LoginPage.cs
│   ├── RegisterPage.cs
│   ├── DashboardPage.cs
│   ├── MfaEnrollPage.cs
│   ├── MfaChallengePage.cs
│   ├── SettingsMfaSection.cs
│   ├── AppRegistrationPage.cs
│   ├── OrganizationPage.cs
│   └── InquiryPage.cs
├── Flows/                      # Test classes organized by user journey
│   ├── AuthFlowTests.cs
│   ├── MfaFlowTests.cs
│   └── DashboardFlowTests.cs
└── xunit.runner.json           # Disables parallel test execution
```

## Fixtures

### DockerComposeFixture

Manages the Docker Compose test environment lifecycle via `IAsyncLifetime`.

**Initialization:**

1. If `E2E_EXTERNAL_SERVICES` is set, skips `docker compose up` (used in CI).
2. Otherwise, checks if services are already healthy by hitting the API health endpoint.
3. If not running, starts via `docker compose -f docker/docker-compose.test.yml up -d --build`.
4. Waits for all three services (API, Auth, Web) to report healthy (5-minute timeout, 3-second poll).

**Teardown:** Runs `docker compose down -v` unless services are externally managed.

**Service URLs:**

| Property | Default | Env Var Override |
|----------|---------|-----------------|
| `ApiBaseUrl` | `http://localhost:5050` | `E2E_BASE_URL` |
| `AuthBaseUrl` | `http://localhost:5051` | `E2E_AUTH_URL` |
| `WebBaseUrl` | `http://localhost:5053` | `E2E_WEB_URL` |
| `MailpitBaseUrl` | `http://localhost:8035` | `E2E_MAILPIT_URL` |

### PlaywrightFixture

Creates a single shared Chromium browser instance. Reads `E2E_HEADED` and `E2E_SLOWMO` for debug configuration. Provides `CreateBrowserContextAsync(recordVideo)` for isolated browser contexts.

## Base Classes

### E2ETestBase -- Unauthenticated Tests

Use for tests that manage their own authentication or test unauthenticated flows (login, registration, forgot password).

**Provides:** `Page`, `Context`, `Docker`, `Playwright` properties.

**Lifecycle per test:**

1. Creates a new browser context (with video if `E2E_VIDEO` is set), starts tracing if `E2E_TRACING` is set, opens a new page.
2. Test runs.
3. If `MarkTestFailed()` was called, saves failure artifacts (screenshot, HTML, trace). Closes page and disposes context.

### AuthenticatedE2ETestBase -- Authenticated Tests

Extends `E2ETestBase` for tests needing a logged-in user on the dashboard.

**Additional setup:** Creates a verified test user via `TestUserFactory`, navigates to the Web app's OIDC login endpoint, fills credentials, waits for redirect to dashboard.

**Additional property:** `TestUser` with `Email` and `Password`.

## Page Object Pattern

Each UI page has a page object in `PageObjects/` that encapsulates all interactions.

**Conventions:**

- Constructor takes `IPage` and a base URL string
- `NavigateAsync` goes to the page and waits for readiness
- `IsLoadedAsync` returns whether the page's key element is present
- All selectors use `data-testid` attributes -- never CSS classes, raw IDs, or text
- Naming convention: `{page}-{element}` in kebab-case (e.g., `login-email`, `mfa-challenge-code`)

## Test User Creation

`TestUserFactory` creates verified test users via the API (not through the browser UI).

**`CreateAsync`** -- Standard user: registers via API, polls Mailpit for verification email (15 retries, 2s apart), extracts and visits the verification link. Returns `TestUser(Email, Password)`.

**`CreateWithMfaAsync`** -- User with MFA: creates a standard user, logs in via API, enrolls TOTP, confirms with a generated code. Returns `MfaTestUser(Email, Password, TotpSecret, BackupCodes)`.

**`CreateInMfaRequiredOrgAsync`** -- User in MFA-required org: creates a standard user, logs in, creates an org with MFA required and configurable grace period. Returns `MfaTestUser` (no secret/codes yet since MFA is not enrolled).

## Blazor Readiness

After navigating to a Blazor page, call `WaitForBlazorReadyAsync(page)`:

1. Waits for `[data-blazor-ready='true']` (set by `BlazorReadyIndicator` component after SignalR circuit connects).
2. Fallback: polls for the `Blazor` JavaScript global with a 500ms delay for circuit initialization.

Default timeout is 15 seconds.

## Test Flow Structure

Tests are organized in `Flows/` by user journey:

- **`AuthFlowTests`** (`E2ETestBase`) -- login, forgot password, auth redirects
- **`MfaFlowTests`** (`E2ETestBase`) -- MFA enrollment, TOTP challenge, backup codes, grace period
- **`DashboardFlowTests`** (`AuthenticatedE2ETestBase`) -- settings, app registration, orgs, inquiries

Every test uses `try/catch` with `MarkTestFailed()` in the catch block to ensure failure artifacts are captured.

## Environment Variables

| Variable | Type | Default | Purpose |
|----------|------|---------|---------|
| `E2E_HEADED` | bool | `false` | Run browser in headed (visible) mode |
| `E2E_SLOWMO` | int (ms) | none | Slow down every Playwright operation |
| `E2E_VIDEO` | any | unset | Record video to `test-results/videos/` |
| `E2E_TRACING` | any | unset | Enable Playwright tracing; saved on failure |
| `E2E_BASE_URL` | string | `http://localhost:5050` | API base URL |
| `E2E_AUTH_URL` | string | `http://localhost:5051` | Auth app base URL |
| `E2E_WEB_URL` | string | `http://localhost:5053` | Web app base URL |
| `E2E_MAILPIT_URL` | string | `http://localhost:8035` | Mailpit base URL |
| `E2E_EXTERNAL_SERVICES` | any | unset | Skip docker compose up/down |

## Failure Artifacts

When `MarkTestFailed()` is called, `DisposeAsync` saves:

- **Screenshot:** `test-results/failures/{TestClass}_{timestamp}/screenshot.png`
- **Page HTML:** `test-results/failures/{TestClass}_{timestamp}/page.html`
- **Trace:** `test-results/failures/{TestClass}_{timestamp}/trace.zip` (if `E2E_TRACING` is set)

Videos (when `E2E_VIDEO` is set) save to `test-results/videos/` regardless of outcome.

## Writing a New E2E Test

**1. Create or reuse a page object.** Add a class in `PageObjects/` with `data-testid` selectors. Call `WaitForBlazorReadyAsync` after navigation.

**2. Add `data-testid` attributes to Blazor components.** Use `{page}-{element}` kebab-case naming.

**3. Create the test class.** Choose `E2ETestBase` (manages own auth) or `AuthenticatedE2ETestBase` (needs logged-in user). Wrap test body in `try/catch` with `MarkTestFailed()`.

**4. Run:** `./scripts/run-tests.sh e2e`

## Test Parallelism

E2E tests run sequentially (`xunit.runner.json` disables parallel execution) to prevent race conditions from multiple browsers hitting the same services.

## Debugging Failed Tests

### Headed Mode

Watch the browser:

```bash
E2E_HEADED=true ./scripts/run-tests.sh e2e
```

### Slow Motion

Slow down operations (250-500ms is a good start):

```bash
E2E_HEADED=true E2E_SLOWMO=500 ./scripts/run-tests.sh e2e
```

### Video Recording

```bash
E2E_VIDEO=1 ./scripts/run-tests.sh e2e
```

### Playwright Tracing

```bash
E2E_TRACING=1 ./scripts/run-tests.sh e2e
```

View a trace:

```bash
pwsh tests/Wallow.E2E.Tests/bin/Debug/net10.0/playwright.ps1 show-trace \
  tests/Wallow.E2E.Tests/test-results/failures/AuthFlowTests_20260328_120000/trace.zip
```

Or use the [Playwright Trace Viewer](https://trace.playwright.dev/) web UI.

### Maximum Debug Visibility

```bash
E2E_HEADED=true E2E_SLOWMO=300 E2E_VIDEO=1 E2E_TRACING=1 ./scripts/run-tests.sh e2e
```

### Common Failure Patterns

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Timeout waiting for `data-blazor-ready` | Blazor circuit failed to connect | Check service health; inspect browser console for SignalR errors |
| Timeout waiting for `data-testid` | Element not rendered or wrong testid | Verify attribute in Blazor component; check page HTML artifact |
| Services not healthy within timeout | Container failed to start | Check `docker compose -f docker/docker-compose.test.yml logs` |
| OIDC redirect loops | Issuer mismatch | Verify `OpenIddict__Issuer` matches browser URL (`http://localhost:5050`) |
| Email verification fails | Mailpit not receiving emails | Check `Smtp__Host` points to `mailpit` with correct port |
| `host.docker.internal` not resolving | Missing hosts entry (Linux) | `echo "127.0.0.1 host.docker.internal" | sudo tee -a /etc/hosts` |
