# E2E Test Overhaul Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the fragile E2E test infrastructure with durable Playwright patterns — `data-testid` selectors, proper Blazor readiness signals, failure diagnostics, and authenticated test base classes.

**Architecture:** Add `data-testid` attributes to all Blazor pages tested by E2E. Replace hacky Blazor circuit detection with a `BlazorReadyIndicator` component that sets a body attribute. Build `E2ETestBase` and `AuthenticatedE2ETestBase` classes that handle tracing, screenshots, and OIDC login. Rewrite all page objects and tests to use the new infrastructure.

**Tech Stack:** Playwright for .NET, xUnit `IAsyncLifetime`, Blazor Server InteractiveServer, OIDC + OpenIddict

**Design doc:** `docs/plans/2026-03-26-e2e-test-overhaul-design.md`

---

### Task 1: Add E2E testing convention rule

**Files:**
- Create: `.claude/rules/E2E.md`

**Step 1: Create the rule file**

```markdown
## E2E Testing Rules

- **Always use `data-testid` selectors** in page objects — never CSS classes, positional selectors (`Locator("input").First`), or text content matching for element identification
- **Naming convention:** `data-testid="{page}-{element}"` in kebab-case (e.g., `login-email`, `register-submit`, `app-register-name`)
- **Every interactive or assertable element** in Blazor pages tested by E2E must have a `data-testid` attribute
- **Blazor readiness:** Always wait for `body[data-blazor-ready='true']` before interacting with forms — never use `WaitForLoadStateAsync(LoadState.NetworkIdle)` as the sole readiness signal for interactive pages
- **Page objects:** Each page object must use `[data-testid='...']` locators exclusively. Use `_page.Locator("[data-testid='login-email']")`, not `_page.Locator("#email")`
- **Test base classes:** Auth flow tests extend `E2ETestBase`; dashboard/authenticated tests extend `AuthenticatedE2ETestBase`
- **No hardcoded delays:** Never use `WaitForTimeoutAsync` or `Task.Delay` in tests for synchronization. Use Playwright's built-in auto-waiting and explicit waiters (`WaitForAsync`, `WaitForURLAsync`)
```

**Step 2: Commit**

```bash
git add .claude/rules/E2E.md
git commit -m "docs: add E2E testing convention rule for data-testid selectors"
```

---

### Task 2: Add BlazorReadyIndicator component to Wallow.Auth

**Files:**
- Create: `src/Wallow.Auth/Components/Shared/BlazorReadyIndicator.razor`
- Modify: `src/Wallow.Auth/Components/Layout/AuthLayout.razor`

**Step 1: Create the BlazorReadyIndicator component**

```razor
@rendermode InteractiveServer
@inject IJSRuntime JS

@code {
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

**Step 2: Add BlazorReadyIndicator to AuthLayout.razor**

In `src/Wallow.Auth/Components/Layout/AuthLayout.razor`, add inside the outer `<div>`, just before `@Body`:

```razor
<BlazorReadyIndicator />
```

Add the `@using` directive at the top if needed:
```razor
@using Wallow.Auth.Components.Shared
```

**Step 3: Commit**

```bash
git add src/Wallow.Auth/Components/Shared/BlazorReadyIndicator.razor src/Wallow.Auth/Components/Layout/AuthLayout.razor
git commit -m "feat(auth): add BlazorReadyIndicator component for E2E circuit detection"
```

---

### Task 3: Add BlazorReadyIndicator to Wallow.Web

**Files:**
- Create: `src/Wallow.Web/Components/Shared/BlazorReadyIndicator.razor`
- Modify: `src/Wallow.Web/Components/Layout/DashboardLayout.razor`

**Step 1: Create the BlazorReadyIndicator component**

Same content as Task 2, but in the Web project namespace:

```razor
@rendermode InteractiveServer
@inject IJSRuntime JS

