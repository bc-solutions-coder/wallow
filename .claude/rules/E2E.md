## E2E Test Rules

### Base Class Hierarchy

- **`E2ETestBase`** — unauthenticated tests. Provides `Page`, `Context`, `Docker`, `Playwright` properties. Handles browser context creation, optional video recording, tracing, and failure artifact capture.
- **`AuthenticatedE2ETestBase`** — extends `E2ETestBase`. Creates a verified test user via `TestUserFactory`, logs in through the full OIDC flow, and lands on the dashboard before each test. Exposes `TestUser` property.

### Running E2E Tests

**You MUST run E2E tests when working on E2E test code.** E2E tests require live infrastructure and all three apps running. Use the automated script:

```bash
./scripts/run-e2e.sh
```

This builds the solution once on the host, publishes container images via `dotnet publish /t:PublishContainer`, builds migration bundles, starts the full test stack (`docker-compose.test.yml`), runs E2E tests, and tears everything down. No manual setup required.

**Useful flags:**
- `--no-build` — skip build and image publish (reuse existing images)
- `--keep` — leave containers running after tests (for debugging or re-runs)
- `--headed` — see the browser
- `--video` / `--tracing` — capture test artifacts

**For iterative debugging** (faster feedback loop after first run):

```bash
./scripts/run-e2e.sh --keep             # First run: build + publish + test, leave running
./scripts/run-e2e.sh --no-build --keep  # Re-runs: just run tests against running stack
```

**Manual setup (alternative):**

Start infrastructure, then run all three apps in separate terminals:

```bash
cd docker && docker compose up -d

dotnet run --project src/Wallow.Api      # http://localhost:5001
dotnet run --project src/Wallow.Auth     # http://localhost:5002
dotnet run --project src/Wallow.Web      # http://localhost:5003
```

Run tests pointing at local ports:

```bash
E2E_EXTERNAL_SERVICES=true \
  E2E_BASE_URL=http://localhost:5001 \
  E2E_AUTH_URL=http://localhost:5002 \
  E2E_WEB_URL=http://localhost:5003 \
  ./scripts/run-tests.sh e2e
```

Code changes take effect immediately with manual setup — no container rebuild needed.

**CI (Docker containers):**

CI builds container images and uses `docker/docker-compose.test.yml`. The default URL values in `E2ETestBase` (5050/5051/5053) target those containers. `E2E_EXTERNAL_SERVICES=true` is set automatically in CI so the test runner skips docker compose up/down.

### Test Isolation

- Each test creates its own unique user via `TestUserFactory` with a random email (`e2e-{guid}@test.local`) — tests never share user state.
- Tests must not depend on or be affected by other users in the system.
- Use `TestUserFactory.CreateAsync` / `CreateWithMfaAsync` / `CreateInMfaRequiredOrgAsync` for test-specific users.
- `E2ETestBase.InitializeAsync` creates a fresh browser context per test — no cookie or session leakage between tests.

### Selectors

- **ALWAYS** use `data-testid` attributes: `page.GetByTestId("login-email")`
- **NEVER** use raw `#id`, CSS class (`.btn-primary`), or text-based (`button:has-text('Sign in')`) selectors
- **Naming convention**: `{page}-{element}` in kebab-case (e.g., `login-email`, `login-password`, `login-submit`, `dashboard-welcome`, `register-email`)

### Environment Variables

| Variable | Type | Default | Purpose |
|----------|------|---------|---------|
| `E2E_HEADED` | bool | `false` | Run browser in headed mode |
| `E2E_SLOWMO` | int (ms) | none | Slow down Playwright operations |
| `E2E_VIDEO` | any | unset | Record video of test runs to `test-results/videos/` |
| `E2E_TRACING` | any | unset | Enable Playwright tracing; saved on failure to `test-results/failures/` |
| `E2E_BASE_URL` | string | `http://localhost:5050` | API base URL (CI Docker default; local default is 5001) |
| `E2E_AUTH_URL` | string | `http://localhost:5051` | Auth app base URL (CI Docker default; local default is 5002) |
| `E2E_WEB_URL` | string | `http://localhost:5053` | Web app base URL (CI Docker default; local default is 5003) |
| `E2E_MAILPIT_URL` | string | `http://localhost:8035` | Mailpit base URL |
| `E2E_EXTERNAL_SERVICES` | any | unset | Skip docker compose up/down; set this when running against already-running services |

### Blazor Readiness

Use `WaitForBlazorReadyAsync(page)` which waits for `[data-blazor-ready='true']` on the page. This attribute must be emitted by Blazor components after the SignalR circuit connects.

### Test User Creation

Use `TestUserFactory.CreateAsync(apiBaseUrl, mailpitBaseUrl)` to create verified test users via the API. Do not register users through the browser UI unless testing the registration flow itself.
