# Wallow.Auth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Blazor Web App (`Wallow.Auth`) on `auth.wallow.dev` with login, register, forgot/reset password, email verification, and logout pages using Blazor Blueprint UI — backed by new API endpoints in `Wallow.Api`.

**Architecture:** Wallow.Auth is a standalone Blazor Web App (.NET 10, SSR + InteractiveServer) that acts as a UI frontend. It communicates with Wallow.Api over HTTP. All authentication operations (cookie setting, credential validation, user creation) happen in Wallow.Api. The Identity cookie is set by Wallow.Api on `.wallow.dev` domain.

**Tech Stack:** .NET 10, Blazor Web App, Blazor Blueprint UI, ASP.NET Core Identity, OpenIddict, Tailwind CSS

**Spec:** `docs/superpowers/specs/2026-03-20-wallow-auth-design.md`

---

## File Map

### New Files — Wallow.Auth Project

| File | Responsibility |
|------|---------------|
| `src/Wallow.Auth/Wallow.Auth.csproj` | Project file with BlazorBlueprint.Components, Lucide icons |
| `src/Wallow.Auth/Program.cs` | Host builder, HttpClient registration, Blazor services |
| `src/Wallow.Auth/appsettings.json` | Production config (ApiBaseUrl) |
| `src/Wallow.Auth/appsettings.Development.json` | Dev config (ApiBaseUrl = localhost:5000) |
| `src/Wallow.Auth/Properties/launchSettings.json` | Dev launch profile (port 5002) |
| `src/Wallow.Auth/Components/App.razor` | Root Blazor component (html, head, body) |
| `src/Wallow.Auth/Components/Routes.razor` | Router component |
| `src/Wallow.Auth/Components/_Imports.razor` | Global using directives |
| `src/Wallow.Auth/Components/Layout/AuthLayout.razor` | Minimal dark layout wrapper |
| `src/Wallow.Auth/Components/Pages/Login.razor` | Login page |
| `src/Wallow.Auth/Components/Pages/Register.razor` | Register page |
| `src/Wallow.Auth/Components/Pages/ForgotPassword.razor` | Forgot password page |
| `src/Wallow.Auth/Components/Pages/ResetPassword.razor` | Reset password page |
| `src/Wallow.Auth/Components/Pages/VerifyEmail.razor` | Post-registration "check your email" page |
| `src/Wallow.Auth/Components/Pages/VerifyEmailConfirm.razor` | Email verification landing page |
| `src/Wallow.Auth/Components/Pages/Logout.razor` | Logout confirmation + signed-out page |
| `src/Wallow.Auth/Services/IAuthApiClient.cs` | Interface for API communication |
| `src/Wallow.Auth/Services/AuthApiClient.cs` | HttpClient wrapper for auth endpoints |
| `src/Wallow.Auth/Models/LoginRequest.cs` | Login request DTO |
| `src/Wallow.Auth/Models/RegisterRequest.cs` | Register request DTO |
| `src/Wallow.Auth/Models/ForgotPasswordRequest.cs` | Forgot password request DTO |
| `src/Wallow.Auth/Models/ResetPasswordRequest.cs` | Reset password request DTO |
| `src/Wallow.Auth/Models/AuthResponse.cs` | Shared response model (succeeded, error) |
| `src/Wallow.Auth/wwwroot/css/theme.css` | Blueprint theme overrides (dark mode) |

### New Files — Wallow.Api (New Endpoints)

| File | Responsibility |
|------|---------------|
| `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs` | Cookie-based login, register, sign-out, forgot/reset password, email verification endpoints |
| `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountLoginRequest.cs` | Login request model |
| `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountRegisterRequest.cs` | Register request model |
| `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountForgotPasswordRequest.cs` | Forgot password request model |
| `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountResetPasswordRequest.cs` | Reset password request model |

### Modified Files — Wallow.Api

| File | Change |
|------|--------|
| `src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthorizationController.cs` | Update unauthenticated redirect to configurable auth URL |
| `src/Modules/Identity/Wallow.Identity.Api/Controllers/LogoutController.cs` | Update redirect to auth URL, extract post_logout_redirect_uri |
| `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs` | Add cookie domain config |
| `src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs` | Add localhost:5002 to dev CORS |
| `src/Wallow.Api/appsettings.json` | Add AuthUrl config |
| `src/Wallow.Api/appsettings.Development.json` | Add AuthUrl = http://localhost:5002 |
| `Directory.Packages.props` | Add BlazorBlueprint package versions |