@code {
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

**Step 2: Add to DashboardLayout.razor**

In `src/Wallow.Web/Components/Layout/DashboardLayout.razor`, add inside the outer `<div class="min-h-screen flex bg-[#faf6f1]">`, before the sidebar:

```razor
<BlazorReadyIndicator />
```

Add `@using Wallow.Web.Components.Shared` if needed.

**Step 3: Commit**

```bash
git add src/Wallow.Web/Components/Shared/BlazorReadyIndicator.razor src/Wallow.Web/Components/Layout/DashboardLayout.razor
git commit -m "feat(web): add BlazorReadyIndicator component for E2E circuit detection"
```

---

### Task 4: Add `data-testid` attributes to Auth Blazor pages

**Files:**
- Modify: `src/Wallow.Auth/Components/Pages/Login.razor`
- Modify: `src/Wallow.Auth/Components/Pages/Register.razor`
- Modify: `src/Wallow.Auth/Components/Pages/ForgotPassword.razor`
- Modify: `src/Wallow.Auth/Components/Pages/ResetPassword.razor`
- Modify: `src/Wallow.Auth/Components/Pages/MfaEnroll.razor`

**Step 1: Add data-testid to Login.razor**

Add `data-testid` attributes alongside existing `Id` attributes. The `BbInput` component renders `Id` as the HTML `id` attribute. Add `data-testid` via the component's additional attributes or by wrapping. Since `BbInput` likely passes through additional attributes, add them directly:

| Element | Current | Add |
|---------|---------|-----|
| Email input (password tab) | `Id="email"` | `data-testid="login-email"` |
| Password input | `Id="password"` | `data-testid="login-password"` |
| Remember me checkbox | `Id="rememberMe"` | `data-testid="login-remember-me"` |
| Sign in button | `Type="ButtonType.Submit"` | `data-testid="login-submit"` |
| Error alert | `Variant="AlertVariant.Danger"` | Wrap in `<div data-testid="login-error">` |
| Success alert | `Variant="AlertVariant.Success"` | Wrap in `<div data-testid="login-success">` |
| Forgot password link | `href="/forgot-password"` | `data-testid="login-forgot-password"` |
| Register link | `href="@RegisterUrl"` | `data-testid="login-register-link"` |
| Page title card | `BbCardTitle` | Wrap in `<div data-testid="login-heading">` |
| Magic link email | `Id="magicLinkEmail"` | `data-testid="login-magic-link-email"` |
| Magic link submit | Send link button | `data-testid="login-magic-link-submit"` |
| OTP email | `Id="otpEmail"` | `data-testid="login-otp-email"` |
| OTP code | `Id="otpCode"` | `data-testid="login-otp-code"` |
| OTP send button | Send code button | `data-testid="login-otp-send"` |
| OTP verify button | Verify code button | `data-testid="login-otp-verify"` |

**Step 2: Add data-testid to Register.razor**

| Element | Add |
|---------|-----|
| Page heading (`BbCardTitle`) | Wrap: `<div data-testid="register-heading">` |
| Email input | `data-testid="register-email"` |
| Password input | `data-testid="register-password"` |
| Confirm password | `data-testid="register-confirm-password"` |
| Passwordless checkbox | `data-testid="register-passwordless"` |
| Terms checkbox | `data-testid="register-terms"` |
| Privacy checkbox | `data-testid="register-privacy"` |
| Submit button | `data-testid="register-submit"` |
| Error alert | Wrap: `<div data-testid="register-error">` |
| Sign in link | `data-testid="register-login-link"` |

**Step 3: Add data-testid to ForgotPassword.razor**

| Element | Add |
|---------|-----|
| Email input | `data-testid="forgot-password-email"` (keep existing `id="forgot-email"`) |
| Submit button | `data-testid="forgot-password-submit"` |
| Success alert | Wrap: `<div data-testid="forgot-password-success">` |
| Back to sign in link | `data-testid="forgot-password-back"` |

**Step 4: Add data-testid to ResetPassword.razor**

| Element | Add |
|---------|-----|
| New password input (`id="new-password"`) | `data-testid="reset-password-new"` |
| Confirm password input (`id="confirm-password"`) | `data-testid="reset-password-confirm"` |
| Submit button | `data-testid="reset-password-submit"` |
| Error alert | Wrap: `<div data-testid="reset-password-error">` |
| Back to sign in link | `data-testid="reset-password-back"` |

**Step 5: Add data-testid to MfaEnroll.razor**

| Element | Add |
|---------|-----|
| Page heading | Wrap: `<div data-testid="mfa-enroll-heading">` |
| Begin setup button | `data-testid="mfa-enroll-begin"` |
| Secret display (`font-mono text-sm`) | `data-testid="mfa-enroll-secret"` |
| QR URI display | `data-testid="mfa-enroll-qr-uri"` |
| Code input (`Id="code"`) | `data-testid="mfa-enroll-code"` |
| Verify submit button | `data-testid="mfa-enroll-verify"` |
| Error alert | Wrap: `<div data-testid="mfa-enroll-error">` |
| Success alert | Wrap: `<div data-testid="mfa-enroll-success">` |
| Backup codes container | `data-testid="mfa-enroll-backup-codes"` |
| Done button | `data-testid="mfa-enroll-done"` |
| Cancel link | `data-testid="mfa-enroll-cancel"` |

**Step 6: Verify the Auth app builds**

Run: `dotnet build src/Wallow.Auth`
Expected: Build succeeded

**Step 7: Commit**

```bash
git add src/Wallow.Auth/Components/Pages/Login.razor src/Wallow.Auth/Components/Pages/Register.razor src/Wallow.Auth/Components/Pages/ForgotPassword.razor src/Wallow.Auth/Components/Pages/ResetPassword.razor src/Wallow.Auth/Components/Pages/MfaEnroll.razor
git commit -m "feat(auth): add data-testid attributes to all auth Blazor pages"
```

---

### Task 5: Add `data-testid` attributes to Web Blazor pages

**Files:**
- Modify: `src/Wallow.Web/Components/Pages/Dashboard/Apps.razor`
- Modify: `src/Wallow.Web/Components/Pages/Dashboard/RegisterApp.razor`
- Modify: `src/Wallow.Web/Components/Pages/Dashboard/Organizations.razor`
- Modify: `src/Wallow.Web/Components/Pages/Dashboard/Inquiries.razor`

**Step 1: Add data-testid to Apps.razor (Dashboard)**

| Element | Add |
|---------|-----|
| Page heading (`h1` "My Apps") | `data-testid="dashboard-heading"` |
| Register New App link | `data-testid="dashboard-register-app"` |
| Empty state container | `data-testid="dashboard-empty-state"` |
| Apps table | `data-testid="dashboard-apps-table"` |
| Loading indicator | `data-testid="dashboard-loading"` |

**Step 2: Add data-testid to RegisterApp.razor**

| Element | Add |
|---------|-----|
| Page heading (`h1` "Register New App") | `data-testid="app-register-heading"` |
| App name input (`InputText` for DisplayName) | `data-testid="app-register-name"` |
| Client type select (`InputSelect` for ClientType) | `data-testid="app-register-client-type"` |
| Redirect URIs textarea | `data-testid="app-register-redirect-uris"` |
| Branding display name input | `data-testid="app-register-branding-name"` |
| Branding tagline input | `data-testid="app-register-branding-tagline"` |
| Submit button | `data-testid="app-register-submit"` |
| Success heading ("App Registered Successfully") | `data-testid="app-register-success"` |
| Client ID code element | `data-testid="app-register-client-id"` |
| Client secret container | `data-testid="app-register-client-secret"` |
| Error container | `data-testid="app-register-error"` |
| Back to Apps link | `data-testid="app-register-back"` |

For `InputText` and `InputSelect`, use `AdditionalAttributes` or the built-in attribute splatting. Blazor's `InputText` passes through unmatched attributes to the rendered `<input>`:

```razor
<InputText @bind-Value="_form.DisplayName"
           data-testid="app-register-name"
           class="..." placeholder="My Application" />
```

**Step 3: Add data-testid to Organizations.razor**

| Element | Add |
|---------|-----|
| Page heading | `data-testid="organizations-heading"` |
| Empty state container | `data-testid="organizations-empty-state"` |
| Organizations table | `data-testid="organizations-table"` |
| Loading indicator | `data-testid="organizations-loading"` |

**Step 4: Add data-testid to Inquiries.razor**

| Element | Add |
|---------|-----|
| Page heading | `data-testid="inquiry-heading"` |
| Name input | `data-testid="inquiry-name"` |
| Email input | `data-testid="inquiry-email"` |
| Phone input | `data-testid="inquiry-phone"` |
| Company input | `data-testid="inquiry-company"` |
| Project type select | `data-testid="inquiry-project-type"` |
| Budget select | `data-testid="inquiry-budget"` |
| Timeline select | `data-testid="inquiry-timeline"` |
| Message textarea | `data-testid="inquiry-message"` |
| Submit button | `data-testid="inquiry-submit"` |
| Success heading ("Inquiry Submitted") | `data-testid="inquiry-success"` |
| Error container | `data-testid="inquiry-error"` |
| Submit Another button | `data-testid="inquiry-submit-another"` |

**Step 5: Verify the Web app builds**

Run: `dotnet build src/Wallow.Web`
Expected: Build succeeded

**Step 6: Commit**

```bash
git add src/Wallow.Web/Components/Pages/Dashboard/Apps.razor src/Wallow.Web/Components/Pages/Dashboard/RegisterApp.razor src/Wallow.Web/Components/Pages/Dashboard/Organizations.razor src/Wallow.Web/Components/Pages/Dashboard/Inquiries.razor
git commit -m "feat(web): add data-testid attributes to all dashboard Blazor pages"
```

---

### Task 6: Create E2ETestBase infrastructure

**Files:**
- Create: `tests/Wallow.E2E.Tests/Infrastructure/E2ETestBase.cs`
- Modify: `tests/Wallow.E2E.Tests/Fixtures/PlaywrightFixture.cs`

**Step 1: Rewrite PlaywrightFixture to just manage browser lifecycle and config**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.Fixtures;

public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();

        bool headed = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("E2E_HEADED"));
        float slowMo = float.TryParse(Environment.GetEnvironmentVariable("E2E_SLOWMO"), out float s) ? s : 0;

        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !headed,
            SlowMo = slowMo,
        });
    }

    public async Task<IBrowserContext> CreateBrowserContextAsync(bool recordVideo = false)
    {
        BrowserNewContextOptions options = new();

        if (recordVideo)
        {
            options.RecordVideoDir = Path.Combine("test-results", "videos");
        }

        return await Browser.NewContextAsync(options);
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }
}
```

**Step 2: Create E2ETestBase**

```csharp
using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;

