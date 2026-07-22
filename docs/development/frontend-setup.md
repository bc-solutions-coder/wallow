# Frontend Setup Guide

Wallow's frontend is two separate TanStack Start (React) applications:

- **`apps/wallow-auth`** -- Login, register, password reset, email verification, MFA enrollment, consent
- **`apps/wallow-web`** -- Dashboard, settings, public pages

Both are part of the pnpm workspace and talk to `Wallow.Api` (the headless backend) for all
backend operations. They share branding configuration via `api/branding.json` at the repository
root, consumed through the `@bc-solutions-coder/styles` package, which also owns the entire
Tailwind v4 build (see [Styling and Tailwind Setup](#styling-and-tailwind-setup)).

`wallow-auth` runs a small [h3](https://h3.unjs.io/) server that same-origin reverse-proxies
`/v1/**`, `/connect/**`, and `/.well-known/**` to the API, so the OIDC endpoints appear on the
auth origin without the browser ever crossing origins. `wallow-web` uses the BFF pattern from
the TypeScript SDK: its server holds the OIDC token set in a session and proxies `/api/**` to
the API with a bearer token attached.

## Architecture

```
apps/wallow-auth (port 3002)  ──► Wallow.Api (port 5001) ◄──  apps/wallow-web (port 3000)
       │                              │                              │
       ├─ Login, Register             ├─ OpenIddict OIDC             ├─ Dashboard
       ├─ Password Reset              ├─ REST API (/v1)              ├─ Settings
       ├─ Email Verification          │                              ├─ Organizations
       ├─ MFA Enroll / Challenge      │  (h3 proxy fronts            ├─ Apps
       ├─ Consent                     │   /connect, /.well-known)    └─ Public pages
       └─ Terms / Privacy             │
                                      ▼
                            PostgreSQL / Valkey / GarageHQ
```

## Project Structure

Both apps follow the standard TanStack Start layout (file-based routing under `src/routes/`,
components under `src/components/`) plus the h3 server that fronts them:

```
apps/wallow-auth/
├── src/
│   ├── routes/                 # File-based routes (login, register, mfa, consent, ...)
│   ├── components/
│   │   └── ready-indicator.tsx # Stamps data-app-ready='true' after hydration
│   └── ...
├── dev-server.ts               # Dev h3 host (same-origin proxy to the API)
├── server.ts                   # Production h3 host
├── playwright.config.ts        # E2E config (data-testid selectors, port 3002)
├── e2e/                        # @playwright/test specs
└── package.json

apps/wallow-web/
├── src/
│   ├── routes/                 # Dashboard, settings, public pages
│   └── components/
│       └── ready-indicator.tsx # Stamps data-app-ready='true' after hydration
├── dev-server.ts               # Dev h3 host (BFF)
├── server.ts                   # Production h3 host (BFF)
├── playwright.config.ts        # E2E config (data-testid selectors, port 3000)
├── e2e/                        # @playwright/test specs
└── package.json
```

## Testing

Frontend specs run under **Vitest 4 browser mode**: any component/DOM test executes in real
headless Chromium via the Vitest `playwright` provider (`@vitest/browser-playwright` +
`vitest-browser-react`) — **jsdom, happy-dom, and jest are not used**. `pnpm test` is the same
command as before but now drives a real browser for component specs; each app's `vitest.config.ts`
splits a `node` project (pure-logic `*.test.ts`) from a `browser` project (`*.test.tsx`).

End-to-end tests are per-app `@playwright/test` suites (`apps/wallow-auth/e2e/`,
`apps/wallow-web/e2e/`), run with `pnpm --filter ./apps/<app> test:e2e` or the one-command
`./scripts/e2e.sh` runner. See `.claude/rules/TESTING.md` and `.claude/rules/E2E.md` for the full
rules.

## Running Locally

```bash
# Start infrastructure
pnpm backend:infra

# Start the API (required by both frontends) plus the rest of the stack via Aspire
pnpm backend

# Build the SDK first (apps typecheck and run against dist/)
pnpm --filter @bc-solutions-coder/sdk build

# Start the Auth app (separate terminal)
pnpm --filter @bc-solutions-coder/wallow-auth dev

# Start the Web app (separate terminal)
pnpm --filter @bc-solutions-coder/wallow-web dev
```

### Default Dev Credentials

| Field | Value |
|-------|-------|
| Email | `admin@wallow.dev` |
| Password | `Admin123!` |

### Local URLs

| App | URL |
|-----|-----|
| API | http://localhost:5001 |
| Web (TanStack) | http://localhost:3000 |
| Auth (TanStack) | http://localhost:3002 |

The TanStack apps read `PORT` from the environment and fall back to the defaults above. Keep any
new local port clear of those and of Grafana on 3001.

## Styling and Tailwind Setup

`@bc-solutions-coder/styles` owns the entire Tailwind v4 pipeline: the Tailwind compiler plugin,
the brand-assets (icon/logo) static-file wiring, and the theme token CSS emitted from
`api/branding.json`. Bootstrapping a new TanStack Start app in this workspace needs only three
steps:

1. **Add the workspace dependency** to the app's `package.json`:

   ```json
   {
     "dependencies": {
       "@bc-solutions-coder/styles": "workspace:*"
     }
   }
   ```

   No `@tailwindcss/vite` or `tailwindcss` devDependency is needed — `@bc-solutions-coder/styles`
   depends on both directly and re-exports the Vite plugin.

2. **Register `wallowStyles()`** (from `@bc-solutions-coder/styles/vite`) in the app's Vite
   plugin list — in `vite.config.ts`, and in `dev-server.ts` too if the app runs a custom dev
   server:

   ```ts
   import { wallowStyles } from "@bc-solutions-coder/styles/vite";

   export default defineConfig({
     plugins: [react(), ...wallowStyles()],
     // ...
   });
   ```

   `wallowStyles()` returns a `PluginOption[]` containing the Tailwind compiler plugin and a
   brand-assets plugin that points `publicDir` at the shared package's `assets/` directory
   (brand icon, etc.) through its own `config()` hook — the app never sets `publicDir` itself.

3. **Create the CSS entry** at `src/styles.css`, exactly two lines:

   ```css
   @import "@bc-solutions-coder/styles/styles.css";
   @source "./";
   ```

   The `@import` pulls in the Tailwind base layer and the branding-driven theme tokens. The
   `@source "./"` line is the one thing an app must always own: Tailwind v4 resolves `@source`
   paths relative to the declaring stylesheet, so the shared package can never scan an app's own
   component tree for utility classes on its behalf.

   Import that file once, from the app's client entry point (`src/client.tsx`):

   ```ts
   import "./styles.css";
   ```

That's the entire setup — nothing else is required. No per-app `@tailwindcss/vite`
devDependency, no manual `publicDir` wiring, no explanatory boilerplate duplicated into the
app's own CSS file (the shared package's `styles.css` already documents the `@source`
constraint).

### Docker builds

Because the app's Tailwind build depends on `@bc-solutions-coder/styles` and, transitively, on
`api/branding.json`, an app's Dockerfile must, before building the app image:

- `COPY packages/styles/package.json packages/styles/` alongside the other workspace manifests
  (before `pnpm install --frozen-lockfile`)
- `COPY packages/styles packages/styles` and `COPY api/branding.json api/branding.json` (before
  the build step)
- Build the styles package before the app, e.g.
  `pnpm --filter @bc-solutions-coder/styles build`, since the app's Vite build imports the
  package's built `dist/` output (brand asset paths), not its source

`apps/wallow-auth/Dockerfile` and `apps/wallow-web/Dockerfile` are the reference examples.

## Branding Customization

Edit `api/branding.json` in the repository root to customize identity across both apps:

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

### Branding Ownership

The canonical branding schema lives in `packages/styles` (`@bc-solutions-coder/styles`,
`src/branding.ts`), the TypeScript source of truth that parses `api/branding.json` and emits the
theme CSS every frontend consumes. It exposes `appName`, `appIcon`, `tagline`, `landingPage`, and
`theme`.

### CSS Variable Customization

Theme colors from `branding.json` are emitted as CSS custom properties by
`@bc-solutions-coder/styles`. The tokens use OKLCH color format and map to standard shadcn/ui
variable names:

```
--background, --foreground, --card, --card-foreground,
--popover, --popover-foreground, --primary, --primary-foreground,
--secondary, --secondary-foreground, --muted, --muted-foreground,
--accent, --accent-foreground, --destructive, --destructive-foreground,
--border, --input, --ring, --radius
```

## Authentication

Wallow uses OpenIddict as its OIDC provider, hosted in `Wallow.Api` (wired up in
`Wallow.Identity.Infrastructure`). `apps/wallow-auth` provides the authentication UI (login,
register, password reset, consent) and serves the OIDC endpoints on its own origin by
same-origin proxying them to the API. `apps/wallow-web` authenticates users via OpenID Connect
through its BFF server.

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

**wallow-web-client** (confidential, for `apps/wallow-web`):
- Redirect URI: `http://localhost:3000/auth/callback`
- Secret: `wallow-web-secret`
- Scopes: `openid`, `email`, `profile`, `roles`, `offline_access`

## React Readiness

Both apps stamp `[data-app-ready='true']` on the document once React hydration completes (emitted
by `src/components/ready-indicator.tsx`). E2E tests wait for this marker before interacting with
the page.

## CORS

The API CORS configuration is in `appsettings.Development.json`. Add your frontend origin if
running on a non-standard port:

```json
{
  "Cors": {
    "AllowedOrigins": ["https://your-frontend.example.com"]
  }
}
```

## Fork Adaptation

Forks customize identity through configuration, not code changes:

1. Edit `api/branding.json` for name, icon, tagline, and theme colors
2. Update `appsettings.json` for backend configuration
3. `.gitattributes` marks `branding.json` and `appsettings*.json` as `merge=ours`, so upstream merges preserve fork config

## API Documentation

The Wallow API serves its OpenAPI spec via Scalar at `http://localhost:5001/openapi/v1.json`. The
Scalar UI is available at `http://localhost:5001/scalar/v1`.
