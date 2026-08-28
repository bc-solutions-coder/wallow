# E2E Testing Guide

End-to-end tests drive a real Chromium browser via Playwright (`@playwright/test`) against a
running app. They live with the React apps in the pnpm workspace, not in the .NET solution.

> **Not the same thing as the component tests.** Vitest browser mode also drives a real
> Chromium, but it mounts a single component in an iframe with no dev server and no backend —
> that is a component test, covered in the [Testing Guide](testing.md). Playwright `e2e/` suites
> exercise the full app through a running dev or production server, and (for backend specs) the
> live API.

## Prerequisites

- Node 24 and pnpm (see the repo root `.nvmrc` and `packageManager`)
- Workspace dependencies installed: `pnpm install`
- Playwright browsers:
  `pnpm --filter ./apps/wallow-auth exec playwright install --with-deps chromium`

## Suite Layout

### `apps/wallow-auth/e2e/` — the reference pattern

Nine specs plus three helper modules:

| Spec                      | Backend dependency                                                         |
| ------------------------- | -------------------------------------------------------------------------- |
| `routes.spec.ts`          | None — the route-reachability gate                                         |
| `login.spec.ts`           | API + seeded admin                                                         |
| `signup.spec.ts`          | API                                                                        |
| `logout.spec.ts`          | API (validates the post-logout redirect URI against the client allow-list) |
| `forgot-password.spec.ts` | API                                                                        |
| `reset-password.spec.ts`  | API + Mailpit                                                              |
| `magic-link.spec.ts`      | API + Mailpit                                                              |
| `otp-login.spec.ts`       | API + Mailpit                                                              |
| `mfa.spec.ts`             | API + Mailpit                                                              |

Helpers: `mailpit.ts` (reads emails back over Mailpit's HTTP API), `totp.ts` (generates TOTP
codes for the MFA lifecycle), and `global-setup.ts` (wired in as Playwright's `globalSetup`, it
warms the app to hydration once so the first spec's readiness wait is not racing Vite's cold
pre-bundle).

`routes.spec.ts` is the render-only deletion gate: it visits every route the app claims to
serve, asserts the response status is below 400, and waits for hydration. It proves each screen
is reachable, not that its flow is correct, so it needs only the app itself.

Everything else is backend-dependent and says so in a header comment. Those specs assert
**app-level signals** (`login-signed-in`, `verify-email-heading`, `register-error`) rather than
incidental side effects like a URL change — a bare `/login` visit carries no OIDC `returnUrl`,
so a successful sign-in renders the authenticated state in place instead of navigating.

### `apps/wallow-web/e2e/`

The same pattern on port 3000. `routes.spec.ts` is its backend-free reachability gate; only
`/bff-demo` qualifies today, because it is the one public route with no `beforeLoad` gate. The
other dashboard routes redirect to OIDC or need the API.

### `apps/wallow-web/e2e-cross-app/` — the cross-app journey suite

Two specs live here, both under a dedicated config, `playwright.cross-app.config.ts`, which —
unlike the per-app configs — **boots no server of its own**.