namespace Wallow.E2E.Tests.Infrastructure;

public abstract class E2ETestBase : IClassFixture<DockerComposeFixture>, IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private ITracingStopOptions? _tracingStopOptions;

    protected DockerComposeFixture Docker { get; }
    protected IPage Page { get; private set; } = null!;

    protected E2ETestBase(DockerComposeFixture docker, PlaywrightFixture playwright)
    {
        Docker = docker;
        _playwright = playwright;
    }

    public virtual async Task InitializeAsync()
    {
        bool recordVideo = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("E2E_VIDEO"));
        _context = await _playwright.CreateBrowserContextAsync(recordVideo);

        // Start tracing for failure diagnostics
        string tracingSetting = Environment.GetEnvironmentVariable("E2E_TRACING") ?? "on-failure";
        if (tracingSetting is not "off")
        {
            await _context.Tracing.StartAsync(new()
            {
                Screenshots = true,
                Snapshots = true,
                Sources = false,
            });
        }

        Page = await _context.NewPageAsync();
    }

    public virtual async Task DisposeAsync()
    {
        await Page.CloseAsync();

        // Tracing is stopped in CaptureFailureArtifactsAsync if test failed,
        // otherwise stop and discard here
        string tracingSetting = Environment.GetEnvironmentVariable("E2E_TRACING") ?? "on-failure";
        if (tracingSetting == "always")
        {
            string traceDir = Path.Combine("test-results", "traces");
            Directory.CreateDirectory(traceDir);
            string tracePath = Path.Combine(traceDir, $"{GetType().Name}-{Guid.NewGuid():N}.zip");
            await _context.Tracing.StopAsync(new() { Path = tracePath });
        }
        else
        {
            try { await _context.Tracing.StopAsync(); } catch { /* tracing may not have started */ }
        }

        await _context.DisposeAsync();
    }

    /// <summary>
    /// Call this in a catch block or test teardown when a test fails.
    /// Captures screenshot, HTML snapshot, and Playwright trace.
    /// </summary>
    protected async Task CaptureFailureArtifactsAsync(string testName)
    {
        string artifactDir = Path.Combine("test-results", testName);
        Directory.CreateDirectory(artifactDir);

        // Screenshot
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactDir, "failure.png"),
            FullPage = true,
        });

        // HTML snapshot
        string html = await Page.ContentAsync();
        await File.WriteAllTextAsync(Path.Combine(artifactDir, "failure.html"), html);

        // Trace
        await _context.Tracing.StopAsync(new()
        {
            Path = Path.Combine(artifactDir, "trace.zip"),
        });
    }

    /// <summary>
    /// Waits for the Blazor SignalR circuit to connect.
    /// The BlazorReadyIndicator component sets data-blazor-ready="true" on body
    /// after OnAfterRenderAsync(firstRender: true).
    /// </summary>
    protected static async Task WaitForBlazorReadyAsync(IPage page, int timeoutMs = 10_000)
    {
        await page.Locator("body[data-blazor-ready='true']")
            .WaitForAsync(new() { Timeout = timeoutMs });
    }

    /// <summary>
    /// Navigates to a URL and waits for Blazor circuit readiness.
    /// Use for pages with InteractiveServer rendermode.
    /// </summary>
    protected async Task NavigateAndWaitForBlazorAsync(string url)
    {
        await Page.GotoAsync(url);
        await WaitForBlazorReadyAsync(Page);
    }
}
```

**Step 3: Verify the E2E project builds**

Run: `dotnet build tests/Wallow.E2E.Tests`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add tests/Wallow.E2E.Tests/Infrastructure/E2ETestBase.cs tests/Wallow.E2E.Tests/Fixtures/PlaywrightFixture.cs
git commit -m "feat(e2e): add E2ETestBase with tracing, screenshots, and Blazor readiness"
```