### Test Files

| File | Tests |
|------|-------|
| `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/AccountControllerTests.cs` | Unit tests for AccountController |
| `tests/Wallow.Auth.Tests/Wallow.Auth.Tests.csproj` | Test project for Wallow.Auth |
| `tests/Wallow.Auth.Tests/Services/AuthApiClientTests.cs` | Unit tests for AuthApiClient |

---

## Task 1: Scaffold Wallow.Auth Blazor Web App Project

**Files:**
- Create: `src/Wallow.Auth/Wallow.Auth.csproj`
- Create: `src/Wallow.Auth/Program.cs`
- Create: `src/Wallow.Auth/appsettings.json`
- Create: `src/Wallow.Auth/appsettings.Development.json`
- Create: `src/Wallow.Auth/Properties/launchSettings.json`
- Create: `src/Wallow.Auth/Components/App.razor`
- Create: `src/Wallow.Auth/Components/Routes.razor`
- Create: `src/Wallow.Auth/Components/_Imports.razor`
- Create: `src/Wallow.Auth/Components/Layout/AuthLayout.razor`
- Create: `src/Wallow.Auth/wwwroot/css/theme.css`
- Modify: `Directory.Packages.props` — add BlazorBlueprint package versions

- [ ] **Step 1: Verify BlazorBlueprint NuGet package name and add to Directory.Packages.props**

First, verify the actual NuGet package ID (it may differ from the marketing name "Blazor Blueprint UI"):

```bash
dotnet package search BlazorBlueprint --take 5
```

If the package is not found, check `https://blazorblueprintui.com/docs/installation` for the correct package name. Then add entries to `Directory.Packages.props` with the verified name and a pinned version:

```xml
<PackageVersion Include="BlazorBlueprint.Components" Version="<verified-version>" />
<PackageVersion Include="BlazorBlueprint.Icons.Lucide" Version="<verified-version>" />
```

**Do not use wildcard `*` versions** — pin to a specific version to match the project's convention.

- [ ] **Step 2: Create project file**

Create `src/Wallow.Auth/Wallow.Auth.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <PackageReference Include="BlazorBlueprint.Components" />
    <PackageReference Include="BlazorBlueprint.Icons.Lucide" />
  </ItemGroup>

</Project>
```

Note: `TargetFramework`, `Nullable`, `ImplicitUsings`, etc. are inherited from root `Directory.Build.props`.

- [ ] **Step 3: Create Program.cs**

Create `src/Wallow.Auth/Program.cs`:

```csharp
using BlazorBlueprint.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazorBlueprintComponents();

string apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl must be configured");

builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddScoped<Wallow.Auth.Services.IAuthApiClient, Wallow.Auth.Services.AuthApiClient>();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Wallow.Auth.Components.App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
```

- [ ] **Step 4: Create appsettings files**

Create `src/Wallow.Auth/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ApiBaseUrl": "https://api.wallow.dev"
}
```

Create `src/Wallow.Auth/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "ApiBaseUrl": "http://localhost:5000"
}
```

- [ ] **Step 5: Create launchSettings.json**

Create `src/Wallow.Auth/Properties/launchSettings.json`:

```json
{
  "profiles": {
    "Wallow.Auth": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:5002",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 6: Create App.razor**

Create `src/Wallow.Auth/Components/App.razor`:

```razor
<!DOCTYPE html>
<html lang="en" class="dark">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link href="css/theme.css" rel="stylesheet" />
    <link href="_content/BlazorBlueprint.Components/blazorblueprint.css" rel="stylesheet" />
    <HeadOutlet />
</head>

<body>
    <Routes />
    <script src="_framework/blazor.web.js"></script>
    <BbPortalHost />
</body>

</html>
```

- [ ] **Step 7: Create Routes.razor**

Create `src/Wallow.Auth/Components/Routes.razor`:

```razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.AuthLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

- [ ] **Step 8: Create _Imports.razor**

Create `src/Wallow.Auth/Components/_Imports.razor`:

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.JSInterop
@using BlazorBlueprint.Components
@using Wallow.Auth.Components
@using Wallow.Auth.Components.Layout
@using Wallow.Auth.Models
@using Wallow.Auth.Services
```

- [ ] **Step 9: Create AuthLayout.razor**

Create `src/Wallow.Auth/Components/Layout/AuthLayout.razor`:

```razor
@inherits LayoutComponentBase

