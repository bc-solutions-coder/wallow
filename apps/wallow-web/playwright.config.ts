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
        // src/app/lib/bff.server.ts builds the SDK's BFF server on the first /bff|/api|
        // /health request, and that build reads the OIDC config from env and
        // throws on ANY missing key. Without these the bridge 500s and every
        // route whose `beforeLoad` resolves `getUser()` (notably `/`) fails the
        // gate. Aspire injects them (api/src/Wallow.AppHost/Program.cs); mirror
        // the same dev values here so a bare `pnpm dev` is self-sufficient. An
        // anonymous request never reaches the issuer — it 401s off the absent
        // session cookie — so this stays backend-free.
        OIDC_ISSUER: process.env.OIDC_ISSUER ?? "http://localhost:5001",
        OIDC_CLIENT_ID: process.env.OIDC_CLIENT_ID ?? "wallow-web-client",
        OIDC_CLIENT_SECRET: process.env.OIDC_CLIENT_SECRET ?? "wallow-web-secret",
        OIDC_REDIRECT_URI: process.env.OIDC_REDIRECT_URI ?? `http://localhost:${port}/bff/callback`,
        OIDC_POST_LOGOUT_REDIRECT_URI:
          process.env.OIDC_POST_LOGOUT_REDIRECT_URI ?? `http://localhost:${port}`,
        BFF_API_BASE_URL: process.env.BFF_API_BASE_URL ?? "http://localhost:5001",
        COOKIE_PASSWORD:
          process.env.COOKIE_PASSWORD ?? "wallow-web-dev-cookie-seal-password-min-32-chars",
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
