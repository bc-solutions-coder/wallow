# wallow-web E2E — Playwright Agent Guide

Rules for `apps/wallow-web/e2e/` **and** `apps/wallow-web/e2e-cross-app/`. Two suites, two configs.

## This is Playwright, not Vitest browser mode — do not conflate them

Both drive a real Chromium; they are separate suites with separate configs and commands.
`apps/wallow-auth/e2e/CLAUDE.md` states the distinction once — read it there.

The wallow-web specifics: vitest's `include` is scoped to `src/**`, so Playwright specs live in
`e2e/` or `e2e-cross-app/` only, and `test-results/` and `playwright-report/` are gitignored in
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

Same rules as every Playwright suite here — `apps/wallow-auth/e2e/CLAUDE.md` is the reference
statement of them. In short: **always** `data-testid`, **never** a raw `#id`, a CSS class or a text
match, named `{page}-{element}` in kebab-case. wallow-web's own ids read
`bff-user-status`, `dashboard-nav`, `organization-create-submit`; a cross-app spec also drives
wallow-auth's (`login-email`, `consent-approve`), because the journey crosses that origin.

## Readiness

Wait for React hydration via the marker `src/shared/components/ready-indicator.tsx` stamps — the
app's thin wrapper over `ReadyIndicator` from `@bc-solutions-coder/ui`:

```ts
await expect(page.locator("[data-app-ready='true']")).toBeAttached();
```

## `e2e/` — backend-free reachability gate

`routes.spec.ts` asserts every route renders (<400) and reaches hydration. Reachability specs must
not depend on the backend — keep it that way. Only `/bff-demo` qualifies today; the other dashboard
routes redirect to OIDC or need the API. A backend-dependent spec says so in its header comment and
asserts app-level signals (e.g. `login-signed-in`), never incidental side effects like a URL change.

## `e2e-cross-app/` — three-origin journey suite

Two specs, and they do **not** have the same stack requirements.

**`login-journey.spec.ts`** exercises the complete wallow-web → wallow-auth → wallow-web login round
trip, plus an authenticated mutation and logout on the session it establishes. It needs three
cooperating origins — wallow-web (the BFF where the journey starts and ends), the API OIDC issuer,
and wallow-auth (the login UI the API's `AuthUrl` redirects to) — cross-wired by an **external**
stack, which is why its config boots nothing. It also needs the seeded admin from `api/seed.json`.
Either stack serves it:

```bash
# docker/docker-compose.test.yml — wallow-web on :5053
E2E_BASE_URL=http://localhost:5053 pnpm --filter ./apps/wallow-web test:e2e:cross-app
# pnpm backend (Aspire) — wallow-web on :3000, the config's default
pnpm --filter ./apps/wallow-web test:e2e:cross-app
```

**`external-origin-login.spec.ts`** covers the "sign in with Wallow from another site" flow, and it
needs the **containerised stack specifically**. Its fourth origin is `bff-example`
(`docker/docker-compose.test.yml`), which runs wallow-web's own image but authenticates as the
seeded third-party `bcordes-bff` client instead of `wallow-web-client`. Aspire has no `bff-example`
service, so `pnpm backend` cannot serve this spec. The origin is fixed at **`:3003`**
(`ports: ["3003:3000"]`) independently of `E2E_BASE_URL`; `E2E_BFF_EXAMPLE_URL` overrides it.

The client identity is the whole point of the second spec: `wallow-web-client` is first-party (its
id starts with `wallow-`), so its authorize round trip never renders consent, while `bcordes-bff` is
routed through wallow-auth's interactive consent screen. That leg — real seeded scope descriptions
on `/consent`, not placeholders — is what `login-journey.spec.ts` structurally cannot reach.

A failure in either can be a real cross-app regression rather than a fault in the spec.

## Running

```bash
pnpm --filter ./apps/wallow-web test:e2e             # reachability suite (boots on port 3000)
pnpm --filter ./apps/wallow-web test:e2e:cross-app   # journey suite (needs an external stack)
./scripts/e2e.sh                                     # containerised stack, all suites, then teardown
```

`./scripts/e2e.sh` brings up `docker/docker-compose.test.yml` (infra + API + seeder + wallow-web),
runs all three Playwright suites against it, and tears down. Both wallow-web suites always drive the
containerised app on `:5053`; only the wallow-auth suite's serving mode follows `E2E_BASE_URL`. Env
knobs: `E2E_SKIP_IMAGE_BUILD=1` (reuse prebuilt images instead of building any — it gates
compose's `--build` as well as the `dotnet publish`, so an unset run always tests the current tree), `E2E_UP_SERVICE`, `E2E_BASE_URL` (container
mode), `E2E_KEEP_STACK`. CI runs the same script in the `e2e-tests` job of
`.github/workflows/ci.yml`, uploading the `playwright-report-wallow-auth` and
`playwright-report-wallow-web` artifacts.

To drive the backend manually instead, use the three commands in
`apps/wallow-auth/e2e/CLAUDE.md` — they are the same here. That will not serve
`external-origin-login.spec.ts`, which needs the compose stack's `bff-example` origin.

Seeder gotcha: admin bootstrap is skipped when ANY user already exists, so a stale dev DB can lack
`admin@wallow.dev` even after a "successful" seed.
