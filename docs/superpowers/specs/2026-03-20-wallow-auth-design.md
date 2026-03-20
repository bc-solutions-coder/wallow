# Wallow.Auth — Login & Register Pages Design

## Overview

A new **Wallow.Auth** Blazor Web App project that serves authentication pages on `auth.wallow.dev`. It replaces the existing unstyled Razor Pages in `Wallow.Api` with polished, modern auth pages built with Blazor Blueprint UI.

## Architecture

### Subdomain Layout

| Subdomain | Project | Purpose | Status |
|-----------|---------|---------|--------|
| `wallow.dev` | Wallow.Web | Marketing site | Future |
| `auth.wallow.dev` | **Wallow.Auth** | Auth pages (login, register, etc.) | **Building now** |
| `api.wallow.dev` | Wallow.Api | API + OpenIddict server | Existing |

### Project Details

- **Project name:** `Wallow.Auth`
- **Location:** `src/Wallow.Auth/`
- **Type:** Blazor Web App (.NET 10) — SSR with InteractiveServer components where needed
- **UI library:** Blazor Blueprint UI (`BlazorBlueprint.Components`)
- **Hosting:** Standalone process on `auth.wallow.dev`

### Core Architectural Decision: Auth Proxy Pattern

Because OpenIddict runs inside `Wallow.Api` and relies on ASP.NET Identity cookies to identify authenticated users, **Wallow.Auth cannot set that cookie directly** — it runs in a separate process. Instead, Wallow.Auth acts as a **UI frontend** that delegates all authentication operations to dedicated API endpoints on `Wallow.Api`.

The flow:

1. Wallow.Auth renders the login/register UI
2. User submits credentials (or uses social login)
3. Wallow.Auth calls Wallow.Api auth endpoints over HTTP
4. Wallow.Api validates credentials, sets the Identity cookie on the `.wallow.dev` domain
5. The browser now has the cookie → OpenIddict's authorize endpoint can read it

This means the Identity cookie must be set by `Wallow.Api` (which owns ASP.NET Identity), not by `Wallow.Auth`. The cookie domain is `.wallow.dev` so it's readable across all subdomains.

### Cookie Architecture

The Identity cookie is set exclusively by `Wallow.Api` (which owns ASP.NET Identity) and read exclusively by `Wallow.Api` (where OpenIddict runs). Wallow.Auth never reads or writes this cookie — it only triggers cookie creation by calling API endpoints.

**Production:**
- Cookie domain: `.wallow.dev` (shared across all subdomains)
- Cookie set by: `Wallow.Api` via a new login API endpoint that returns `Set-Cookie` headers
- Cookie flags: `HttpOnly`, `Secure`, `SameSite=Lax`

**`SameSite=Lax` note:** The login flow works with `Lax` because: (1) the cookie is *set* via a cross-origin POST response `Set-Cookie` header, which browsers accept regardless of `SameSite`; (2) the subsequent redirect to `api.wallow.dev/connect/authorize` is a top-level GET navigation, which `Lax` allows. Do not change to `SameSite=None` — that would weaken CSRF protection unnecessarily.

**Local development:**
- Both apps run on `localhost` (different ports: API on 5000, Auth on 5002)
- Cookie domain: omitted (defaults to `localhost`, shared across ports)
- `ApiBaseUrl`: `http://localhost:5000`

**Data protection:** Since only `Wallow.Api` sets and reads the Identity cookie, there is no need for shared data protection keys between the two apps. Wallow.Auth has no data protection key requirements beyond Blazor's defaults.

### Relationship to Existing Code

Wallow.Auth is a **standalone frontend project** with no direct references to Wallow module projects. It communicates with Wallow.Api exclusively over HTTP.

The existing Razor Pages in `Wallow.Api/Pages/Account/` (Login, Logout, Consent) remain in place initially. OpenIddict's unauthenticated redirect will be updated to point to `auth.wallow.dev/login`. The old Razor Pages can be removed once Wallow.Auth is verified working.

## Pages

### Login (`/login`)

**Fields:**
- Email input
- Password input
- "Remember me" checkbox
- "Forgot password?" link → `/forgot-password`

**Social login buttons (OIDC providers):**
- Google
- GitHub
- Microsoft
- Apple

**Additional elements:**
- "Don't have an account? Register" link → `/register`
- Error message display area

