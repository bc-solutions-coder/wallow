# E2E Testing Guide

This guide covers end-to-end testing for Wallow's frontend. E2E tests drive a Chromium browser
via Playwright (`@playwright/test`) against a running app. They live with the React apps in the
pnpm workspace, not in the .NET solution.

## Prerequisites

- Node 24 and pnpm (see the repo root `.nvmrc` / `packageManager`)
- Workspace dependencies installed: `pnpm install`
- Playwright browsers: `pnpm --filter ./apps/wallow-auth exec playwright install --with-deps chromium`

## Running E2E Tests

Each app owns its suite and runs it through its `test:e2e` script:

```bash
pnpm --filter ./apps/wallow-auth test:e2e
```

Playwright's `webServer` config starts the app with `pnpm dev` (and reuses an already-running
dev server), so you do not need to start the app yourself for the render gate. Tests that cross
into the backend also need the API running — start the stack with `pnpm backend` first.

To run a single spec or filter by title:

```bash
pnpm --filter ./apps/wallow-auth exec playwright test e2e/login.spec.ts
pnpm --filter ./apps/wallow-auth exec playwright test -g "password login"
```

## Configuration

`apps/wallow-auth/playwright.config.ts` sets the shared defaults:

- `testDir: "./e2e"`, `fullyParallel: true`, `reporter: "list"`
- `testIdAttribute: "data-testid"` — every selector resolves against `data-testid`
- `baseURL` defaults to `http://localhost:3002` (override with `PORT` or `E2E_BASE_URL`)
- `webServer` runs `pnpm dev` with `reuseExistingServer: true`
- `WALLOW_API_INTERNAL_URL` defaults to `http://localhost:5001` so the app's h3 proxy targets a
  locally-run API outside Aspire

## Suites

`apps/wallow-auth/e2e/` currently holds two kinds of spec:

### `routes.spec.ts` — route-reachability gate

Visits every route the app claims to serve, asserts the response status is below 400, and waits
for hydration. This is a render-only deletion gate: it proves each screen is reachable, not that
its flow is correct, so it needs only the app itself (no backend).

### `login.spec.ts` — password login smoke

Drives the real login flow across the h3 proxy into `Wallow.Api`. It **requires the backend**
(`pnpm backend`) and the seeded admin from `api/seed.json`. It fills `login-email` /
`login-password`, clicks `login-submit`, and waits for the `login-signed-in` signal. Credentials
default to the seeded admin and can be overridden with `E2E_USER` / `E2E_PASSWORD`.

## React Readiness

Both apps stamp `[data-app-ready='true']` on the document once React hydration completes (emitted
by `src/components/ready-indicator.tsx`). Wait for this marker before interacting with a page:

```ts
await expect(page.locator("[data-app-ready='true']")).toBeAttached();
```

## Selectors

- **Always** use `data-testid` — never CSS classes, raw IDs, or text.
- Naming convention: `{page}-{element}` in kebab-case (e.g. `login-email`, `mfa-challenge-code`).

## Writing a New E2E Test

1. Add a spec under the app's `e2e/` directory.
2. Add `data-testid` attributes to the components you need to target, using `{page}-{element}`
   kebab-case naming.
3. Navigate, wait for `[data-app-ready='true']`, then drive the flow with `getByTestId`.
4. Run: `pnpm --filter ./apps/<app> test:e2e`.

## Debugging Failed Tests

Playwright's built-in tooling covers most debugging:

```bash
# Headed mode
pnpm --filter ./apps/wallow-auth exec playwright test --headed

# Step through with the inspector
pnpm --filter ./apps/wallow-auth exec playwright test --debug

# Open the last HTML report (traces, screenshots, video when enabled)
pnpm --filter ./apps/wallow-auth exec playwright show-report
```

### Common Failure Patterns

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Timeout waiting for `data-app-ready` | App failed to hydrate | Check the dev server output and browser console |
| Timeout waiting for `data-testid` | Element not rendered or wrong testid | Verify the attribute in the component |
| `login.spec.ts` fails to sign in | Backend not running or admin not seeded | Start `pnpm backend`; confirm `api/seed.json` admin |
| Proxy/API errors | `WALLOW_API_INTERNAL_URL` points nowhere | Point it at your running API (default `http://localhost:5001`) |
