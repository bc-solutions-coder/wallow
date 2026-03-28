# Frontend Setup Guide

Wallow uses two separate Blazor Server applications for its frontend:

- **`Wallow.Auth`** -- Login, register, password reset, email verification, MFA enrollment
- **`Wallow.Web`** -- Dashboard, settings, public pages

Both are server-rendered Blazor apps that communicate with `Wallow.Api` for backend operations. They share branding configuration via `branding.json` at the repository root.

## Architecture

```
Wallow.Auth (port 5001)  ──► Wallow.Api (port 5000) ◄──  Wallow.Web (port 5003)
       │                           │                           │
       ├─ Login, Register          ├─ OpenIddict OIDC          ├─ Dashboard
       ├─ Password Reset           ├─ REST API                 ├─ Settings
       ├─ Email Verification       ├─ SignalR Realtime         ├─ Organizations
       ├─ MFA Enrollment           │                           ├─ Apps
       └─ Terms / Privacy          │                           └─ Public pages
                                   ▼
                            PostgreSQL / Valkey / GarageHQ
```

## Project Structure

```
src/Wallow.Auth/
├── Components/
│   ├── Layout/
│   │   └── AuthLayout.razor           # Layout for all auth pages
│   ├── Pages/
│   │   ├── Login.razor
│   │   ├── Register.razor
│   │   ├── ForgotPassword.razor
│   │   ├── ResetPassword.razor
│   │   ├── VerifyEmail.razor
│   │   ├── MfaEnroll.razor
│   │   ├── MfaChallenge.razor
│   │   └── ...
│   ├── Shared/
│   │   └── BlazorReadyIndicator.razor
│   ├── BrandingTheme.razor            # CSS variable injection from BrandingOptions
│   └── App.razor
├── Configuration/
│   └── BrandingOptions.cs             # Canonical branding model
├── wwwroot/
└── Wallow.Auth.csproj

src/Wallow.Web/
├── Components/
│   ├── Layout/
│   │   ├── DashboardLayout.razor      # Authenticated dashboard layout
│   │   ├── PublicLayout.razor          # Public-facing layout
│   │   └── MainLayout.razor
│   ├── Pages/
│   │   ├── Home.razor
│   │   └── Dashboard/
│   │       ├── Settings.razor
│   │       ├── Organizations.razor
│   │       ├── Apps.razor
│   │       ├── Inquiries.razor
│   │       └── ...
│   └── Shared/
│       └── BlazorReadyIndicator.razor
├── Configuration/
│   └── BrandingOptions.cs             # Local copy of branding model
├── wwwroot/
└── Wallow.Web.csproj
```

## Running Locally

```bash
# Start infrastructure
cd docker && docker compose up -d

# Start the API (required by both frontends)
dotnet run --project src/Wallow.Api

# Start Auth app (separate terminal)
dotnet run --project src/Wallow.Auth

# Start Web app (separate terminal)
dotnet run --project src/Wallow.Web
```

### Default Dev Credentials

| Field | Value |
|-------|-------|
| Email | `admin@wallow.dev` |
| Password | `Admin123!` |

### Local URLs

| App | URL |
|-----|-----|
| API | http://localhost:5000 |
| Auth | http://localhost:5001 |
| Web | http://localhost:5003 |

## Branding Customization

Edit `branding.json` in the repository root to customize identity across both Auth and Web apps:

```json
{
  "appName": "YourProduct",
  "appIcon": "your-icon.svg",
  "tagline": "Your product tagline",
  "theme": {
    "defaultMode": "dark",
    "light": { "primary": "oklch(0.55 0.15 250)" },
    "dark": { "primary": "oklch(0.65 0.15 250)" }
  }
}
```

Place custom icons in the `wwwroot` directory of `Wallow.Auth` (and `Wallow.Web` if applicable).

### BrandingOptions

`Wallow.Auth` owns the canonical `BrandingOptions` class; `Wallow.Web` has a local copy. Both read from `branding.json` at the repo root. The class exposes `AppName`, `AppIcon`, `Tagline`, `LandingPage`, and `Theme` properties.

### Layouts That Use BrandingOptions

| Layout | Project | What it reads |
|--------|---------|---------------|
| `AuthLayout.razor` | `Wallow.Auth` | AppName (page title), AppIcon, Tagline, Theme |
| `DashboardLayout.razor` | `Wallow.Web` | AppName (sidebar header), AppIcon |
| `PublicLayout.razor` | `Wallow.Web` | AppName (page title), AppIcon, Tagline |

All layouts inject `IOptions<BrandingOptions>` and read the same configuration.

### CSS Variable Customization

Theme colors from `BrandingOptions` are rendered as CSS custom properties by the `BrandingTheme.razor` component. The tokens use OKLCH color format and map to standard shadcn/ui variable names:

```
--background, --foreground, --card, --card-foreground,
--popover, --popover-foreground, --primary, --primary-foreground,
--secondary, --secondary-foreground, --muted, --muted-foreground,
--accent, --accent-foreground, --destructive, --destructive-foreground,
--border, --input, --ring, --radius
```

To override a single token without changing `branding.json`, use environment variables:

```bash
Branding__Theme__Dark__Primary="oklch(0.60 0.20 280)"
```

## Authentication

Wallow uses OpenIddict as its OIDC provider. `Wallow.Auth` handles the authentication UI (login, register, password reset). `Wallow.Web` authenticates users via OpenID Connect against the API.

### OIDC Endpoints (Wallow.Api)

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/connect/authorize` | GET | Start authorization |
| `/connect/token` | POST | Exchange code for tokens |
| `/connect/logout` | GET/POST | End session |
| `/connect/userinfo` | GET/POST | Get user profile claims |

### Pre-Registered Dev Clients

The API seeds two development clients:

**wallow-dev-client** (public, for external frontends):
- Redirect URIs: `http://localhost:5001/callback`, `http://localhost:3000/callback`, `http://localhost:3000/auth/callback`
- PKCE required (S256)
- Scopes: `openid`, `profile`, `email`, `roles`, `offline_access`, plus module-specific scopes

**wallow-web-client** (confidential, for Wallow.Web):
- Redirect URI: `http://localhost:5003/signin-oidc`
- Secret: `wallow-web-secret`
- Scopes: `openid`, `email`, `profile`, `roles`, `offline_access`

## Blazor Readiness

Both apps include a `BlazorReadyIndicator.razor` component that emits `[data-blazor-ready='true']` once the SignalR circuit connects. This is used by E2E tests via `WaitForBlazorReadyAsync(page)`.

## CORS

The API CORS configuration is in `appsettings.Development.json`. Add your frontend origin if running on a non-standard port:

```json
{
  "Cors": {
    "AllowedOrigins": ["https://your-frontend.example.com"]
  }
}
```

## Fork Adaptation

Forks customize identity through configuration, not code changes:

1. Edit `branding.json` for name, icon, tagline, and theme colors
2. Update `appsettings.json` for backend configuration
3. `.gitattributes` marks `branding.json` and `appsettings*.json` as `merge=ours`, so upstream merges preserve fork config

## API Documentation

The Wallow API serves its OpenAPI spec via Scalar at `http://localhost:5000/openapi/v1.json`. The Scalar UI is available at `http://localhost:5000/scalar/v1`.
