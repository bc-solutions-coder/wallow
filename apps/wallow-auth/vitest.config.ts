import { defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-auth (Wallow-vec7.1.5).
 *
 * Until this task the app's only suite (`src/lib/auth-server.test.ts`) ran on
 * Vitest's defaults; component tests need two things those defaults do not give:
 * a shared setup file (RTL cleanup — see `vitest.setup.ts`) and a `jsdom`
 * environment.
 *
 * `environment` stays `node` — the default for the proxy/branding suites, which
 * are plain functions — and the component suites opt into `jsdom` per file with
 * a `// @vitest-environment jsdom` pragma. This mirrors wallow-web and keeps the
 * DOM out of tests that have no business paying for it.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.{ts,tsx}"],
    setupFiles: ["./vitest.setup.ts"],
  },
});
