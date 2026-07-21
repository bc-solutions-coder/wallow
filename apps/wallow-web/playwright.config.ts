import { defineConfig, type PlaywrightTestConfig } from "@playwright/test";

const DEFAULT_PORT = 3000;
const port = Number(process.env.PORT ?? DEFAULT_PORT);

// When E2E_BASE_URL points at an already-running app — the wallow-web container
// a compose stack serves in CI — Playwright drives that URL directly and must
// NOT boot a local dev server. Left unset (the local default) it falls back to a
// `pnpm dev` webServer on `port`, whose BFF proxy targets WALLOW_API_INTERNAL_URL.
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
  fullyParallel: true,
  reporter: "list",
  use: {
    baseURL: externalBaseURL ?? `http://localhost:${port}`,
    testIdAttribute: "data-testid",
  },
  ...(webServer ? { webServer } : {}),
});