<div class="min-h-screen bg-background flex flex-col items-center justify-center px-4">
    <div class="w-full max-w-[400px]">
        <div class="text-center mb-8">
            <h1 class="text-2xl font-bold text-foreground">Wallow</h1>
        </div>
        @Body
    </div>
</div>
```

- [ ] **Step 10: Create theme.css**

Create `src/Wallow.Auth/wwwroot/css/theme.css`:

```css
/* Blazor Blueprint dark theme overrides for Wallow.Auth */
/* Base styles - Blueprint handles the dark theme via class="dark" on <html> */

body {
    margin: 0;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}
```

- [ ] **Step 11: Verify project builds**

Run: `dotnet build src/Wallow.Auth/Wallow.Auth.csproj`
Expected: Build succeeds with no errors or warnings.

- [ ] **Step 12: Commit**

```bash
git add src/Wallow.Auth/ Directory.Packages.props
git commit -m "feat(auth): scaffold Wallow.Auth Blazor Web App with Blueprint UI"
```

---

## Task 2: Create AuthApiClient Service and Models

**Files:**
- Create: `src/Wallow.Auth/Services/IAuthApiClient.cs`
- Create: `src/Wallow.Auth/Services/AuthApiClient.cs`
- Create: `src/Wallow.Auth/Models/LoginRequest.cs`
- Create: `src/Wallow.Auth/Models/RegisterRequest.cs`
- Create: `src/Wallow.Auth/Models/ForgotPasswordRequest.cs`
- Create: `src/Wallow.Auth/Models/ResetPasswordRequest.cs`
- Create: `src/Wallow.Auth/Models/AuthResponse.cs`
- Create: `tests/Wallow.Auth.Tests/Wallow.Auth.Tests.csproj`
- Create: `tests/Wallow.Auth.Tests/Services/AuthApiClientTests.cs`

- [ ] **Step 1: Create request/response models**

Create `src/Wallow.Auth/Models/LoginRequest.cs`:

```csharp
namespace Wallow.Auth.Models;

public sealed record LoginRequest(string Email, string Password, bool RememberMe);
```

Create `src/Wallow.Auth/Models/RegisterRequest.cs`:

```csharp
namespace Wallow.Auth.Models;

public sealed record RegisterRequest(string Email, string Password, string ConfirmPassword);
```

Create `src/Wallow.Auth/Models/ForgotPasswordRequest.cs`:

```csharp
namespace Wallow.Auth.Models;

public sealed record ForgotPasswordRequest(string Email);
```

Create `src/Wallow.Auth/Models/ResetPasswordRequest.cs`:

```csharp
namespace Wallow.Auth.Models;

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
```

Create `src/Wallow.Auth/Models/AuthResponse.cs`:

```csharp
namespace Wallow.Auth.Models;

public sealed record AuthResponse(bool Succeeded, string? Error = null);
```

- [ ] **Step 2: Create IAuthApiClient interface**

Create `src/Wallow.Auth/Services/IAuthApiClient.cs`:

```csharp
namespace Wallow.Auth.Services;

using Wallow.Auth.Models;