- **`login-journey.spec.ts`** exercises the complete wallow-web → wallow-auth → wallow-web login
  round trip. It needs three cooperating origins that only a full stack cross-wires: wallow-web
  (where the journey starts and ends), the API OIDC issuer, and wallow-auth (the login UI the
  API's `AuthUrl` redirects to).
- **`external-origin-login.spec.ts`** runs the same round trip from the `bff-example` origin,
  whose host port defaults to `:3003`, which authenticates as the seeded third-party `bcordes-bff`
  client instead of `wallow-web-client`. Because that client is not first-party, the API routes it
  through wallow-auth's interactive **consent** screen — the leg `login-journey.spec.ts` never
  reaches. `bff-example` exists only in `docker/docker-compose.test.yml`, so this spec needs the
  containerised stack specifically; Aspire has no equivalent service.

Supply that stack one of two ways:

```bash
# Against the containerised test stack (wallow-web on :5053, the classic default) — runs both
# specs. ./scripts/e2e.sh instead allocates a free per-run port for each and threads it through
# E2E_BASE_URL / E2E_BFF_EXAMPLE_URL (Wallow-joo0).
E2E_BASE_URL=http://localhost:5053 pnpm --filter ./apps/wallow-web test:e2e:cross-app

# Against the Aspire AppHost (wallow-web on :3000, the config's default) — login-journey only
pnpm backend
pnpm --filter ./apps/wallow-web test:e2e:cross-app
```

Both also need the seeded admin from `api/seed.json`.

## Running

```bash
pnpm --filter ./apps/wallow-auth test:e2e     # full suite (boots its own dev server)
pnpm --filter ./apps/wallow-web test:e2e      # wallow-web suite (port 3000)

# A single spec, or filter by title
pnpm --filter ./apps/wallow-auth exec playwright test routes.spec.ts
pnpm --filter ./apps/wallow-auth exec playwright test -g "password login"
```

Playwright's `webServer` starts the app with `pnpm dev` and reuses an already-running dev
server, so you do not need to start the app yourself for the reachability gate. Backend-
dependent specs additionally need the API — either `pnpm backend` or the runner below.

### One-command backend-dependent runner

`./scripts/e2e.sh` is the supported way to run the backend-dependent suites. It brings up
`docker/docker-compose.test.yml` (infra, migrations, seeder, `Wallow.Api`, and `wallow-web`),
waits for OIDC discovery to answer at that run's API URL (classic default
`http://localhost:5050/.well-known/openid-configuration`), runs all three Playwright suites, and
tears the stack down:

1. `pnpm --filter ./apps/wallow-auth test:e2e`
2. `pnpm --filter ./apps/wallow-web test:e2e` — the reachability gate
3. `pnpm --filter ./apps/wallow-web test:e2e:cross-app` — both cross-app specs: the first-party
   login journey (full login + authenticated mutation + logout loop) and the external-origin
   journey through the consent screen

```bash
./scripts/e2e.sh                            # local run: (re)build images, up, test, down
E2E_SKIP_IMAGE_BUILD=1 ./scripts/e2e.sh     # reuse already-built :test images
```

It always starts by running `docker compose down -v` so the volumes are fresh. That matters:
the seeder skips admin bootstrap when _any_ user already exists, so a reused database would
silently lack the `admin@wallow.dev` account the login spec signs in as.

| Env knob                 | Effect                                                                                                                                 |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------- |
| `E2E_STACK_ID=<id>`      | Per-run stack identity (default: this shell's PID). The compose project is `wallow-test-<id>`; concurrent runs isolate on this plus their per-run host ports (Wallow-joo0). |
| `E2E_*_PORT=<n>`         | Pin any host port (API/AUTH/WEB/BFF/POSTGRES/VALKEY/MAILPIT_SMTP/MAILPIT_HTTP/GARAGE_S3/GARAGE_ADMIN — full list in `docker/.env.example`). Unset ports each get a free port from the kernel for that run. |
| `E2E_IMAGE_TAG=<tag>`    | Pin the image tag. Default: `test` when `E2E_SKIP_IMAGE_BUILD=1` (reuse), else `test-<stack id>` (built per-run, untagged at teardown). |
| `E2E_SKIP_IMAGE_BUILD=1` | Reuse whatever `:test` images already exist rather than building any of them — it suppresses both the `dotnet publish` of the API, migration, and seeder images **and** compose's `--build` of the services that have a build block (`wallow-web`, `wallow-auth`, `bff-example`, `garage`). CI sets it because a prior job preloads all but `bff-example` from cache. Leaving it unset is what guarantees the run tests your current tree. |
| `E2E_UP_SERVICE=<svc>`   | Extra compose service to `up --wait` (default `wallow-api`). CI sets `wallow-auth` so that app is served from a container, which also points the wallow-auth suite at that container's per-run port unless `E2E_BASE_URL` is set. `wallow-web` is always brought up as well. |
| `E2E_BASE_URL=<url>`     | Drive an already-running wallow-auth at that URL; Playwright then boots no local dev server. Does not affect the wallow-web suites.                                   |
| `E2E_KEEP_STACK=1`       | Leave the stack up after the run, for debugging.                                                                                                                     |

The two serving modes follow from `E2E_BASE_URL`, and they apply to the **wallow-auth** suite
only. Left unset (the local default), Playwright's own `pnpm dev` webServer serves that app on
this run's allocated port (classic default `:3002`, passed through `PORT`) and its passthrough
server routes target the containerised API via `WALLOW_API_INTERNAL_URL`. Set (as in CI), the
prebuilt `wallow-auth-react:test` container serves it on this run's auth port (classic default
`:5051`) and Playwright drives it directly.