---

### Task 7: Create TestUserFactory and AuthenticatedE2ETestBase

**Files:**
- Create: `tests/Wallow.E2E.Tests/Infrastructure/TestUserFactory.cs`
- Create: `tests/Wallow.E2E.Tests/Infrastructure/AuthenticatedE2ETestBase.cs`

**Step 1: Create TestUserFactory**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wallow.E2E.Tests.Infrastructure;

public sealed record TestUser(string Email, string Password);

public sealed class TestUserFactory
{
    private readonly string _apiBaseUrl;
    private readonly string _mailpitBaseUrl;

    public TestUserFactory(string apiBaseUrl, string mailpitBaseUrl)
    {
        _apiBaseUrl = apiBaseUrl;
        _mailpitBaseUrl = mailpitBaseUrl;
    }

    /// <summary>
    /// Creates a new user account via the API and verifies the email via Mailpit.
    /// No browser needed — uses HttpClient for everything.
    /// </summary>
    public async Task<TestUser> CreateVerifiedUserAsync(string? clientId = null)
    {
        string email = $"e2e-{Guid.NewGuid():N}@test.local";
        string password = "P@ssw0rd!Strong12";

        using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };

        // Register via API
        object registerPayload = new
        {
            email,
            password,
            confirmPassword = password,
            clientId,
            loginMethod = (string?)null,
            returnUrl = (string?)null,
        };

        HttpResponseMessage registerResponse = await http.PostAsJsonAsync(
            $"{_apiBaseUrl}/api/v1/identity/auth/register", registerPayload);
        registerResponse.EnsureSuccessStatusCode();