**Behavior:**
1. User submits email + password
2. Wallow.Auth calls `POST api.wallow.dev/api/v1/identity/auth/login` (new cookie-based login endpoint)
3. The API validates credentials via `SignInManager.PasswordSignInAsync`, sets the Identity cookie on `.wallow.dev`
4. On success: Wallow.Auth redirects to `returnUrl` (the OpenIddict authorize URL)
5. On failure: displays error message inline. Specific error cases:
   - Invalid credentials → "Invalid email or password"
   - Account locked out → "Account locked. Try again later."
   - Email not confirmed → "Please verify your email before signing in. [Resend verification email]"
   - Other failure → generic error message
6. "Remember me": passed to the API, which controls `isPersistent` on the cookie

**No `returnUrl`:** If no `returnUrl` is present (user navigated directly to `/login`), show a "You are now signed in" confirmation page after successful login. No redirect.

**Social login:** See "Social Login Architecture" section below.

### Register (`/register`)

**Fields:**
- Email input
- Password input
- Confirm password input
- "I agree to the Terms of Service" checkbox (links to `/terms`)
- "I agree to the Privacy Policy" checkbox (links to `/privacy`)

**Social signup buttons:**
- Same providers as login (Google, GitHub, Microsoft, Apple)

**Additional elements:**
- "Already have an account? Sign in" link → `/login`
- Error message display area
- Password strength indicator

**Behavior:**
1. Client-side validation: email format, password match, password strength, checkboxes checked
2. Wallow.Auth calls `POST api.wallow.dev/api/v1/identity/auth/register`
3. API creates the user via `UserManager.CreateAsync`, sends verification email
4. On success: redirects to `/verify-email` confirmation page
5. On failure: displays error messages inline (password too weak, email taken, etc.)

### Forgot Password (`/forgot-password`)

**Fields:**
- Email input

**Behavior:**
1. User submits email
2. Calls `POST api.wallow.dev/api/v1/identity/auth/forgot-password`
3. Shows confirmation message regardless of whether email exists (prevents user enumeration)
4. API generates a password reset token and sends email with link to `auth.wallow.dev/reset-password?token=...&email=...`

### Reset Password (`/reset-password`)

Accessed via link in the password reset email, with token and email in the query string.

**Fields:**
- New password input
- Confirm new password input

**Behavior:**
1. Reads token and email from query string
2. User submits new password
3. Calls `POST api.wallow.dev/api/v1/identity/auth/reset-password` with `{ email, token, newPassword }`
4. On success: redirects to `/login` with success message

### Email Verification (`/verify-email`)

**Two modes:**
1. **Post-registration confirmation** (`/verify-email`): Static page saying "Check your email for a verification link"
2. **Verification landing** (`/verify-email/confirm?token=...&email=...`): Calls `GET api.wallow.dev/api/v1/identity/auth/verify-email?token=...&email=...` to confirm, shows success or failure message with link to login

### Logout (`/logout`)

**Behavior:**
1. User or client app initiates logout via `GET api.wallow.dev/connect/logout`
2. `LogoutController` GET handler reads `post_logout_redirect_uri` from the OpenIddict server request (`HttpContext.GetOpenIddictServerRequest()`) and redirects to `auth.wallow.dev/logout?post_logout_redirect_uri=...`
3. Wallow.Auth shows "Are you sure you want to sign out?" with a confirm button
4. On confirm: Wallow.Auth redirects the browser to `POST api.wallow.dev/api/v1/identity/auth/sign-out` via a form submission (not a fetch/XHR call). This ensures:
   - `SignInManager.SignOutAsync()` clears the Identity cookie
   - `SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)` revokes the OpenIddict authorization/tokens
   - Both operations happen in the same request within `Wallow.Api`
5. The new `/auth/sign-out` endpoint, after sign-out, redirects to `auth.wallow.dev/logout?signed_out=true&post_logout_redirect_uri=...`
6. Wallow.Auth shows "You have been signed out" message
7. If `post_logout_redirect_uri` was provided, shows a "Return to application" link

**Why form POST instead of fetch:** The logout must happen within `Wallow.Api`'s process to call both `SignInManager.SignOutAsync()` and OpenIddict's `SignOut`. A cross-origin fetch would miss OpenIddict token revocation. A form POST navigates the browser directly to the API domain, avoiding CORS issues.

