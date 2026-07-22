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

## New App Bootstrap

A new TanStack Start app in this workspace is almost entirely wiring into five
`@bc-solutions-coder` packages. The app owns only its own routes, router, and
backend-facing slices; everything cross-cutting (styling, components, the auth
client, the test harness, and the host runtime) comes from the shared packages.

| Package | Published | Entry points | What a new app pulls from it |
|---------|-----------|--------------|------------------------------|
| `@bc-solutions-coder/styles` | yes | `.`, `./styles.css`, `./vite`, `./assets` | Tailwind v4 pipeline plugin (`wallowStyles()`), theme-token CSS, brand assets, the branding schema |
| `@bc-solutions-coder/ui` | no (`private`) | `.`, `./source.css` | Shared browser components/primitives (`Button`, `Input`, `Label`, `Field`, `Card`, `ErrorBanner`, `MutedText`, `CenteredCardLayout`, `ForkAttribution`) plus `ReadyIndicator`/`FocusOnNavigate`, and the Tailwind `@source` scan of its component tree |
| `@bc-solutions-coder/sdk` | yes | `.`, `./server` | Browser BFF client + typed API operations (`.`); BFF handlers, API proxy, and session stores (`./server`) |
| `@bc-solutions-coder/testing` | no (`private`) | `.`, `./render` | The `createVitestProjects` node + browser preset (`.`); the browser-mode `render` helper (`./render`) |
| `@bc-solutions-coder/web-shell` | no (`private`) | `.`, `./server` | `createQueryClient` (`.`); the standalone host runtime and Vite/dev-server presets (`./server`): `createStandaloneHost`/`ShellConfig`, `createDevServer`/`DevServerConfig`, `createClientViteConfig`/`createSsrViteConfig`, and the static-asset reader |

`wallow-auth` and `wallow-web` both depend on all five as `workspace:*` runtime
`dependencies` (no per-app `@tailwindcss/vite`, `tailwindcss`, `vitest` preset,
or host-runtime code of their own). Bootstrapping a new app is these steps.

### 1. Depend on all five packages

In the app's `package.json` `dependencies` (not `devDependencies` — the host
runtime and Vite presets are imported by `server.ts`/`vite.config.ts` at build
time):

```json
{
  "dependencies": {
    "@bc-solutions-coder/sdk": "workspace:*",
    "@bc-solutions-coder/styles": "workspace:*",
    "@bc-solutions-coder/testing": "workspace:*",
    "@bc-solutions-coder/ui": "workspace:*",
    "@bc-solutions-coder/web-shell": "workspace:*"
  }
}
```

### 2. CSS entry (`src/styles.css`) — three lines

```css
@import "@bc-solutions-coder/styles/styles.css";
@import "@bc-solutions-coder/ui/source.css";
@source "./";
```