        // Poll Mailpit for verification email and visit the link
        string verificationLink = await GetVerificationLinkAsync(http, email);
        if (!string.IsNullOrEmpty(verificationLink))
        {
            await http.GetAsync(verificationLink);
        }

        return new TestUser(email, password);
    }

    private async Task<string> GetVerificationLinkAsync(HttpClient http, string email)
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            HttpResponseMessage response = await http.GetAsync(
                $"{_mailpitBaseUrl}/api/v1/search?query=to:{email}");

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                MailpitSearchResult? result = JsonSerializer.Deserialize<MailpitSearchResult>(json);

                if (result?.Messages is { Count: > 0 })
                {
                    string messageId = result.Messages[0].Id;
                    HttpResponseMessage msgResponse = await http.GetAsync(
                        $"{_mailpitBaseUrl}/api/v1/message/{messageId}");

                    if (msgResponse.IsSuccessStatusCode)
                    {
                        MailpitMessage? message = await msgResponse.Content
                            .ReadFromJsonAsync<MailpitMessage>();
                        string body = message?.Text ?? message?.Html ?? string.Empty;

                        string? link = ExtractLinkContaining(body, "verify")
                            ?? ExtractLinkContaining(body, "confirm");
                        if (link is not null)
                        {
                            return link;
                        }
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return string.Empty;
    }

    private static string? ExtractLinkContaining(string body, string keyword)
    {
        int searchIndex = 0;
        while (searchIndex < body.Length)
        {
            int httpIndex = body.IndexOf("http", searchIndex, StringComparison.OrdinalIgnoreCase);
            if (httpIndex < 0) break;

            int endIndex = body.IndexOfAny([' ', '"', '\'', '<', '\n', '\r'], httpIndex);
            if (endIndex < 0) endIndex = body.Length;

            string url = body[httpIndex..endIndex];
            if (url.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            searchIndex = endIndex;
        }

        return null;
    }

    private sealed class MailpitSearchResult
    {
        [JsonPropertyName("messages")]
        public List<MailpitMessageSummary> Messages { get; set; } = [];
    }

    private sealed class MailpitMessageSummary
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;
    }

    private sealed class MailpitMessage
    {
        [JsonPropertyName("Text")]
        public string? Text { get; set; }

        [JsonPropertyName("HTML")]
        public string? Html { get; set; }
    }
}
```

**Step 2: Create AuthenticatedE2ETestBase**

```csharp
using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;

namespace Wallow.E2E.Tests.Infrastructure;

/// <summary>
/// Base class for tests that need an authenticated user session.
/// Seeds a user via the API (no browser), then performs a single OIDC login.
/// </summary>
public abstract class AuthenticatedE2ETestBase : E2ETestBase
{
    private static readonly string MailpitBaseUrl =
        Environment.GetEnvironmentVariable("E2E_MAILPIT_URL") ?? "http://localhost:8035";

    protected TestUser User { get; private set; } = null!;

    protected AuthenticatedE2ETestBase(DockerComposeFixture docker, PlaywrightFixture playwright)
        : base(docker, playwright)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Seed user via API (no browser interaction)
        TestUserFactory factory = new(Docker.ApiBaseUrl, MailpitBaseUrl);
        User = await factory.CreateVerifiedUserAsync(clientId: "wallow-web-client");

        // Perform OIDC login via browser
        await LoginViaOidcAsync();
    }

    private async Task LoginViaOidcAsync()
    {
        // Trigger OIDC challenge: Web -> API /connect/authorize -> Auth /login
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForURLAsync(
            url => url.Contains("/login", StringComparison.OrdinalIgnoreCase),
            new() { Timeout = 15_000 });

        // Wait for Blazor circuit on the Auth login page
        await WaitForBlazorReadyAsync(Page);

        // Fill credentials using data-testid selectors
        await Page.Locator("[data-testid='login-email']").FillAsync(User.Email);
        await Page.Locator("[data-testid='login-password']").FillAsync(User.Password);
        await Page.Locator("[data-testid='login-submit']").ClickAsync();

        // Wait for OIDC redirect chain to reach the dashboard
        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase),
            new() { Timeout = 15_000 });

        // Wait for Blazor circuit on the Web dashboard
        await WaitForBlazorReadyAsync(Page);
    }
}
```

**Step 3: Verify the E2E project builds**

Run: `dotnet build tests/Wallow.E2E.Tests`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add tests/Wallow.E2E.Tests/Infrastructure/TestUserFactory.cs tests/Wallow.E2E.Tests/Infrastructure/AuthenticatedE2ETestBase.cs
git commit -m "feat(e2e): add TestUserFactory and AuthenticatedE2ETestBase for API-seeded auth"
```

---

### Task 8: Rewrite page objects to use `data-testid` selectors

