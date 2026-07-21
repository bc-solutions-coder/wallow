import { defineConfig } from "@playwright/test";

/**
 * Cross-app journey config (Wallow-xzha.4.3).
 *
 * Unlike apps/wallow-web/playwright.config.ts — whose `webServer` boots a single
 * `pnpm dev` wallow-web instance for the backend-free reachability gate — the
 * cross-app login journey needs THREE cooperating origins that only a full stack
 * wires together: wallow-web (the BFF where the journey starts and ends), the API
 * OIDC issuer, and wallow-auth (the login UI the API's `AuthUrl` redirects to).
 * That wiring is supplied by an EXTERNAL stack, so this config boots no server of
 * its own and simply drives whatever `E2E_BASE_URL` (or the local default) serves.
 *
 * Two supported stacks (both cross-wire the three origins correctly):
 *   - docker/docker-compose.test.yml — wallow-web on :5053, run with
 *     `E2E_BASE_URL=http://localhost:5053 pnpm --filter ./apps/wallow-web test:e2e:cross-app`.
 *   - `pnpm backend` (Aspire AppHost, wiring fixed in Wallow-xzha.1.1) — wallow-web
 *     on :3000, the local default below; no `E2E_BASE_URL` needed.
 */
const externalBaseURL = process.env.E2E_BASE_URL;
const DEFAULT_WEB_URL = "http://localhost:3000";

export default defineConfig({
  testDir: "./e2e-cross-app",
  fullyParallel: true,
  reporter: "list",
  use: {
    baseURL: externalBaseURL ?? DEFAULT_WEB_URL,
    testIdAttribute: "data-testid",
  },
});