The first line pulls in the Tailwind base layer and branding-driven theme
tokens. The second makes Tailwind scan `@bc-solutions-coder/ui`'s component tree
(Tailwind v4 skips `node_modules`, so the package ships its own `@source`
declaration for the app to import). The `@source "./"` is the one line every app
must own — Tailwind resolves `@source` relative to the declaring stylesheet, so
the shared packages cannot scan the app's own components on its behalf. Import
this file once from the app's client entry (`src/client.tsx`):
`import "./styles.css";`. See [Styling and Tailwind Setup](#styling-and-tailwind-setup)
for the full rationale.

### 3. Vite config via the web-shell preset (`vite.config.ts`)

The client-bundle preset owns the whole config — the `react()` + `wallowStyles()`
plugin set and the stable unhashed `dist/client/client.js` output the host and
document shell depend on. The only per-app knob is `appDir`:

```ts
import { createClientViteConfig } from "@bc-solutions-coder/web-shell/server";
import { defineConfig } from "vite";

export default defineConfig(createClientViteConfig({ appDir: import.meta.dirname }));
```

`createSsrViteConfig` is the matching preset for the SSR build. Because the
preset composes `wallowStyles()`, the app never imports it directly.

### 4. Vitest config via the testing preset (`vitest.config.ts`)

`createVitestProjects` returns the node + headless-Chromium two-project split.
A simple app supplies only its `nodeTsxSpecs` (the `*.test.tsx` specs that render
via `react-dom/server` and never mount a live DOM, so they stay on the node
project); everything else defaults from the preset:

```ts
import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

const { node, browser } = createVitestProjects({ nodeTsxSpecs: [] });

export default defineConfig({ test: { projects: [node, browser] } });
```

`createVitestProjects` also accepts `extraBrowserOptimizeDeps` and
`nodeProjectOverrides` for apps that need them (see `apps/wallow-web/vitest.config.ts`,
which inlines the SDK and aliases `openid-client` for its BFF specs).

### 5. Host files via the web-shell factories (`server.ts`, `dev-server.ts`)

Both host files are thin config objects handed to a `web-shell/server` factory;
all host behavior lives in the factory. `server.ts` (`pnpm start`, ~30 lines) is
the standalone SSR host the Dockerfile/E2E container runs:

```ts
import { createStandaloneHost, type ShellConfig } from "@bc-solutions-coder/web-shell/server";

const config: ShellConfig = {
  appName: "my-app",
  defaultPort: "3010",
  appDir: import.meta.dirname,
  isProxyPath,        // the app's proxy topology (below)
  handleProxy,        // web Request -> Response bridge to the app's proxy/BFF
  // clientIpHeader?  // optional, e.g. wallow-auth forwards the peer address
};

await createStandaloneHost(config);
```

`dev-server.ts` (`pnpm dev`) hands a `DevServerConfig` to `createDevServer` — the
same `appName`/`defaultPort`/`appDir`/`isProxyPath`, plus a `loadProxyHandler`
that `ssrLoadModule`s the proxy bridge and `reactPluginInDev: false` (Fast
Refresh's preamble breaks whole-document hydration). It runs a bit longer than
`server.ts` because it wires that lazy proxy loader; `apps/wallow-auth/dev-server.ts`
is the reference.

### 6. What the app still owns

The shared packages leave exactly the app-specific surface to the app:

- **Router, routes, and route components** — file-based routing under `src/routes/`,
  composing `@bc-solutions-coder/ui` primitives (`import { Button, Card } from "@bc-solutions-coder/ui";`).
- **Proxy topology** — the `isProxyPath` predicate and `handleProxy` bridge
  (`src/lib/proxy-*.ts`): a same-origin reverse proxy like wallow-auth's
  `createAuthServer`, or a BFF token tunnel like wallow-web's `handleBffRequest`.
  These are the `ShellConfig` seam the design intentionally keeps per-app.
- **Backend-facing slices** — its own `src/features/**` calling the SDK's typed
  operations, and its client-configuration facade (`configureBffClient` /
  `configureSsrClient`).

Branding, theme tokens, the component library, the auth client, the test harness,
and the host/dev/Vite runtime all stay in the shared packages — no source changes
needed to rebrand, and nothing cross-cutting is duplicated into the app.

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
   In this repo the two apps do not call `wallowStyles()` by hand: the `@bc-solutions-coder/web-shell`
   client/SSR presets (`createClientViteConfig`/`createSsrViteConfig`) compose it in for them
   (see [New App Bootstrap](#new-app-bootstrap)).

3. **Create the CSS entry** at `src/styles.css`, three lines:

   ```css
   @import "@bc-solutions-coder/styles/styles.css";
   @import "@bc-solutions-coder/ui/source.css";
   @source "./";
   ```

   The first `@import` pulls in the Tailwind base layer and the branding-driven theme tokens. The
   second imports `@bc-solutions-coder/ui`'s own `@source` declaration so Tailwind scans the shared
   component library's class names (omit it if the app does not use `@bc-solutions-coder/ui`). The
   `@source "./"` line is the one thing an app must always own: Tailwind v4 resolves `@source`
   paths relative to the declaring stylesheet, so a shared package can never scan an app's own
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

### Adding a New Design Token

Adding a new semantic design token (e.g. a `warning` or `success` color) touches exactly
**two files** — nothing per-app needs to change:

1. **`api/branding.json`** — add the new key under both `theme.light` and `theme.dark` with
   an OKLCH value.
2. **`packages/styles/styles.css`** — add the matching `@theme` mapping (e.g.
   `--color-warning: var(--warning);`) so Tailwind exposes it as a utility class.

`packages/styles/src/branding.ts` parses `api/branding.json` and emits every `theme.light`/
`theme.dark` key as a CSS custom property at render time, so no app-level code references the
token directly — apps just use the Tailwind utility (`bg-warning`, `text-warning`, etc.) once
it exists in `styles.css`. `packages/styles/src/theme-css.test.ts` guards this rule: it asserts
every CSS variable emitted from `forkBranding.theme` has a corresponding `@theme` mapping in
`styles.css`, so a forgotten step 2 fails the build instead of silently rendering an unstyled
token.

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