public interface IAuthApiClient
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<AuthResponse> VerifyEmailAsync(string email, string token, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create AuthApiClient implementation**

Create `src/Wallow.Auth/Services/AuthApiClient.cs`:

```csharp
namespace Wallow.Auth.Services;

using System.Net;
using System.Net.Http.Json;
using Wallow.Auth.Models;

public sealed class AuthApiClient(IHttpClientFactory httpClientFactory) : IAuthApiClient
{
    private const string BasePath = "api/v1/identity/auth";

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync($"{BasePath}/login", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync($"{BasePath}/register", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync($"{BasePath}/forgot-password", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync($"{BasePath}/reset-password", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> VerifyEmailAsync(string email, string token, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        string encodedEmail = Uri.EscapeDataString(email);
        string encodedToken = Uri.EscapeDataString(token);
        HttpResponseMessage response = await client.GetAsync(
            $"{BasePath}/verify-email?email={encodedEmail}&token={encodedToken}", ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }
}
```

- [ ] **Step 4: Create test project**

Create `tests/Wallow.Auth.Tests/Wallow.Auth.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="RichardSzalay.MockHttp" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Wallow.Auth\Wallow.Auth.csproj" />
  </ItemGroup>

</Project>
```

Add `RichardSzalay.MockHttp` to `Directory.Packages.props`:

```xml
<PackageVersion Include="RichardSzalay.MockHttp" Version="7.*" />
```

- [ ] **Step 5: Write AuthApiClient tests**

Create `tests/Wallow.Auth.Tests/Services/AuthApiClientTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RichardSzalay.MockHttp;
using Wallow.Auth.Models;
using Wallow.Auth.Services;

namespace Wallow.Auth.Tests.Services;

public sealed class AuthApiClientTests
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly AuthApiClient _sut;

    public AuthApiClientTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:5000");

        IHttpClientFactory factory = NSubstitute.Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AuthApi").Returns(httpClient);

        _sut = new AuthApiClient(factory);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/login")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { succeeded = true }));

        AuthResponse result = await _sut.LoginAsync(new LoginRequest("test@test.com", "password", false));

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/login")
            .Respond(HttpStatusCode.Unauthorized,
                JsonContent.Create(new { succeeded = false, error = "invalid_credentials" }));

        AuthResponse result = await _sut.LoginAsync(new LoginRequest("test@test.com", "wrong", false));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task LoginAsync_LockedOut_ReturnsLockedOutError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/login")
            .Respond((HttpStatusCode)423,
                JsonContent.Create(new { succeeded = false, error = "locked_out" }));

        AuthResponse result = await _sut.LoginAsync(new LoginRequest("test@test.com", "password", false));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("locked_out");
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/register")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { succeeded = true }));

        AuthResponse result = await _sut.RegisterAsync(
            new RegisterRequest("test@test.com", "Password1!", "Password1!"));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/register")
            .Respond(HttpStatusCode.BadRequest,
                JsonContent.Create(new { succeeded = false, error = "email_taken" }));

        AuthResponse result = await _sut.RegisterAsync(
            new RegisterRequest("taken@test.com", "Password1!", "Password1!"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("email_taken");
    }

    [Fact]
    public async Task ForgotPasswordAsync_AnyEmail_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/forgot-password")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { succeeded = true }));

        AuthResponse result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest("any@test.com"));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/verify-email*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { succeeded = true }));

        AuthResponse result = await _sut.VerifyEmailAsync("test@test.com", "valid-token");

        result.Succeeded.Should().BeTrue();
    }
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test tests/Wallow.Auth.Tests/`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Wallow.Auth/Services/ src/Wallow.Auth/Models/ tests/Wallow.Auth.Tests/ Directory.Packages.props
git commit -m "feat(auth): add AuthApiClient service with models and tests"
```

---

## Task 3: Build Login Page

**Files:**
- Create: `src/Wallow.Auth/Components/Pages/Login.razor`

- [ ] **Step 1: Create Login.razor**

Create `src/Wallow.Auth/Components/Pages/Login.razor` with:
- `@page "/login"`
- `@rendermode InteractiveServer`
- Email input (`BbInput`), password input (`BbInput` type="password")
- "Remember me" checkbox (`BbCheckbox`)
- Submit button (`BbButton`) with loading state
- Error display area (`BbAlert` variant="destructive")
- "Forgot password?" link to `/forgot-password`
- Social login buttons (Google, GitHub, Microsoft, Apple) — each links to `{ApiBaseUrl}/api/v1/identity/auth/external-login?provider={name}&returnUrl={returnUrl}`
- "Don't have an account? Register" link to `/register`
- Read `returnUrl` from query string via `[SupplyParameterFromQuery]`
- On submit: call `IAuthApiClient.LoginAsync`, handle response errors, redirect to `returnUrl` on success (or show "You are now signed in" if no returnUrl)

- [ ] **Step 2: Verify login page renders**

Run: `dotnet run --project src/Wallow.Auth`
Navigate to `http://localhost:5002/login`
Expected: Login form renders with all elements in dark theme.

- [ ] **Step 3: Commit**

```bash
git add src/Wallow.Auth/Components/Pages/Login.razor
git commit -m "feat(auth): add login page with Blueprint UI components"
```

---

## Task 4: Build Register Page

**Files:**
- Create: `src/Wallow.Auth/Components/Pages/Register.razor`

- [ ] **Step 1: Create Register.razor**

Create `src/Wallow.Auth/Components/Pages/Register.razor` with:
- `@page "/register"`
- `@rendermode InteractiveServer`
- Email input, password input, confirm password input
- ToS checkbox linking to `/terms`, privacy policy checkbox linking to `/privacy`
- Submit button with loading state
- Client-side validation: email format, passwords match, both checkboxes checked
- Password strength indicator (`BbProgress`)
- Social signup buttons (same as login)
- "Already have an account? Sign in" link to `/login`
- On submit: call `IAuthApiClient.RegisterAsync`, redirect to `/verify-email` on success

- [ ] **Step 2: Verify register page renders**

Run Wallow.Auth, navigate to `http://localhost:5002/register`.
Expected: Register form renders with all elements.

- [ ] **Step 3: Commit**

```bash
git add src/Wallow.Auth/Components/Pages/Register.razor
git commit -m "feat(auth): add register page with validation and Blueprint UI"
```

---

## Task 5: Build Supporting Pages (Forgot Password, Reset Password, Email Verification, Logout)

**Files:**
- Create: `src/Wallow.Auth/Components/Pages/ForgotPassword.razor`
- Create: `src/Wallow.Auth/Components/Pages/ResetPassword.razor`
- Create: `src/Wallow.Auth/Components/Pages/VerifyEmail.razor`
- Create: `src/Wallow.Auth/Components/Pages/VerifyEmailConfirm.razor`
- Create: `src/Wallow.Auth/Components/Pages/Logout.razor`

- [ ] **Step 1: Create ForgotPassword.razor**

`@page "/forgot-password"`, `@rendermode InteractiveServer`. Email input, submit button. On submit: call `IAuthApiClient.ForgotPasswordAsync`, always show success message "If an account exists with that email, we've sent a password reset link."

- [ ] **Step 2: Create ResetPassword.razor**

`@page "/reset-password"`, `@rendermode InteractiveServer`. Read `token` and `email` from query string. New password + confirm password inputs. On submit: call `IAuthApiClient.ResetPasswordAsync`, redirect to `/login?message=password_reset` on success.

- [ ] **Step 3: Create VerifyEmail.razor**

`@page "/verify-email"`. Static SSR page: "Check your email for a verification link." Link back to `/login`.

- [ ] **Step 4: Create VerifyEmailConfirm.razor**

`@page "/verify-email/confirm"`, `@rendermode InteractiveServer`. Read `token` and `email` from query string. On load: call `IAuthApiClient.VerifyEmailAsync`. Show success ("Email verified! You can now sign in.") or failure message. Link to `/login`.

- [ ] **Step 5: Create Logout.razor**

`@page "/logout"`. Read `post_logout_redirect_uri` and `signed_out` from query string.

Two states:
- If `signed_out=true`: show "You have been signed out" message + optional "Return to application" link if `post_logout_redirect_uri` is present.
- Otherwise: show "Are you sure you want to sign out?" with a `<form>` that POSTs to `{ApiBaseUrl}/api/v1/identity/auth/sign-out` with a hidden `post_logout_redirect_uri` field. This is a real HTML form submission (not Blazor), so the browser navigates to the API domain.

- [ ] **Step 6: Verify all pages render**

Navigate to each page on localhost:5002 and verify they render correctly.

- [ ] **Step 7: Commit**

```bash
git add src/Wallow.Auth/Components/Pages/
git commit -m "feat(auth): add forgot password, reset password, email verification, and logout pages"
```

---

## Task 6: Add AccountController to Wallow.Api (Login + Sign-out Endpoints)

**Files:**
- Create: `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountLoginRequest.cs`
- Create: `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/AccountControllerTests.cs`

- [ ] **Step 1: Create request model**

Create `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountLoginRequest.cs`:

```csharp
namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record AccountLoginRequest(string Email, string Password, bool RememberMe);
```

- [ ] **Step 2: Create AccountController with login endpoint**

Create `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs`:

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// Cookie-based authentication endpoints for Wallow.Auth (browser-based auth flows).
/// For bearer token BFF endpoints, see <see cref="AuthController"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/auth")]
[EnableRateLimiting("auth")]
public sealed class AccountController(
    SignInManager<WallowUser> signInManager,
    IConfiguration configuration,
    ILogger<AccountController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AccountLoginRequest request, CancellationToken ct)
    {
        SignInResult result = await signInManager.PasswordSignInAsync(
            request.Email, request.Password, request.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return Ok(new { succeeded = true });
        }

        if (result.IsLockedOut)
        {
            return StatusCode(423, new { succeeded = false, error = "locked_out" });
        }

        if (result.IsNotAllowed)
        {
            return StatusCode(403, new { succeeded = false, error = "email_not_confirmed" });
        }

        return Unauthorized(new { succeeded = false, error = "invalid_credentials" });
    }

    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOut([FromForm] string? postLogoutRedirectUri)
    {
        await signInManager.SignOutAsync();

        // Also sign out of OpenIddict to revoke authorizations/tokens
        await HttpContext.SignOutAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        string authUrl = configuration["AuthUrl"] ?? "https://auth.wallow.dev";

        string redirectUrl = $"{authUrl}/logout?signed_out=true";
        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            redirectUrl += $"&post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}";
        }

        return Redirect(redirectUrl);
    }
}
```

**Key decisions:**
- Inherits `ControllerBase` (not `Controller`) to match project convention — no Razor view support needed.
- `[ApiController]` attribute for automatic model validation and problem details.
- `[AllowAnonymous]` applied per-method (not class-level) so `[Authorize]` on `SignOut` is effective.
- `SignOut` calls both `SignInManager.SignOutAsync()` and OpenIddict's `SignOut` per spec requirements.
- `IConfiguration` injected via primary constructor (not `HttpContext.RequestServices`).

- [ ] **Step 3: Write tests for AccountController**

Create `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/AccountControllerTests.cs`:

Test cases:
- `Login_ValidCredentials_ReturnsOk`
- `Login_InvalidCredentials_ReturnsUnauthorized`
- `Login_LockedOut_Returns423`
- `Login_EmailNotConfirmed_Returns403`
- `SignOut_Authenticated_RedirectsToAuthLogout`

Use NSubstitute to mock `SignInManager<WallowUser>`.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Modules/Identity/Wallow.Identity.Tests/ --filter "AccountController"`
Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs \
  src/Modules/Identity/Wallow.Identity.Api/Requests/LoginRequest.cs \
  tests/Modules/Identity/Wallow.Identity.Tests/