**Files:**
- Modify: `tests/Wallow.E2E.Tests/PageObjects/LoginPage.cs`
- Modify: `tests/Wallow.E2E.Tests/PageObjects/RegisterPage.cs`
- Modify: `tests/Wallow.E2E.Tests/PageObjects/DashboardPage.cs`
- Modify: `tests/Wallow.E2E.Tests/PageObjects/ForgotPasswordPage.cs` (rename from current patterns)
- Modify: `tests/Wallow.E2E.Tests/PageObjects/AppRegistrationPage.cs`
- Modify: `tests/Wallow.E2E.Tests/PageObjects/MfaEnrollPage.cs`
- Modify: `tests/Wallow.E2E.Tests/PageObjects/OrganizationPage.cs`
- Modify: `tests/Wallow.E2E.Tests/PageObjects/InquiryPage.cs`

**Step 1: Rewrite LoginPage.cs**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class LoginPage(IPage page, string baseUrl)
{
    public async Task NavigateAsync(string? returnUrl = null)
    {
        string url = returnUrl is not null
            ? $"{baseUrl}/login?returnUrl={Uri.EscapeDataString(returnUrl)}"
            : $"{baseUrl}/login";
        await page.GotoAsync(url);
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await page.Locator("[data-testid='login-heading']").IsVisibleAsync();
    }

    public async Task FillEmailAsync(string email)
    {
        await page.Locator("[data-testid='login-email']").FillAsync(email);
    }

    public async Task FillPasswordAsync(string password)
    {
        await page.Locator("[data-testid='login-password']").FillAsync(password);
    }

    public async Task SubmitAsync()
    {
        await page.Locator("[data-testid='login-submit']").ClickAsync();
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator error = page.Locator("[data-testid='login-error']");
        if (!await error.IsVisibleAsync()) return null;
        return await error.InnerTextAsync();
    }

    public async Task ClickForgotPasswordAsync()
    {
        await page.Locator("[data-testid='login-forgot-password']").ClickAsync();
    }

    public async Task ClickRegisterLinkAsync()
    {
        await page.Locator("[data-testid='login-register-link']").ClickAsync();
    }
}
```

**Step 2: Rewrite RegisterPage.cs**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class RegisterPage(IPage page, string baseUrl)
{
    public async Task NavigateAsync(string? clientId = null, string? returnUrl = null)
    {
        List<string> queryParams = [];
        if (clientId is not null) queryParams.Add($"client_id={Uri.EscapeDataString(clientId)}");
        if (returnUrl is not null) queryParams.Add($"returnUrl={Uri.EscapeDataString(returnUrl)}");

        string url = queryParams.Count > 0
            ? $"{baseUrl}/register?{string.Join("&", queryParams)}"
            : $"{baseUrl}/register";
        await page.GotoAsync(url);
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await page.Locator("[data-testid='register-heading']").IsVisibleAsync();
    }

    public async Task FillFormAsync(string email, string password, string confirmPassword,
        bool acceptTerms = true, bool acceptPrivacy = true)
    {
        await page.Locator("[data-testid='register-email']").FillAsync(email);
        await page.Locator("[data-testid='register-password']").FillAsync(password);
        await page.Locator("[data-testid='register-confirm-password']").FillAsync(confirmPassword);

        if (acceptTerms)
            await page.Locator("[data-testid='register-terms']").ClickAsync();
        if (acceptPrivacy)
            await page.Locator("[data-testid='register-privacy']").ClickAsync();
    }

    public async Task SubmitAsync()
    {
        await page.Locator("[data-testid='register-submit']").ClickAsync();
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator error = page.Locator("[data-testid='register-error']");
        if (!await error.IsVisibleAsync()) return null;
        return await error.InnerTextAsync();
    }
}
```

