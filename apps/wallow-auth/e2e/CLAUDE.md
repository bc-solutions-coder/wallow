# wallow-auth E2E — Playwright Agent Guide

Rules for `apps/wallow-auth/e2e/`. This is the reference pattern for a per-app Playwright suite.

## This is Playwright, not Vitest browser mode — do not conflate them

Both drive a real Chromium, but they are separate suites with separate configs and commands:

- **Vitest browser mode** (`src/**/*.test.tsx`, run by `pnpm test`) — **isolated component render,
  no dev server.** Mounts one component in a headless Chromium iframe via the Vitest `playwright`
  provider. It is a unit/component test, not E2E, and boots neither the app nor a backend. Its
  rules live in `.claude/rules/TESTING.md`.
- **Playwright `e2e/` suites** (this directory) — **the full app via a running dev/prod server**,
  exercising real navigation and, for backend specs, the live API.

Keep Playwright specs out of vitest: vitest's `include` is scoped to `src/**`, so specs live here
only. `test-results/` and `playwright-report/` are gitignored in `apps/wallow-auth/.gitignore`.

## Config

`apps/wallow-auth/playwright.config.ts` sets `testDir: "./e2e"` and
`testIdAttribute: "data-testid"`. Its `webServer` boots `pnpm dev` on port 3002 (reusing an
already-running server) and defaults `WALLOW_API_INTERNAL_URL` to `http://localhost:5001` so the
passthrough proxy resolves outside Aspire. Setting `E2E_BASE_URL` drives an already-running app
instead and boots no server. `e2e/global-setup.ts` drives one page load to hydration first so no
spec pays the dev server's lazy first-request cost.

## Selectors

- **ALWAYS** use `data-testid`: `page.getByTestId("login-email")`.
- **NEVER** use a raw `#id`, a CSS class (`.btn-primary`), or text (`button:has-text('Sign in')`).
- **Naming**: `{page}-{element}` kebab-case — `login-email`, `login-submit`.

## Readiness

Wait for React hydration via the marker `src/shared/components/ready-indicator.tsx` stamps:

```ts
await expect(page.locator("[data-app-ready='true']")).toBeAttached();
```

## Backend dependence

- `routes.spec.ts` is the route-reachability gate: every route renders (<400) and reaches
  hydration. Reachability specs must not depend on the backend — keep it that way.
- Every other spec (`login`, `signup`, `logout`, `mfa`, `otp-login`, `magic-link`,
  `forgot-password`, `reset-password`) requires the API plus the seeded admin from `api/seed.json`.
  A backend-dependent spec says so in its header comment and asserts app-level signals (e.g.
  `login-signed-in`), never incidental side effects like a URL change.
- Helpers: `mailpit.ts` reads delivered mail out of Mailpit; `totp.ts` generates TOTP codes.

Seeder gotcha: admin bootstrap is skipped when ANY user already exists, so a stale dev DB can lack
`admin@wallow.dev` even after a "successful" seed.

## Running

```bash
pnpm --filter ./apps/wallow-auth test:e2e                              # full suite (boots the dev server itself)
pnpm --filter ./apps/wallow-auth exec playwright test routes.spec.ts   # reachability only
./scripts/e2e.sh                                                       # containerised stack, all suites, then teardown
```

Drive the backend manually instead:

```bash
pnpm backend:infra                                   # docker compose up -d
dotnet run --project api/src/Wallow.Api              # port 5001
dotnet run --project api/src/Wallow.SeederService    # needs ConnectionStrings__DefaultConnection when run standalone
```
