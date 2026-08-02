# wallow-web E2E — Playwright Agent Guide

Rules for `apps/wallow-web/e2e/` **and** `apps/wallow-web/e2e-cross-app/`. Two suites, two configs.

## This is Playwright, not Vitest browser mode — do not conflate them

Both drive a real Chromium, but they are separate suites with separate configs and commands:

- **Vitest browser mode** (`src/**/*.test.tsx`, run by `pnpm test`) — **isolated component render,
  no dev server.** Mounts one component in a headless Chromium iframe via the Vitest `playwright`
  provider. It is a unit/component test, not E2E, and boots neither the app nor a backend. Its
  rules live in `.claude/rules/TESTING.md`.
- **Playwright suites** (this directory and `e2e-cross-app/`) — **the full app via a running
  server**, exercising real navigation and, for backend specs, the live API.

Keep Playwright specs out of vitest: vitest's `include` is scoped to `src/**`, so specs live in
`e2e/` or `e2e-cross-app/` only. `test-results/` and `playwright-report/` are gitignored in
`apps/wallow-web/.gitignore`.

## Configs

- `apps/wallow-web/playwright.config.ts` — `testDir: "./e2e"`,
  `testIdAttribute: "data-testid"`. Its `webServer` boots `pnpm dev` on port 3000 (reusing an
  already-running server), defaults `WALLOW_API_INTERNAL_URL` to `http://localhost:5001` so the BFF
  proxy resolves outside Aspire, and supplies the OIDC/cookie env the BFF bridge throws without.
  Setting `E2E_BASE_URL` drives an already-running app instead and boots no server.
  `e2e/global-setup.ts` drives one page load to hydration first so no spec pays the dev server's
  lazy first-request cost.
- `apps/wallow-web/playwright.cross-app.config.ts` — `testDir: "./e2e-cross-app"`. Boots **no
  server** at all. See below.

## Selectors

- **ALWAYS** use `data-testid`: `page.getByTestId("login-email")`.
- **NEVER** use a raw `#id`, a CSS class (`.btn-primary`), or text (`button:has-text('Sign in')`).
- **Naming**: `{page}-{element}` kebab-case — `login-email`, `login-submit`.

## Readiness

Wait for React hydration via the marker `src/shared/components/ready-indicator.tsx` stamps:

```ts
await expect(page.locator("[data-app-ready='true']")).toBeAttached();
```

## `e2e/` — backend-free reachability gate

`routes.spec.ts` asserts every route renders (<400) and reaches hydration. Reachability specs must
not depend on the backend — keep it that way. Only `/bff-demo` qualifies today; the other dashboard
routes redirect to OIDC or need the API. A backend-dependent spec says so in its header comment and
asserts app-level signals (e.g. `login-signed-in`), never incidental side effects like a URL change.

## `e2e-cross-app/` — three-origin journey suite

`login-journey.spec.ts` exercises the complete wallow-web → wallow-auth → wallow-web login round
trip, plus an authenticated mutation and logout on the session it establishes. It needs three
cooperating origins — wallow-web (the BFF where the journey starts and ends), the API OIDC issuer,
and wallow-auth (the login UI the API's `AuthUrl` redirects to) — cross-wired by an **external**
stack, which is why its config boots nothing. It also needs the seeded admin from `api/seed.json`.

Two supported stacks:

```bash
# docker/docker-compose.test.yml — wallow-web on :5053
E2E_BASE_URL=http://localhost:5053 pnpm --filter ./apps/wallow-web test:e2e:cross-app
# pnpm backend (Aspire) — wallow-web on :3000, the config's default
pnpm --filter ./apps/wallow-web test:e2e:cross-app
```

A failure here can be a real cross-app regression rather than a fault in the spec.

## Running

```bash
pnpm --filter ./apps/wallow-web test:e2e             # reachability suite (boots on port 3000)
pnpm --filter ./apps/wallow-web test:e2e:cross-app   # journey suite (needs an external stack)
./scripts/e2e.sh                                     # containerised stack, all suites, then teardown
```

`./scripts/e2e.sh` brings up `docker/docker-compose.test.yml` (infra + API + seeder + wallow-web),
runs all three Playwright suites against it, and tears down. Both wallow-web suites always drive the
containerised app on `:5053`; only the wallow-auth suite's serving mode follows `E2E_BASE_URL`. Env
knobs: `E2E_SKIP_IMAGE_BUILD=1` (reuse prebuilt images), `E2E_UP_SERVICE`, `E2E_BASE_URL` (container
mode), `E2E_KEEP_STACK`. CI runs the same script in the `e2e-tests` job of
`.github/workflows/ci.yml`, uploading the `playwright-report-wallow-auth` and
`playwright-report-wallow-web` artifacts.

Drive the backend manually instead:

```bash
pnpm backend:infra                                   # docker compose up -d
dotnet run --project api/src/Wallow.Api              # port 5001
dotnet run --project api/src/Wallow.SeederService    # needs ConnectionStrings__DefaultConnection when run standalone
```

Seeder gotcha: admin bootstrap is skipped when ANY user already exists, so a stale dev DB can lack
`admin@wallow.dev` even after a "successful" seed.
