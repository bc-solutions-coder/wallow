# E2E Test Overhaul Design

## Problem

The E2E test suite fails consistently. Root causes:

1. **Fragile selectors** — page objects use positional CSS selectors (`input.First`, `select.Nth(1)`) that match hidden elements or break when markup changes
2. **Unreliable Blazor circuit detection** — `WaitForBlazorAsync` uses performance API hacks and hardcoded delays that don't guarantee event handlers are wired
3. **No failure diagnostics** — when tests fail, there are no screenshots, traces, or HTML snapshots to diagnose what happened
4. **Duplicated login setup** — every dashboard test repeats a 6-step UI-driven registration and login flow, multiplying flakiness
5. **No enforced conventions** — nothing prevents writing fragile selectors in new tests

## Design

### 1. Playwright Infrastructure Layer

**Artifact capture on failure:**
- Playwright tracing enabled for every test; trace zip saved on failure
- Screenshot and HTML snapshot captured on failure
- All artifacts written to `tests/Wallow.E2E.Tests/test-results/` (gitignored)
- Video recording available locally via `E2E_VIDEO=1`

**Configuration via environment variables:**

| Variable | Default | Purpose |
|----------|---------|---------|
| `E2E_HEADED` | `false` | Show browser window |
| `E2E_SLOWMO` | `0` | Delay between actions (ms) |
| `E2E_VIDEO` | `false` | Record video (local only) |
| `E2E_TRACING` | `on-failure` | `on-failure`, `always`, or `off` |

**Test base class hierarchy:**

```
E2ETestBase
├── Manages IBrowserContext and IPage lifecycle
├── Starts/stops tracing per test
├── Captures screenshot + HTML + trace on failure
├── Waits for Blazor circuit readiness after navigation
└── AuthenticatedE2ETestBase
    ├── Seeds a verified user via API + Mailpit (no browser)
    ├── Performs single OIDC browser login
    └── Provides authenticated IPage ready for dashboard tests
```

**CI integration:**
- GitHub Actions `upload-artifact@v4` step runs on failure
- Uploads `test-results/` directory with 7-day retention
- Developers download the zip to inspect screenshots and open traces in Playwright Trace Viewer

### 2. `data-testid` Selector Convention

Every interactive or assertable element receives a `data-testid` attribute. Page objects use these exclusively.

**Naming convention:** `{page}-{element}` in kebab-case.

**Login page example:**

```html
<input data-testid="login-email" />
<input data-testid="login-password" />
<input data-testid="login-remember-me" />
<button data-testid="login-submit">Sign in</button>
<div data-testid="login-error">Invalid credentials</div>
<a data-testid="login-forgot-password">Forgot password?</a>
<a data-testid="login-register">Register</a>
```

**What gets a `data-testid`:**
- Form inputs, buttons, selects, textareas
- Error and success alert containers
- Key headings used for page-loaded assertions
- Navigation links exercised by E2E flows

**What does not:**
- Decorative elements, layout containers, icons
- Elements never referenced in tests

**Pages requiring `data-testid` attributes:**
- `Login.razor`
- `Register.razor`
- `ForgotPassword.razor`
- `ResetPassword.razor`
- `MfaEnroll.razor`
- `Dashboard` (Web app — apps list heading)
- `AppRegistration` (Web app — register form)
- `Organizations` (Web app — list/empty state)
- `Inquiry` (Web app — contact form)

### 3. Blazor Circuit Readiness Signal

**App-side:** A `BlazorReadyIndicator` component included in both Auth and Web layouts. After `OnAfterRenderAsync(firstRender: true)`, it sets `data-blazor-ready="true"` on `<body>` via JS interop.

```razor
@code {
    [Inject] private IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("eval",
                "document.body.setAttribute('data-blazor-ready', 'true')");
        }
    }
}
```

This fires only after the SignalR circuit connects and event handlers are wired. Zero overhead, invisible to users.

**Test-side:** Replace all `WaitForBlazorAsync` calls and `ClickAndWaitForNavigationAsync` retry loops with:

```csharp
await page.Locator("body[data-blazor-ready='true']")
    .WaitForAsync(new() { Timeout = 10_000 });
```

Playwright's built-in auto-waiting handles the rest. No JS evaluation, no resource timing hacks, no hardcoded delays.

### 4. Authenticated Test Base & API Seeding

**`TestUserFactory`** creates verified users without a browser:
1. `POST` to the API registration endpoint via `HttpClient`
2. Poll Mailpit HTTP API for the verification email
3. `GET` the verification link via `HttpClient` to confirm the account
4. Return `TestUser` record with email and password

**`AuthenticatedE2ETestBase`** uses the factory, then performs one browser-driven OIDC login:
1. Call `TestUserFactory.CreateVerifiedUserAsync(clientId: "wallow-web-client")`
2. Navigate to `{WebBaseUrl}/authentication/login`
3. Wait for Blazor readiness on the Auth login page
4. Fill credentials, submit, wait for dashboard URL

**Test isolation model:**
- **User isolation:** unique `e2e-{Guid}@test.local` email per test — no collisions
- **Browser isolation:** fresh `IBrowserContext` per test — no cookie leakage
- **Data isolation:** each test creates and queries only its own entities
- **Infrastructure:** shared Postgres/Valkey/Mailpit, wiped only on `docker compose down -v`

### 5. Rewritten Page Objects

All page objects rewritten to use `data-testid` selectors. Example:

```csharp
// AppRegistrationPage — before
await _page.Locator("input").Filter(new() { HasText = "" }).First.FillAsync(name);
await _page.Locator("select").First.SelectOptionAsync(type);

// AppRegistrationPage — after
await _page.Locator("[data-testid='app-register-name']").FillAsync(name);
await _page.Locator("[data-testid='app-register-client-type']").SelectOptionAsync(type);
```

Page objects also gain a `WaitForReadyAsync()` method that waits for both the page-specific heading and Blazor circuit readiness.

### 6. Agent Convention Rule

`.claude/rules/E2E.md` enforces the `data-testid` convention for all future E2E work, prohibiting CSS class selectors and positional matching in page objects.

## Files Changed

### New files
| File | Purpose |
|------|---------|
| `.claude/rules/E2E.md` | E2E testing conventions |
| `tests/Wallow.E2E.Tests/Infrastructure/E2ETestBase.cs` | Base class with tracing, screenshots, lifecycle |
| `tests/Wallow.E2E.Tests/Infrastructure/AuthenticatedE2ETestBase.cs` | Authenticated base with API seeding + OIDC login |
| `tests/Wallow.E2E.Tests/Infrastructure/TestUserFactory.cs` | API-based user creation and verification |

### Modified files
| File | Change |
|------|--------|
| 9 Blazor pages (Auth + Web) | Add `data-testid` attributes |
| 2 layout files (Auth + Web) | Add `BlazorReadyIndicator` component |
| 7 page objects | Rewrite to use `data-testid` selectors |
| 2 test classes | Change base class, simplify setup |
| `PlaywrightFixture.cs` | Simplify to browser lifecycle + config only |
| `.github/workflows/ci.yml` | Add artifact upload on failure |
| `.gitignore` | Add `test-results/` |
