# wallow-web E2E — Playwright Agent Guide

Rules for `apps/wallow-web/e2e/` **and** `apps/wallow-web/e2e-cross-app/`. Two suites, two
configs. `apps/wallow-auth/e2e/CLAUDE.md` is the repo's Playwright reference (selectors,
readiness marker/wrapper, Playwright-vs-vitest) — read it first. Playwright specs live in
`e2e/` or `e2e-cross-app/` only; vitest's `include` is scoped to `src/**`.

`playwright.config.ts` (`testDir: "./e2e"`) supplies the OIDC/cookie env the BFF bridge throws
without — see its comments. `playwright.cross-app.config.ts` (`testDir: "./e2e-cross-app"`)
boots **no server** at all. A cross-app spec also drives wallow-auth's testids (`login-email`,
`consent-approve`) — the journey crosses that origin.

## `e2e/` — backend-free reachability gate

`routes.spec.ts` asserts every route renders (<400) and reaches hydration; keep it
backend-free. Only `/bff-demo` qualifies today — the other dashboard routes redirect to OIDC or
need the API.

## `e2e-cross-app/` — three-origin journey suite

**`login-journey.spec.ts`** — the complete wallow-web → wallow-auth → wallow-web login round
trip plus an authenticated mutation and logout. Needs three cooperating origins — wallow-web
(the BFF), the API OIDC issuer, wallow-auth (the login UI) — cross-wired by an **external**
stack (why its config boots nothing), plus the seeded admin. Either stack serves it:

```bash
# docker/docker-compose.test.yml — wallow-web on :5053, the classic default;
# scripts/e2e.sh allocates a free per-run port and threads it through E2E_BASE_URL
E2E_BASE_URL=http://localhost:5053 pnpm --filter ./apps/wallow-web test:e2e:cross-app
# pnpm backend (Aspire) — wallow-web on :3000, the config's default
pnpm --filter ./apps/wallow-web test:e2e:cross-app
```

**`failure-surfaces.spec.ts`** — the failure model's five surfaces on a real session: network
down (read banner with retry, mutation toast), a 429's wait-seconds copy, a validation 400's
field errors, a 401's "Sign in" action returning to the current path, and a loader 404's
not-found page. Every scenario but the 404 injects its failure with `page.route` at the
browser's own `/api/...` call, answering with the problem body the API or BFF actually writes
(a body without a `code` is an unrecognised response, not the failure under test). A banner's
actions derive their testids from the banner's own (`<banner>-retry`, `<banner>-sign-in`), the
same `{page}-{element}` rule as every other composite. The sign-in round trip every journey starts
from is `sign-in.ts`'s `signInAndLandOn`.

**`external-origin-login.spec.ts`** — the external relying-party acceptance journey, a
`test.describe.serial` suite (anonymous service-account contact, branded-consent sign-in +
typed API call + back-channel logout proof, then org-surface suspension — which poisons the
earlier stages, so ordering is load-bearing); needs the **containerised stack specifically**. Its fourth origin is `bff-example`
(`docker/docker-compose.test.yml`), running `apps/minimal-app`'s own image — the external
relying-party example — authenticating as the seeded third-party `bff-example-client` and
submitting its anonymous `POST /contact` as the seeded `sa-wallow-nightly-sync` service
account. Aspire has no `bff-example` service, so `pnpm backend` cannot serve it. The origin's host port defaults to `:3003` (`E2E_BFF_PORT`), independent of
`E2E_BASE_URL`; `scripts/e2e.sh` passes a per-run port as `E2E_BFF_EXAMPLE_URL`.

The client identity is the point: `wallow-web-client` is first-party (id starts with `wallow-`)
so its authorize round trip never renders consent, while `bff-example-client` routes through
wallow-auth's interactive consent screen — the leg `login-journey.spec.ts` structurally cannot
reach. A failure in either can be a real cross-app regression rather than a fault in the spec.

**`service-account.spec.ts`** — no browser at all: the seeded `sa-wallow-nightly-sync`
service account (`api/seed.json`) mints a client-credentials token at the API's
`/connect/token` and lists its organization with it. It reads the API origin from
`E2E_API_URL` (`scripts/e2e.sh` passes this run's port; classic default `:5050`). The Identity
integration harness stubs bearer authentication, so this is where a registered service
account's `org_id` binding is proven against real JWT validation.

Seeder-bootstrap semantics (when a re-seed does or does not create the admin):
`apps/wallow-auth/e2e/CLAUDE.md`.

## Running

```bash
pnpm --filter ./apps/wallow-web test:e2e             # reachability suite
pnpm --filter ./apps/wallow-web test:e2e:cross-app   # journey suite (needs an external stack)
./scripts/e2e.sh                                     # containerised stack, all suites, then teardown
```

Only the wallow-auth suite's serving mode follows `E2E_BASE_URL` under `e2e.sh`; both
wallow-web suites always drive the containerised app. A manual backend (`api/CLAUDE.md`) will
not serve `external-origin-login.spec.ts` — it needs the compose stack's `bff-example` origin.
