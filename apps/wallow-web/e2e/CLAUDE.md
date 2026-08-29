# wallow-web E2E — Playwright Agent Guide

Rules for `apps/wallow-web/e2e/` **and** `apps/wallow-web/e2e-cross-app/`. Two suites, two configs.

## This is Playwright, not Vitest browser mode — do not conflate them

Both drive a real Chromium; they are separate suites with separate configs and commands.
`apps/wallow-auth/e2e/CLAUDE.md` states the distinction once — read it there.

wallow-web specifics: vitest's `include` is scoped to `src/**`, so Playwright specs live in
`e2e/` or `e2e-cross-app/` only; `test-results/` and `playwright-report/` are gitignored.

## Configs

- `apps/wallow-web/playwright.config.ts` — `testDir: "./e2e"`, `testIdAttribute: "data-testid"`.
  Its `webServer` boots `pnpm dev` on port 3000 (reusing an already-running server), defaults
  `WALLOW_API_INTERNAL_URL` to `http://localhost:5001` so the BFF proxy resolves outside Aspire,
  and supplies the OIDC/cookie env the BFF bridge throws without. Setting `E2E_BASE_URL` drives
  an already-running app instead and boots no server. `e2e/global-setup.ts` drives one page load
  to hydration first so no spec pays the dev server's lazy first-request cost.
- `apps/wallow-web/playwright.cross-app.config.ts` — `testDir: "./e2e-cross-app"`. Boots **no
  server** at all. See below.

## Selectors

Same rules as every Playwright suite here — `apps/wallow-auth/e2e/CLAUDE.md` is the reference
statement. In short: **always** `data-testid`, **never** a raw `#id`, CSS class or text match,
named `{page}-{element}` kebab-case (`bff-user-status`, `dashboard-nav`,
`organization-create-submit`). A cross-app spec also drives wallow-auth's ids (`login-email`,
`consent-approve`) because the journey crosses that origin.

## Readiness

Wait for React hydration via the marker `src/shared/components/ready-indicator.tsx` stamps (the
app's thin wrapper over `ReadyIndicator` from `@bc-solutions-coder/ui`):

```ts
await expect(page.locator("[data-app-ready='true']")).toBeAttached();
```

## `e2e/` — backend-free reachability gate

`routes.spec.ts` asserts every route renders (<400) and reaches hydration. Reachability specs
must not depend on the backend — keep it that way. Only `/bff-demo` qualifies today; the other
dashboard routes redirect to OIDC or need the API. A backend-dependent spec says so in its header
comment and asserts app-level signals (e.g. `login-signed-in`), never incidental side effects
like a URL change.

## `e2e-cross-app/` — three-origin journey suite

Two specs with **different** stack requirements.

**`login-journey.spec.ts`** exercises the complete wallow-web → wallow-auth → wallow-web login
round trip, plus an authenticated mutation and logout. It needs three cooperating origins —
wallow-web (the BFF), the API OIDC issuer, and wallow-auth (the login UI) — cross-wired by an
**external** stack, which is why its config boots nothing. It also needs the seeded admin from
`api/seed.json`. Either stack serves it:

```bash
# docker/docker-compose.test.yml — wallow-web on :5053, the classic default;
# scripts/e2e.sh allocates a free per-run port and threads it through E2E_BASE_URL
E2E_BASE_URL=http://localhost:5053 pnpm --filter ./apps/wallow-web test:e2e:cross-app
# pnpm backend (Aspire) — wallow-web on :3000, the config's default
pnpm --filter ./apps/wallow-web test:e2e:cross-app
```

**`external-origin-login.spec.ts`** covers the "sign in with Wallow from another site" flow and
needs the **containerised stack specifically**. Its fourth origin is `bff-example`
(`docker/docker-compose.test.yml`), running wallow-web's image but authenticating as the seeded
third-party `bcordes-bff` client instead of `wallow-web-client`. Aspire has no `bff-example`
service, so `pnpm backend` cannot serve it. The origin's host port defaults to **`:3003`**
(`ports: ["${E2E_BFF_PORT:-3003}:3000"]`), independently of `E2E_BASE_URL`; `scripts/e2e.sh`
allocates a per-run port and passes it as `E2E_BFF_EXAMPLE_URL`.

The client identity is the point: `wallow-web-client` is first-party (id starts with `wallow-`)
so its authorize round trip never renders consent, while `bcordes-bff` routes through
wallow-auth's interactive consent screen — the leg `login-journey.spec.ts` structurally cannot
reach. A failure in either can be a real cross-app regression rather than a fault in the spec.

## Running

```bash
pnpm --filter ./apps/wallow-web test:e2e             # reachability suite (boots on port 3000)
pnpm --filter ./apps/wallow-web test:e2e:cross-app   # journey suite (needs an external stack)
./scripts/e2e.sh                                     # containerised stack, all suites, then teardown
```

`./scripts/e2e.sh` brings up `docker/docker-compose.test.yml` (infra + API + seeder +
wallow-web), runs all three Playwright suites, and tears down. Both wallow-web suites always
drive the containerised app on the run's allocated port; only the wallow-auth suite's serving
mode follows `E2E_BASE_URL`. Env knobs: `E2E_SKIP_IMAGE_BUILD=1` (reuse prebuilt images — gates
compose's `--build` and the `dotnet publish`, so an unset run always tests the current tree),
`E2E_UP_SERVICE`, `E2E_BASE_URL`, `E2E_KEEP_STACK`, `E2E_STACK_ID` and `E2E_*_PORT` (per-run
isolation — full list in `docker/.env.example`). CI runs the same script in the `e2e-tests` job
of `.github/workflows/ci.yml`, uploading the two `playwright-report-*` artifacts.

To drive the backend manually, use the three commands in `apps/wallow-auth/e2e/CLAUDE.md` — the
same here. That will not serve `external-origin-login.spec.ts`, which needs the compose stack's
`bff-example` origin.

Seeder gotcha: admin bootstrap is skipped when ANY user already exists, so a stale dev DB can
lack `admin@wallow.dev` even after a "successful" seed.
