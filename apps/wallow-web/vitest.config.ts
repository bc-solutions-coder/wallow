import { createRequire } from "node:module";

import { playwright } from "@vitest/browser-playwright";
import { configDefaults, defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-web — multi-project split (Wallow-xzha.2.2).
 *
 * Two runtimes, one per project:
 *
 *   node    — every pure-logic spec (`src/lib/*.test.ts`, the BFF spike) PLUS
 *             the SSR route specs that render via `react-dom/server`
 *             (`renderToString`) and never mount a live DOM (`__root.*`,
 *             `router`, `index`, `dashboard/route`). This keeps the
 *             `openid-client` alias and the inlined workspace SDK the BFF spec
 *             relies on (see below), and pays no browser overhead for
 *             pure-logic suites.
 *   browser — every component spec (`*.test.tsx` that mounts a component) plus
 *             the browser-native smoke spec. They run in headless Chromium via
 *             Playwright with ZERO jsdom involvement, using `vitest-browser-react`
 *             (Wallow-xzha.3.2). The jsdom pragma and `@testing-library/*` are
 *             gone from every one of these files.
 *
 * NOTE (Vitest 4 provider API): v4 replaced the v3 string form
 * `provider: "playwright"` with the factory `provider: playwright()` from
 * `@vitest/browser-playwright` — passing the bare string now throws
 * ("configuration was changed to accept a factory instead of a string").
 */

// The BFF spike tests (src/lib/bff-server.test.ts) hermetically mock
// `openid-client` (`vi.mock`), then drive the SDK's BFF handlers through
// bff-server.ts. Two harness knobs make that mock reach the SDK's transitive
// `openid-client` import (NODE project only — browser specs never touch it):
//   1. Inline the workspace SDK (realpath `packages/sdk`) so Vitest transforms
//      it instead of loading the built dist via native ESM — a native import
//      would bypass the module mock entirely.
//   2. Alias `openid-client` to its single resolved entry so the specifier the
//      test mocks and the specifier the SDK imports key to the SAME module id
//      (pnpm otherwise resolves it to different ids across packages, so the
//      factory mock would miss). Resolved from the SDK, which owns the dep, so
//      the path tracks version bumps automatically.
const sdkRequire = createRequire(new URL("../../packages/sdk/package.json", import.meta.url));
const openidClientEntry: string = sdkRequire.resolve("openid-client");

// SSR route specs render through `react-dom/server` (`renderToString`) and never
// mount a live DOM, so they stay on the node project — routing them into Chromium
// buys nothing and costs real per-test browser overhead. Every OTHER `*.test.tsx`
// mounts a component via `vitest-browser-react` and belongs in the browser project.
const nodeTsxSpecs: string[] = [
  "src/router.test.tsx",
  "src/routes/index.test.tsx",
  "src/routes/__root.test.tsx",
  "src/routes/__root.branding.test.tsx",
  "src/routes/__root.hydration.test.tsx",
  "src/routes/__root.provider.test.tsx",
  "src/routes/dashboard/route.test.tsx",
];

// The `.test.tsx` literal here is what the multi-project config guard keys on to
// confirm a tsx-routed browser project exists.
const browserSpecs = "src/**/*.test.tsx";

export default defineConfig({
  test: {
    projects: [
      {
        resolve: {
          alias: {
            "openid-client": openidClientEntry,
          },
        },
        test: {
          name: "node",
          environment: "node",
          include: ["src/**/*.test.ts", ...nodeTsxSpecs],
          exclude: [...configDefaults.exclude],
          server: { deps: { inline: [/packages[/\\]sdk/u] } },
        },
      },
      {
        // Pre-bundle the libraries every component spec pulls in so the browser
        // provider doesn't discover them mid-run and trigger a Vite reload
        // ("Vite unexpectedly reloaded a test"), which flakes the suite.
        optimizeDeps: {
          include: [
            "vitest-browser-react",
            "react",
            "react/jsx-dev-runtime",
            "react/jsx-runtime",
            "react-dom",
            "react-dom/client",
            "@tanstack/react-query",
            "@tanstack/react-router",
            "@tanstack/react-form",
          ],
        },
        test: {
          name: "browser",
          include: [browserSpecs],
          exclude: [...configDefaults.exclude, ...nodeTsxSpecs],
          browser: {
            enabled: true,
            provider: playwright(),
            headless: true,
            instances: [{ browser: "chromium" }],
          },
        },
      },
    ],
  },
});