**Step 3: Rewrite DashboardPage.cs**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class DashboardPage(IPage page, string baseUrl)
{
    public async Task NavigateAsync()
    {
        await page.GotoAsync($"{baseUrl}/dashboard/apps");
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await page.Locator("[data-testid='dashboard-heading']").IsVisibleAsync();
    }

    public async Task<string?> GetWelcomeMessageAsync()
    {
        ILocator heading = page.Locator("[data-testid='dashboard-heading']");
        if (!await heading.IsVisibleAsync()) return null;
        return await heading.InnerTextAsync();
    }
}
```

**Step 4: Rewrite AppRegistrationPage.cs**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class AppRegistrationPage(IPage page, string baseUrl)
{
    public async Task NavigateAsync()
    {
        await page.GotoAsync($"{baseUrl}/dashboard/apps/register");
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await page.Locator("[data-testid='app-register-heading']").IsVisibleAsync();
    }

    public async Task FillFormAsync(string displayName, string clientType = "public",
        string? redirectUris = null, string? brandingDisplayName = null, string? brandingTagline = null)
    {
        await page.Locator("[data-testid='app-register-name']").FillAsync(displayName);
        await page.Locator("[data-testid='app-register-client-type']").SelectOptionAsync(clientType);

        if (redirectUris is not null)
            await page.Locator("[data-testid='app-register-redirect-uris']").FillAsync(redirectUris);
        if (brandingDisplayName is not null)
            await page.Locator("[data-testid='app-register-branding-name']").FillAsync(brandingDisplayName);
        if (brandingTagline is not null)
            await page.Locator("[data-testid='app-register-branding-tagline']").FillAsync(brandingTagline);
    }

    public async Task SubmitAsync()
    {
        await page.Locator("[data-testid='app-register-submit']").ClickAsync();
    }

    public async Task<AppRegistrationResult> GetResultAsync()
    {
        ILocator success = page.Locator("[data-testid='app-register-success']");
        if (!await success.IsVisibleAsync())
        {
            ILocator error = page.Locator("[data-testid='app-register-error']");
            string? errorMsg = await error.IsVisibleAsync() ? await error.InnerTextAsync() : null;
            return new AppRegistrationResult(false, null, null, errorMsg);
        }

        string clientId = await page.Locator("[data-testid='app-register-client-id']").InnerTextAsync();

        ILocator secretLocator = page.Locator("[data-testid='app-register-client-secret']");
        string? secret = await secretLocator.IsVisibleAsync()
            ? await secretLocator.Locator("code").InnerTextAsync()
            : null;

        return new AppRegistrationResult(true, clientId.Trim(), secret?.Trim(), null);
    }

    public async Task ClickBackToAppsAsync()
    {
        await page.Locator("[data-testid='app-register-back']").ClickAsync();
    }
}

public sealed record AppRegistrationResult(
    bool Success, string? ClientId, string? ClientSecret, string? ErrorMessage);
```

**Step 5: Rewrite MfaEnrollPage.cs**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class MfaEnrollPage(IPage page, string baseUrl)
{
    public async Task NavigateAsync(string? returnUrl = null)
    {
        string url = returnUrl is not null
            ? $"{baseUrl}/mfa/enroll?returnUrl={Uri.EscapeDataString(returnUrl)}"
            : $"{baseUrl}/mfa/enroll";
        await page.GotoAsync(url);
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await page.Locator("[data-testid='mfa-enroll-heading']").IsVisibleAsync();
    }

    public async Task ClickBeginSetupAsync()
    {
        await page.Locator("[data-testid='mfa-enroll-begin']").ClickAsync();
    }

    public async Task<bool> IsSecretVisibleAsync()
    {
        return await page.Locator("[data-testid='mfa-enroll-secret']").IsVisibleAsync();
    }

    public async Task FillCodeAsync(string code)
    {
        await page.Locator("[data-testid='mfa-enroll-code']").FillAsync(code);
    }

    public async Task SubmitAsync()
    {
        await page.Locator("[data-testid='mfa-enroll-verify']").ClickAsync();
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator error = page.Locator("[data-testid='mfa-enroll-error']");
        if (!await error.IsVisibleAsync()) return null;
        return await error.InnerTextAsync();
    }

    public async Task<bool> IsSuccessAsync()
    {
        return await page.Locator("[data-testid='mfa-enroll-success']").IsVisibleAsync();
    }

    public async Task ClickDoneAsync()
    {
        await page.Locator("[data-testid='mfa-enroll-done']").ClickAsync();
    }

    public async Task ClickCancelAsync()
    {
        await page.Locator("[data-testid='mfa-enroll-cancel']").ClickAsync();
    }
}
```

**Step 6: Rewrite OrganizationPage.cs**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class OrganizationPage(IPage page, string baseUrl)
{
    public async Task NavigateAsync()
    {
        await page.GotoAsync($"{baseUrl}/dashboard/organizations");
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await page.Locator("[data-testid='organizations-heading']").IsVisibleAsync();
    }

    public async Task<bool> IsEmptyStateAsync()
    {
        return await page.Locator("[data-testid='organizations-empty-state']").IsVisibleAsync();
    }

    public async Task<bool> HasTableAsync()
    {
        return await page.Locator("[data-testid='organizations-table']").IsVisibleAsync();
    }
}
```

Note: The old `GetOrganizationsAsync()` method scraped table rows using CSS — replace with `HasTableAsync()` for the assertion. The `OrganizationRow` record can be removed.