git commit -m "feat(identity): add AccountController with login and sign-out endpoints"
```

---

## Task 7: Add Register, Forgot/Reset Password, and Email Verification Endpoints

**Files:**
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountRegisterRequest.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountForgotPasswordRequest.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountResetPasswordRequest.cs`

- [ ] **Step 1: Create request models**

Create `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountRegisterRequest.cs`:
```csharp
namespace Wallow.Identity.Api.Contracts.Requests;
public sealed record AccountRegisterRequest(string Email, string Password, string ConfirmPassword);
```

Create `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountForgotPasswordRequest.cs`:
```csharp
namespace Wallow.Identity.Api.Contracts.Requests;
public sealed record AccountForgotPasswordRequest(string Email);
```

Create `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AccountResetPasswordRequest.cs`:
```csharp
namespace Wallow.Identity.Api.Contracts.Requests;
public sealed record AccountResetPasswordRequest(string Email, string Token, string NewPassword);
```

- [ ] **Step 2: Add register endpoint to AccountController**

```csharp
[HttpPost("register")]
[AllowAnonymous]
public async Task<IActionResult> Register([FromBody] AccountRegisterRequest request, CancellationToken ct)
{
    if (request.Password != request.ConfirmPassword)
    {
        return BadRequest(new { succeeded = false, error = "passwords_do_not_match" });
    }

    WallowUser user = new() { UserName = request.Email, Email = request.Email };
    IdentityResult result = await signInManager.UserManager.CreateAsync(user, request.Password);

    if (!result.Succeeded)
    {
        string error = result.Errors.First().Code switch
        {
            "DuplicateEmail" or "DuplicateUserName" => "email_taken",
            _ => result.Errors.First().Description
        };
        return BadRequest(new { succeeded = false, error });
    }

    // Generate verification token and send email via Wolverine message bus
    string token = await signInManager.UserManager.GenerateEmailConfirmationTokenAsync(user);
    string authUrl = configuration["AuthUrl"] ?? "https://auth.wallow.dev";
    string verifyUrl = $"{authUrl}/verify-email/confirm?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

    // Publish email command via Wolverine - the Notifications module handles delivery
    // await messageBus.PublishAsync(new SendEmailCommand(user.Email!, "Verify your email", $"Click here to verify: {verifyUrl}"));
    // NOTE: If the Notifications module email integration is not yet wired, log the verify URL for dev testing:
    logger.LogInformation("Email verification URL for {Email}: {VerifyUrl}", user.Email, verifyUrl);

    return Ok(new { succeeded = true });
}
```