**CSRF protection for sign-out:** The new `/auth/sign-out` endpoint does not use `[ValidateAntiForgeryToken]` (which only works same-origin). Instead, it requires a valid Identity cookie — only an authenticated user can sign themselves out. The existing `LogoutController.LogoutPost` with `[ValidateAntiForgeryToken]` is left unchanged for backward compatibility but is no longer the primary logout path.

### OAuth Consent (`/consent`)

The consent page requires access to OpenIddict's server request context (`HttpContext.GetOpenIddictServerRequest()`), which is only available inside the process hosting the OpenIddict server. Therefore, **the consent page stays in `Wallow.Api`** but is restyled.

**Approach:** The existing `Consent.cshtml` Razor Page in Wallow.Api will be updated to use a modern dark theme that visually matches Wallow.Auth pages. This is simpler and more correct than trying to proxy the OpenIddict request context across HTTP.

**Elements:**
- Application name and description
- List of requested scopes with friendly names and descriptions
- "Allow" and "Deny" buttons
- Visual styling consistent with Wallow.Auth pages

## Social Login Architecture

External OIDC providers must be handled by `Wallow.Api`, not `Wallow.Auth`, because:
- Provider callbacks need to create/link `WallowUser` records (requires `UserManager`)
- The Identity cookie must be set by the process that owns ASP.NET Identity
- External login schemes (`IdentityConstants.ExternalScheme`) are configured in the Identity module

**Flow:**
1. User clicks "Sign in with Google" on `auth.wallow.dev/login`
2. Wallow.Auth redirects to `api.wallow.dev/api/v1/identity/auth/external-login?provider=Google&returnUrl=...`
3. Wallow.Api issues a `Challenge` to the external provider (Google OIDC)
4. Provider redirects back to `api.wallow.dev/api/v1/identity/auth/external-login-callback`
5. Wallow.Api processes the external login:
   - If user exists: signs in via `SignInManager.ExternalLoginSignInAsync`, sets Identity cookie
   - If new user: creates account via `UserManager.CreateAsync`, links external login, sets cookie
6. Redirects back to `returnUrl` (the original OpenIddict authorize URL)

**`returnUrl` chain:** The full redirect chain preserves the original destination:
```
client app → api.wallow.dev/connect/authorize (OpenIddict stores client redirect_uri)
  → auth.wallow.dev/login?returnUrl=<encoded authorize URL>
    → user clicks "Sign in with Google"
    → api.wallow.dev/api/v1/identity/auth/external-login?provider=Google&returnUrl=<same encoded authorize URL>
      → Google OIDC
      → api.wallow.dev/api/v1/identity/auth/external-login-callback (reads returnUrl from auth properties)
        → redirects to <authorize URL> (OpenIddict completes the flow)
```

**Social login and Terms of Service:** When an external provider creates a new user, the callback redirects to `auth.wallow.dev/complete-registration?returnUrl=...` instead of directly completing the flow. This page shows ToS and Privacy Policy checkboxes. On acceptance, it calls `POST /api/v1/identity/auth/accept-terms` which marks the user's ToS acceptance and then redirects to the `returnUrl`. Existing users who have already accepted skip this step.

**Provider configuration** lives in `Wallow.Api`'s Identity module (not in Wallow.Auth), since that's where ASP.NET Identity's external authentication schemes are registered.

## Visual Design

### Layout

- **Minimal full-width** — no card borders, form floating on dark background
- Wallow logo/wordmark centered at top
- Form fields centered, max-width ~400px
- Social buttons full-width within form container

### Theme

- Dark mode using Blazor Blueprint UI's default dark theme
- Clean sans-serif typography
- Accent color for primary buttons and links (indigo/violet range from Blueprint defaults)

### Component Usage (Blazor Blueprint UI)

| Element | Blueprint Component |
|---------|-------------------|
| Email/password fields | `BbInput` |
| Submit buttons | `BbButton` |
| Checkboxes | `BbCheckbox` |
| Social login buttons | `BbButton` with variant + provider icons |
| Error messages | `BbAlert` |
| Password strength | `BbProgress` |
| Loading states | `BbButton` loading prop |

## Auth Flow Integration

### Authorization Code Flow (Primary)