**Step 7: Rewrite InquiryPage.cs**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class InquiryPage(IPage page, string baseUrl)
{
    public async Task NavigateAsync()
    {
        await page.GotoAsync($"{baseUrl}/dashboard/inquiries");
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await page.Locator("[data-testid='inquiry-heading']").IsVisibleAsync();
    }

    public async Task FillFormAsync(string name, string email, string message,
        string? phone = null, string? company = null, string? projectType = null,
        string? budgetRange = null, string? timeline = null)
    {
        await page.Locator("[data-testid='inquiry-name']").FillAsync(name);
        await page.Locator("[data-testid='inquiry-email']").FillAsync(email);

        if (phone is not null)
            await page.Locator("[data-testid='inquiry-phone']").FillAsync(phone);
        if (company is not null)
            await page.Locator("[data-testid='inquiry-company']").FillAsync(company);
        if (projectType is not null)
            await page.Locator("[data-testid='inquiry-project-type']").SelectOptionAsync(projectType);
        if (budgetRange is not null)
            await page.Locator("[data-testid='inquiry-budget']").SelectOptionAsync(budgetRange);
        if (timeline is not null)
            await page.Locator("[data-testid='inquiry-timeline']").SelectOptionAsync(timeline);

        await page.Locator("[data-testid='inquiry-message']").FillAsync(message);
    }

    public async Task SubmitInquiryAsync()
    {
        await page.Locator("[data-testid='inquiry-submit']").ClickAsync();
    }

    public async Task<bool> IsSubmissionSuccessAsync()
    {
        return await page.Locator("[data-testid='inquiry-success']").IsVisibleAsync();
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator error = page.Locator("[data-testid='inquiry-error']");
        if (!await error.IsVisibleAsync()) return null;
        return await error.InnerTextAsync();
    }

    public async Task ClickSubmitAnotherAsync()
    {
        await page.Locator("[data-testid='inquiry-submit-another']").ClickAsync();
    }
}
```

**Step 8: Verify the E2E project builds**

Run: `dotnet build tests/Wallow.E2E.Tests`
Expected: Build succeeded

**Step 9: Commit**

```bash
git add tests/Wallow.E2E.Tests/PageObjects/
git commit -m "refactor(e2e): rewrite all page objects to use data-testid selectors"
```

---

### Task 9: Rewrite test classes to use new base classes

**Files:**
- Modify: `tests/Wallow.E2E.Tests/Flows/AuthFlowTests.cs`
- Modify: `tests/Wallow.E2E.Tests/Flows/DashboardFlowTests.cs`

**Step 1: Rewrite AuthFlowTests.cs**

This test class tests auth flows directly, so it extends `E2ETestBase` (not `AuthenticatedE2ETestBase`). It still needs its own registration helpers but uses the new `TestUserFactory` for pre-registration where the test isn't testing registration itself.

Key changes:
- Extend `E2ETestBase` instead of implementing `IAsyncLifetime` directly
- Use `Page` and `Docker` from base class
- Use `WaitForBlazorReadyAsync` instead of `PlaywrightFixture.WaitForBlazorAsync`
- Use `data-testid` selectors in inline interactions
- Use `CaptureFailureArtifactsAsync` in catch blocks
- Remove the duplicated Mailpit models (use `TestUserFactory` where possible)
- Keep direct Mailpit access for `ForgotPasswordFlow` (needs to check specific email)

The test for `RegistrationAndLoginFlow` still drives the registration UI since it's testing that flow. The test for `LoginToDashboard` uses `TestUserFactory` to seed the user, then does the OIDC login via browser.

**Step 2: Rewrite DashboardFlowTests.cs**

This test class extends `AuthenticatedE2ETestBase` — all tests start with an authenticated session.

Key changes:
- Extend `AuthenticatedE2ETestBase`
- Remove `RegisterAndLoginAsync` helper entirely
- Remove all Mailpit code
- Each test just navigates to its page and tests the feature
- Use `WaitForBlazorReadyAsync` after navigation to interactive pages
- Use `CaptureFailureArtifactsAsync` in catch blocks

**Step 3: Run the full E2E test suite**

Run: `./scripts/run-tests.sh tests/Wallow.E2E.Tests`
Expected: All 8 tests pass

**Step 4: If tests fail, capture artifacts and debug**

Check `tests/Wallow.E2E.Tests/test-results/` for screenshots and traces. Fix any selector mismatches between `data-testid` attributes in Razor pages and the page objects.

**Step 5: Commit**

```bash
git add tests/Wallow.E2E.Tests/Flows/
git commit -m "refactor(e2e): rewrite test classes to use E2ETestBase and AuthenticatedE2ETestBase"
```

---

### Task 10: Update CI pipeline and gitignore

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: `.gitignore`

**Step 1: Add test-results to .gitignore**

Add to `.gitignore`:

```
# E2E test artifacts
tests/Wallow.E2E.Tests/test-results/
```

**Step 2: Add artifact upload to CI e2e job**

In `.github/workflows/ci.yml`, add after the "Run E2E tests" step and before "Tear down test environment":

```yaml
      - name: Upload E2E test artifacts
        uses: actions/upload-artifact@v4
        if: failure()
        with:
          name: e2e-test-results
          path: tests/Wallow.E2E.Tests/test-results/
          retention-days: 7
```

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml .gitignore
git commit -m "ci: upload E2E test artifacts on failure"
```

---

### Task 11: Final verification and cleanup

**Step 1: Run the full E2E suite**

Run: `./scripts/run-tests.sh tests/Wallow.E2E.Tests`
Expected: All 8 tests pass

**Step 2: Run the full test suite to verify no regressions**

Run: `./scripts/run-tests.sh`
Expected: All tests pass (E2E + unit + integration)

**Step 3: Clean up any orphaned files**

Check if the old `OrganizationRow` record type was in `OrganizationPage.cs` — if so, it was already removed in Task 8.

**Step 4: Push**

```bash
git pull --rebase && git push
```