**Email sending note:** The implementer should check how the existing Notifications module sends emails (look for `IEmailSender` or Wolverine message handlers). If a `SendEmailCommand` or similar exists, use it. Otherwise, use ASP.NET Identity's `IEmailSender<WallowUser>` interface. As a fallback for initial development, log the URL and verify manually via Mailpit.

- [ ] **Step 3: Add forgot-password endpoint**

```csharp
[HttpPost("forgot-password")]
[AllowAnonymous]
public async Task<IActionResult> ForgotPassword([FromBody] AccountForgotPasswordRequest request, CancellationToken ct)
{
    // Always return success to prevent email enumeration
    WallowUser? user = await signInManager.UserManager.FindByEmailAsync(request.Email);
    if (user is not null && await signInManager.UserManager.IsEmailConfirmedAsync(user))
    {
        string token = await signInManager.UserManager.GeneratePasswordResetTokenAsync(user);
        string authUrl = configuration["AuthUrl"] ?? "https://auth.wallow.dev";
        string resetUrl = $"{authUrl}/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

        // Send email via Notifications module (same pattern as register)
        logger.LogInformation("Password reset URL for {Email}: {ResetUrl}", user.Email, resetUrl);
    }

    return Ok(new { succeeded = true });
}
```

- [ ] **Step 4: Add reset-password endpoint**

