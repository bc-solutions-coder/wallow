## E2E Test Rules

E2E suites are **per-app `@playwright/test` suites** living inside each app
(`apps/wallow-auth/e2e/` and `apps/wallow-web/e2e/`). The old .NET xUnit suite
(`Wallow.E2E.Tests`) and `scripts/run-e2e.sh` are deleted — do not recreate them.

### Two distinct real-browser test types — do not conflate them

Both drive a real Chromium, but they are separate suites with separate configs and commands:

- **Vitest browser mode** (`src/**/*.test.tsx`, run by `pnpm test`) — **isolated component render, no
  dev server.** Mounts one component in a headless Chromium iframe via the Vitest `playwright` provider.
  This is a unit/component test, not E2E. Its rules live in `TESTING.md`; it never boots the app or a
  backend.
- **Playwright `e2e/` suites** (this file, run by `pnpm --filter ./apps/<app> test:e2e`) — **the full app
  via a running dev/prod server**, exercising real navigation and (for backend specs) the live API.

### Layout (wallow-auth is the reference pattern)

- `apps/wallow-auth/playwright.config.ts` — `testDir: "./e2e"`, `testIdAttribute:
"data-testid"`. Its `webServer` boots `pnpm dev` (reusing an already-running server) and
  defaults `WALLOW_API_INTERNAL_URL` to `http://localhost:5001` so the passthrough proxy resolves
  outside Aspire.
- `apps/wallow-auth/e2e/routes.spec.ts` — route-reachability gate: every route renders (<400) and reaches
  hydration. Needs no backend.
- `apps/wallow-auth/e2e/login.spec.ts` — backend smoke: requires the API + seeded admin (`api/seed.json`).
- `apps/wallow-web/e2e/` — same pattern (`playwright.config.ts` on **port 3000**, `testDir: "./e2e"`,
  `WALLOW_API_INTERNAL_URL` default `http://localhost:5001`). `routes.spec.ts` is its backend-free
  reachability gate (only `/bff-demo` qualifies today — the other dashboard routes redirect to OIDC or
  need the API).
- `apps/wallow-web/e2e-cross-app/` — **cross-app journey suite** (`login-journey.spec.ts`), run via
  `playwright.cross-app.config.ts` (`pnpm --filter ./apps/wallow-web test:e2e:cross-app`). Unlike the
  per-app configs it boots **no server**: it drives three cooperating origins (wallow-web, the API OIDC
  issuer, wallow-auth) supplied by an EXTERNAL stack — either `docker/docker-compose.test.yml`
  (`E2E_BASE_URL=http://localhost:5053`) or `pnpm backend` (Aspire, default `http://localhost:3000`).

### Running

```bash
pnpm --filter ./apps/wallow-auth test:e2e            # full suite (boots the dev server itself)
pnpm --filter ./apps/wallow-web test:e2e             # wallow-web suite (boots on port 3000)
pnpm --filter ./apps/wallow-auth exec playwright test routes.spec.ts   # reachability only
```

**One-command backend-dependent runner:** `./scripts/e2e.sh` brings up the containerised stack
(`docker/docker-compose.test.yml`: infra + API + seeder + `wallow-web`), runs **all three**
Playwright suites against it — wallow-auth, wallow-web, and the wallow-web cross-app login
journey — and tears down. The two wallow-web suites always drive the containerised app on
`:5053`; only the wallow-auth suite's serving mode follows `E2E_BASE_URL`. Env knobs:
`E2E_SKIP_IMAGE_BUILD=1` (reuse prebuilt images), `E2E_UP_SERVICE`, `E2E_BASE_URL` (container
mode), `E2E_KEEP_STACK`. **Named `e2e.sh`, NOT `run-e2e.sh`** (which this file forbids). CI runs
the same script in the `e2e-tests` job of `.github/workflows/ci.yml` (installs Chromium, runs
`./scripts/e2e.sh`, uploads the `playwright-report-wallow-auth` and `playwright-report-wallow-web`
artifacts).

Or drive the backend manually (infra + API + seeder):

```bash
pnpm backend:infra                                   # docker compose up -d
dotnet run --project api/src/Wallow.Api              # port 5001
dotnet run --project api/src/Wallow.SeederService    # needs ConnectionStrings__DefaultConnection when run standalone
```

Seeder gotcha: admin bootstrap is skipped when ANY user already exists (Wallow-wd6n) — a
stale dev DB can lack `admin@wallow.dev` even after a "successful" seed.

### Selectors

- **ALWAYS** use `data-testid`: `page.getByTestId("login-email")`
- **NEVER** use raw `#id`, CSS class (`.btn-primary`), or text-based (`button:has-text('Sign in')`) selectors
- **Naming**: `{page}-{element}` kebab-case (e.g. `login-email`, `login-submit`)

### Readiness

Wait for React hydration via the marker `ready-indicator.tsx` stamps:
`await expect(page.locator("[data-app-ready='true']")).toBeAttached()`.
(The Blazor `data-blazor-ready` signal is gone.)

### Conventions

- Keep Playwright specs out of vitest: vitest `include` is scoped to `src/**`; specs live in
  `e2e/` (or wallow-web's `e2e-cross-app/`) only. Playwright artifacts (`test-results/`, `playwright-report/`) are gitignored per app;
  Vitest browser-mode artifacts (`__screenshots__/`, `.vitest-attachments/`) are gitignored at the
  repo root.
- Reachability specs must not depend on the backend; backend-dependent specs must say so in
  a header comment and assert app-level signals (e.g. `login-signed-in`), not incidental
  side effects like URL changes.
