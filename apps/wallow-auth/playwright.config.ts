import { defineConfig } from "@playwright/test";

const DEFAULT_PORT = 3002;
const port = Number(process.env.PORT ?? DEFAULT_PORT);

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  reporter: "list",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? `http://localhost:${port}`,
    testIdAttribute: "data-testid",
  },
  webServer: {
    command: "pnpm dev",
    port,
    reuseExistingServer: true,
    env: {
      // Outside Aspire the proxy's default target (http://wallow-api) does not
      // resolve; point it at the locally-run API unless the caller overrides.
      WALLOW_API_INTERNAL_URL: process.env.WALLOW_API_INTERNAL_URL ?? "http://localhost:5001",
    },
  },
});