The two wallow-web suites always run in container mode against the `wallow-web-react:test`
container on this run's web port (classic default `:5053`). The cross-app journey needs three
cooperating origins that only the compose stack cross-wires — wallow-web, the API's OIDC issuer,
and the wallow-auth login UI — and `playwright.cross-app.config.ts` boots no server of its own.

### Driving the backend manually

```bash
pnpm backend:infra                                   # docker compose up -d (infra only)
dotnet run --project api/src/Wallow.Api              # port 5001
dotnet run --project api/src/Wallow.SeederService    # needs ConnectionStrings__DefaultConnection standalone
```

## Configuration

`apps/wallow-auth/playwright.config.ts` sets the shared defaults; `apps/wallow-web/playwright.config.ts`
is identical apart from the port.

- `testDir: "./e2e"`, `fullyParallel: true`, `reporter: "list"`
- `testIdAttribute: "data-testid"` — every selector resolves against `data-testid`
- `baseURL` defaults to `http://localhost:3002` for wallow-auth and `http://localhost:3000` for
  wallow-web; override with `PORT`, or with `E2E_BASE_URL` to target an external app
- `webServer` runs `pnpm dev` with `reuseExistingServer: true` — and is omitted entirely when
  `E2E_BASE_URL` is set
- `WALLOW_API_INTERNAL_URL` defaults to `http://localhost:5001`, so the app's proxy resolves to
  a locally-run API outside Aspire

## Selectors

- **Always** use `data-testid`: `page.getByTestId("login-email")`.
- **Never** use raw `#id`, a CSS class (`.btn-primary`), or a text-based selector
  (`button:has-text('Sign in')`).
- **Naming:** `{page}-{element}` in kebab-case — `login-email`, `login-submit`,
  `mfa-challenge-code`.

## React Readiness

Both apps stamp `data-app-ready="true"` on the document once React hydration completes, emitted
by `src/shared/components/ready-indicator.tsx`. Wait for that marker before interacting with a page:

```ts
await expect(page.locator("[data-app-ready='true']")).toBeAttached();
```

## Writing a New E2E Test

1. Add a spec under the app's `e2e/` directory. Playwright specs must stay out of `src/**`,
   which is where Vitest looks.
2. Add `data-testid` attributes to the components you need to target, using `{page}-{element}`
   kebab-case naming.
3. Navigate, wait for `[data-app-ready='true']`, then drive the flow with `getByTestId`.
4. If the spec needs the backend, say so in a header comment and assert an app-level signal, not
   a URL change.
5. Run it: `pnpm --filter ./apps/<app> test:e2e`.

Playwright artifacts (`test-results/`, `playwright-report/`) are gitignored per app.

## Debugging Failed Tests

```bash
# Headed mode
pnpm --filter ./apps/wallow-auth exec playwright test --headed

# Step through with the inspector
pnpm --filter ./apps/wallow-auth exec playwright test --debug

# Open the last HTML report (traces, screenshots, video when enabled)
pnpm --filter ./apps/wallow-auth exec playwright show-report
```

### Common Failure Patterns

| Symptom                                            | Likely cause                                       | Fix                                                                                         |
| -------------------------------------------------- | -------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Timeout waiting for `data-app-ready`               | The app failed to hydrate                          | Check the dev server output and the browser console                                         |
| Timeout waiting for a `data-testid`                | Element not rendered, or the testid is wrong       | Verify the attribute in the component                                                       |
| `login.spec.ts` cannot sign in                     | Backend not running, or the admin was never seeded | Run `./scripts/e2e.sh`, which recreates volumes so the seeder bootstraps `admin@wallow.dev` |
| Mail-dependent specs get `ECONNREFUSED` on `:8035` | Mailpit is not up                                  | Use `./scripts/e2e.sh`; the compose file gates `wallow-api` on Mailpit starting             |
| Proxy or API errors                                | `WALLOW_API_INTERNAL_URL` points nowhere           | Point it at your running API (default `http://localhost:5001`)                              |