```
1. Client app → GET api.wallow.dev/connect/authorize?client_id=...&redirect_uri=...
2. OpenIddict checks for Identity cookie → not found
3. Wallow.Api redirects → auth.wallow.dev/login?returnUrl=<encoded authorize URL on api.wallow.dev>
4. User enters credentials on auth.wallow.dev
5. Wallow.Auth POSTs to api.wallow.dev/api/v1/identity/auth/login (with credentials + CORS)
6. Wallow.Api validates via SignInManager, sets Identity cookie on .wallow.dev domain
7. Wallow.Auth redirects browser to returnUrl (api.wallow.dev/connect/authorize)
8. OpenIddict checks for Identity cookie → found, user authenticated
9. OpenIddict issues authorization code → redirects to client's redirect_uri
10. Client exchanges code for tokens via POST api.wallow.dev/connect/token
```

### CORS Configuration

Wallow.Api must allow cross-origin requests from `auth.wallow.dev`:
- `Access-Control-Allow-Origin: https://auth.wallow.dev`
- `Access-Control-Allow-Credentials: true` (required for cookie operations)
- Allowed methods: `GET`, `POST`

**Local development:** The existing Development CORS policy must also include `http://localhost:5002` (the local Wallow.Auth URL) alongside the existing allowed origins.

**Scope:** The CORS policy with credentials applies only to the `/api/v1/identity/auth/` path prefix. The OpenIddict endpoints (`/connect/*`) do not need CORS — they are accessed via browser navigation (redirects), not XHR/fetch.

## API Endpoints

### Existing (in Wallow.Api — functional)

| Endpoint | Purpose |
|----------|---------|
| `GET/POST /connect/authorize` | OpenIddict authorize endpoint |
| `POST /connect/token` | OpenIddict token endpoint |
| `GET /connect/logout` | OpenIddict logout endpoint |

### Existing (in Wallow.Api — need `ITokenService` implementation)

| Endpoint | Purpose | Note |
|----------|---------|------|
| `POST /api/v1/identity/auth/token` | Get tokens (BFF) | `ITokenService` not yet implemented |
| `POST /api/v1/identity/auth/refresh` | Refresh token (BFF) | `ITokenService` not yet implemented |
| `POST /api/v1/identity/auth/logout` | Revoke token (BFF) | `ITokenService` not yet implemented |

These BFF endpoints are not used by Wallow.Auth's login flow (which uses cookie-based auth instead), but they need `ITokenService` implemented for SPA/mobile clients.

**Naming distinction — `logout` vs `sign-out`:** The existing `POST /auth/logout` endpoint revokes bearer tokens (for SPA/mobile clients). The new `POST /auth/sign-out` endpoint clears the Identity cookie and revokes OpenIddict authorizations (for browser-based flows via Wallow.Auth). These are different operations for different client types and both must coexist. The logout page in Wallow.Auth uses only `/auth/sign-out` — it does not use the existing `/connect/logout` POST handler or the BFF `/auth/logout` endpoint.

### New Endpoints (to be added to Wallow.Api)

| Endpoint | Purpose | Sets Cookie? |
|----------|---------|-------------|
| `POST /api/v1/identity/auth/login` | Validate credentials, set Identity cookie (see response contract below) | Yes |
| `POST /api/v1/identity/auth/sign-out` | Clear Identity cookie + revoke OpenIddict tokens | Yes (clears) |
| `POST /api/v1/identity/auth/register` | Create user, send verification email | No |
| `POST /api/v1/identity/auth/forgot-password` | Send password reset email | No |
| `POST /api/v1/identity/auth/reset-password` | Reset password with token | No |
| `GET /api/v1/identity/auth/verify-email` | Confirm email with token | No |
| `GET /api/v1/identity/auth/external-login` | Initiate external provider challenge | No |
| `GET /api/v1/identity/auth/external-login-callback` | Handle external provider callback | Yes |
| `POST /api/v1/identity/auth/accept-terms` | Record ToS acceptance for social signup users | No |

### Login Endpoint Response Contract

`POST /api/v1/identity/auth/login` returns:

| Status | Body | Meaning |
|--------|------|---------|
| `200 OK` | `{ "succeeded": true }` | Login successful, Identity cookie set via `Set-Cookie` header |
| `401 Unauthorized` | `{ "error": "invalid_credentials" }` | Wrong email or password |
| `403 Forbidden` | `{ "error": "email_not_confirmed" }` | Email not yet verified |
| `423 Locked` | `{ "error": "locked_out" }` | Account temporarily locked |

Wallow.Auth's `AuthApiClient` uses the `error` field to determine which message to display.

## Project Structure

