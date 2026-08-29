# wallow-auth E2E — Playwright Agent Guide

Rules for `apps/wallow-auth/e2e/`. This is the reference pattern for a per-app Playwright
suite; wallow-web's guide points here. Playwright specs live in `e2e/` only — vitest's
`include` is scoped to `src/**`.

## Config

Serving mode: `E2E_BASE_URL` set = drive an already-running app, no server booted; unset = the
config's `webServer` boots `pnpm dev` — see `playwright.config.ts`'s comments.

Two Playwright projects order the run: `first-run` holds only `first-run-setup.spec.ts`; `main`
(everything else) declares `dependencies: ["first-run"]`. Against the admin-less stack
`scripts/e2e.sh` boots, that journey creates the `admin@wallow.dev` every other spec signs in
as; against an already-provisioned backend it skips itself. Keep new specs OUT of `first-run`.

## Selectors

These rules hold for every Playwright suite in the repo; this is where they are stated.

- **ALWAYS** use `data-testid`: `page.getByTestId("login-email")`.
- **NEVER** use a raw `#id`, a CSS class (`.btn-primary`), or text (`button:has-text('Sign in')`).
- **Naming**: `{page}-{element}` kebab-case — `login-email`, `login-submit`.

## Readiness

Wait for React hydration via the marker the app's `ready-indicator` wrapper (over
`ReadyIndicator` from `@bc-solutions-coder/ui`) stamps:

```ts
await expect(page.locator("[data-app-ready='true']")).toBeAttached();
```

## Backend dependence

- `routes.spec.ts` is the backend-free route-reachability gate — keep it backend-free.
- A backend-dependent spec says so in its header comment and asserts app-level signals (e.g.
  `login-signed-in`), never incidental side effects like a URL change.
- Seeder gotcha: admin bootstrap is skipped when an administrator already exists (setup gate
  closed — an active membership holding an AdminAccess role), or when the configured admin
  email already exists as a user. A half-bootstrapped account (user exists, no admin
  membership) is left for a human, and a re-seed never fights the setup page's outcome.

## Running

Commands: `test:e2e` (package.json), `./scripts/e2e.sh` (`.claude/rules/E2E.md`); manual
backend: `api/CLAUDE.md`.
