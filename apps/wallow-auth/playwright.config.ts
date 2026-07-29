import { defineConfig, type PlaywrightTestConfig } from "@playwright/test";

const DEFAULT_PORT = 3002;
const port = Number(process.env.PORT ?? DEFAULT_PORT);

// When E2E_BASE_URL points at an already-running app — the wallow-auth container
// the compose stack serves on :5051 in CI (scripts/e2e.sh) — Playwright drives
// that URL directly and must NOT boot a local dev server. Left unset (the local
// runner's default) it falls back to a `pnpm dev` (`vite dev`) webServer on
// `port`, whose passthrough server routes target WALLOW_API_INTERNAL_URL.
//
// `port` here and `server.port` in vite.config.ts must agree: Playwright waits on
// this one and passes no PORT to the child, so the app's own default is what
// actually gets claimed.
const externalBaseURL = process.env.E2E_BASE_URL;

const webServer: PlaywrightTestConfig["webServer"] = externalBaseURL
  ? undefined
  : {
      command: "pnpm dev",
      port,
      reuseExistingServer: true,
      env: {
        // Outside Aspire the proxy's default target (http://wallow-api) does not
        // resolve; point it at the locally-run API unless the caller overrides.
        WALLOW_API_INTERNAL_URL: process.env.WALLOW_API_INTERNAL_URL ?? "http://localhost:5001",
      },
    };

export default defineConfig({
  testDir: "./e2e",
  // Runs after `webServer` is listening and before the first spec: drives one
  // page load to hydration so no test pays the dev server's lazy first-request
  // cost. See e2e/global-setup.ts for why that cost breaks a cold run.
  globalSetup: "./e2e/global-setup.ts",
  fullyParallel: true,
  reporter: "list",
  use: {
    baseURL: externalBaseURL ?? `http://localhost:${port}`,
    testIdAttribute: "data-testid",
  },
  ...(webServer ? { webServer } : {}),
});