```
src/Wallow.Auth/
├── Wallow.Auth.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Properties/
│   └── launchSettings.json
├── Components/
│   ├── App.razor
│   ├── Routes.razor
│   ├── _Imports.razor
│   ├── Layout/
│   │   └── AuthLayout.razor          # Minimal dark layout wrapper
│   └── Pages/
│       ├── Login.razor
│       ├── Register.razor
│       ├── ForgotPassword.razor
│       ├── ResetPassword.razor
│       ├── VerifyEmail.razor
│       ├── VerifyEmailConfirm.razor
│       ├── Logout.razor
│       └── CompleteRegistration.razor  # ToS consent for social signup
├── Services/
│   ├── IAuthApiClient.cs             # Interface for API communication
│   └── AuthApiClient.cs              # HttpClient wrapper for auth endpoints
├── Models/
│   ├── LoginRequest.cs
│   ├── RegisterRequest.cs
│   ├── ForgotPasswordRequest.cs
│   └── ResetPasswordRequest.cs
└── wwwroot/
    ├── css/
    │   └── theme.css                 # Blueprint theme overrides
    └── favicon.ico
```

## Configuration

### appsettings.json

```json
{
  "ApiBaseUrl": "https://api.wallow.dev"
}
```

### appsettings.Development.json

```json
{
  "ApiBaseUrl": "http://localhost:5000"
}
```

Social login provider credentials are configured in **Wallow.Api** (not Wallow.Auth), since external auth schemes are registered in the Identity module.

### Local Development

| Service | URL |
|---------|-----|
| Wallow.Auth | `http://localhost:5002` |
| Wallow.Api | `http://localhost:5000` (existing) |

### Data Protection

Wallow.Auth does not need shared data protection keys with Wallow.Api. The Identity cookie is set and read exclusively by Wallow.Api. Wallow.Auth uses Blazor's default data protection for its own antiforgery tokens, which is independent.

## Wallow.Api Changes Required

### 1. New Auth Endpoints

Add a new `AccountController` (or extend `AuthController`) with cookie-based login/register/logout endpoints as listed above. Also update the stale XML documentation on `AuthController` which still references Keycloak — it should accurately describe the OpenIddict-backed BFF endpoints.

### 2. Update OpenIddict Redirect

Change the unauthenticated redirect in `AuthorizationController` from:
```
/Account/Login?returnUrl=...
```
to:
```
https://auth.wallow.dev/login?returnUrl=...
```
(Configurable via `appsettings.json` so local dev can use `http://localhost:5002`.)

### 3. Update LogoutController Redirect

Change redirect from `/Account/Logout` to `https://auth.wallow.dev/logout`.

### 4. CORS Configuration

Add CORS policy allowing `auth.wallow.dev` with credentials.

### 5. Cookie Domain Configuration

Configure the Identity cookie domain to `.wallow.dev` in production:
```csharp
services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Domain = ".wallow.dev"; // production
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
```

### 6. Restyle Consent Page

Update `Consent.cshtml` to use a dark theme matching Wallow.Auth's visual style. This page stays in Wallow.Api because it needs OpenIddict server request context.

**Note:** The `AuthorizationController` currently auto-approves all authorization requests for authenticated users — it never redirects to the consent page. Wiring up the consent redirect (for first-time scope grants or when new scopes are requested) is a separate enhancement. For now, the consent page is restyled but remains reachable only via direct navigation or future controller changes.

### 7. Social Login Provider Registration

Add external authentication schemes for Google, GitHub, Microsoft, and Apple in the Identity module's service registration.

## Security Considerations

- All pages served over HTTPS in production
- CSRF protection on all form submissions (Blazor's built-in antiforgery for same-origin forms)
- Logout uses a form POST directly to `api.wallow.dev/connect/logout` (same-origin from the browser's perspective during navigation), avoiding cross-origin CSRF concerns
- Rate limiting on login/register API endpoints
- Password strength validation client-side and server-side
- Email enumeration prevention on forgot-password (always show generic success)
- Cookie: `HttpOnly`, `Secure`, `SameSite=Lax`, domain `.wallow.dev`
- CORS restricted to `auth.wallow.dev` origin only (with credentials)

## Out of Scope

- Marketing site (`wallow.dev` / Wallow.Web)
- Two-factor authentication (future enhancement)
- Account management / profile pages (future enhancement)
- Admin dashboard (future enhancement)
- `ITokenService` implementation for BFF endpoints (separate task)