```csharp
[HttpPost("reset-password")]
[AllowAnonymous]
public async Task<IActionResult> ResetPassword([FromBody] AccountResetPasswordRequest request, CancellationToken ct)
{
    WallowUser? user = await signInManager.UserManager.FindByEmailAsync(request.Email);
    if (user is null)
    {
        return BadRequest(new { succeeded = false, error = "invalid_token" });
    }

    IdentityResult result = await signInManager.UserManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
    if (!result.Succeeded)
    {
        return BadRequest(new { succeeded = false, error = "invalid_token" });
    }

    return Ok(new { succeeded = true });
}
```

- [ ] **Step 5: Add verify-email endpoint**

```csharp
[HttpGet("verify-email")]
[AllowAnonymous]
public async Task<IActionResult> VerifyEmail([FromQuery] string email, [FromQuery] string token, CancellationToken ct)
{
    WallowUser? user = await signInManager.UserManager.FindByEmailAsync(email);
    if (user is null)
    {
        return BadRequest(new { succeeded = false, error = "invalid_token" });
    }

    IdentityResult result = await signInManager.UserManager.ConfirmEmailAsync(user, token);
    if (!result.Succeeded)
    {
        return BadRequest(new { succeeded = false, error = "invalid_token" });
    }

    return Ok(new { succeeded = true });
}
```

- [ ] **Step 6: Write tests for new endpoints**

Add test cases to `AccountControllerTests.cs`:
- `Register_ValidRequest_ReturnsOk`
- `Register_PasswordMismatch_ReturnsBadRequest`
- `Register_DuplicateEmail_ReturnsBadRequest`
- `ForgotPassword_AnyEmail_ReturnsOk` (always succeeds)
- `ResetPassword_ValidToken_ReturnsOk`
- `ResetPassword_InvalidToken_ReturnsBadRequest`
- `VerifyEmail_ValidToken_ReturnsOk`
- `VerifyEmail_InvalidToken_ReturnsBadRequest`

- [ ] **Step 7: Run tests**

Run: `dotnet test tests/Modules/Identity/Wallow.Identity.Tests/ --filter "AccountController"`
Expected: All pass.

- [ ] **Step 8: Commit**

```bash
git add src/Modules/Identity/Wallow.Identity.Api/ tests/Modules/Identity/Wallow.Identity.Tests/
git commit -m "feat(identity): add register, forgot/reset password, and email verification endpoints"
```

---

## Task 8: Update Wallow.Api Configuration (CORS, Cookie Domain, Redirects)

**Files:**
- Modify: `src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/Wallow.Api/appsettings.json`
- Modify: `src/Wallow.Api/appsettings.Development.json`
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthorizationController.cs`
- Modify: `src/Modules/Identity/Wallow.Identity.Api/Controllers/LogoutController.cs`
- Modify: `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`

- [ ] **Step 1: Add AuthUrl to appsettings**

In `src/Wallow.Api/appsettings.json`, add:
```json
"AuthUrl": "https://auth.wallow.dev"
```

In `src/Wallow.Api/appsettings.Development.json`, add:
```json
"AuthUrl": "http://localhost:5002"
```

- [ ] **Step 2: Update CORS policies**

In `src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs`, add `http://localhost:5002` to the Development CORS policy origins array.

In `src/Wallow.Api/appsettings.json`, add `https://auth.wallow.dev` to the `Cors:AllowedOrigins` array for the production default CORS policy.

- [ ] **Step 3: Update AuthorizationController redirect**

In `AuthorizationController.cs`, change the unauthenticated redirect from:
```csharp
return Challenge(new AuthenticationProperties
{
    RedirectUri = ...
}, IdentityConstants.ApplicationScheme);
```
or the equivalent redirect to `/Account/Login?returnUrl=...`, to use the configurable `AuthUrl`:

```csharp
string authUrl = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["AuthUrl"]
    ?? "https://auth.wallow.dev";
string returnUrl = Request.PathBase + Request.Path + Request.QueryString;
return Redirect($"{authUrl}/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
```

- [ ] **Step 4: Update LogoutController redirect**

In `LogoutController.cs`, update the GET handler to:
1. Read `post_logout_redirect_uri` from the OpenIddict server request
2. Redirect to `{AuthUrl}/logout?post_logout_redirect_uri=...` instead of `/Account/Logout`

- [ ] **Step 5: Configure cookie domain**

In `IdentityInfrastructureExtensions.cs`, add cookie configuration:

```csharp
services.ConfigureApplicationCookie(options =>
{
    string? cookieDomain = configuration["Authentication:CookieDomain"];
    if (!string.IsNullOrEmpty(cookieDomain))
    {
        options.Cookie.Domain = cookieDomain;
    }
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
```

Add to `appsettings.json`:
```json
"Authentication": {
  "CookieDomain": ".wallow.dev"
}
```

Leave `CookieDomain` absent in `appsettings.Development.json` (defaults to localhost behavior).

- [ ] **Step 6: Verify existing tests still pass**

Run: `dotnet test`
Expected: All existing tests pass (no regressions from config changes).

- [ ] **Step 7: Commit**

```bash
git add src/Wallow.Api/ src/Modules/Identity/
git commit -m "feat(identity): configure CORS, cookie domain, and auth redirects for Wallow.Auth"
```

---

## Task 9: End-to-End Smoke Test

**Prerequisites:** Tasks 1–8 must ALL be complete before this task. Tasks 1–5 (Wallow.Auth UI) and Tasks 6–8 (Wallow.Api endpoints + config) are independent parallel tracks that converge here.

**Files:** None (manual verification)

- [ ] **Step 1: Start infrastructure**

```bash
cd docker && docker compose up -d
```

- [ ] **Step 2: Start Wallow.Api**

```bash
dotnet run --project src/Wallow.Api
```

- [ ] **Step 3: Start Wallow.Auth**

In a separate terminal:
```bash
dotnet run --project src/Wallow.Auth
```

- [ ] **Step 4: Verify login page**

Navigate to `http://localhost:5002/login`. Verify:
- Dark theme renders correctly
- All form elements present (email, password, remember me, social buttons, links)
- Form submission calls localhost:5000 API

- [ ] **Step 5: Verify register page**

Navigate to `http://localhost:5002/register`. Verify:
- All form elements present
- Client-side validation works (password mismatch, unchecked checkboxes)

- [ ] **Step 6: Verify supporting pages**

Navigate to each page:
- `http://localhost:5002/forgot-password`
- `http://localhost:5002/reset-password?token=test&email=test@test.com`
- `http://localhost:5002/verify-email`
- `http://localhost:5002/verify-email/confirm?token=test&email=test@test.com`
- `http://localhost:5002/logout`

- [ ] **Step 7: Test login flow end-to-end**

1. Register a new user via the register page
2. Check the API console logs for the email verification URL (emails are logged in dev until Notifications module integration is wired up). Alternatively check Mailpit at localhost:8025 if email sending is configured.
3. Open the verification URL to confirm the email
4. Log in with the registered credentials
5. Verify cookie is set and redirect works

- [ ] **Step 8: Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 9: Commit any fixes**

If any fixes were needed during smoke testing, commit them.

---

## Task 10: Final Cleanup and Push

- [ ] **Step 1: Verify git status**

```bash
git status
```

Ensure all changes are committed and working tree is clean.

- [ ] **Step 2: Run full test suite one more time**

```bash
dotnet test
```

- [ ] **Step 3: Push**

```bash
git pull --rebase && git push
```

---

## Future Tasks (Out of Scope)

These are noted for tracking but not part of this implementation:

- Social login provider registration (Google, GitHub, Microsoft, Apple OIDC config)
- `CompleteRegistration.razor` page for social signup ToS consent
- `ITokenService` implementation for BFF endpoints
- Consent page restyling
- Two-factor authentication
- Account management / profile pages
