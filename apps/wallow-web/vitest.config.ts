import { createRequire } from "node:module";

import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-web — the shared node + real-Chromium two-project
 * split from `@bc-solutions-coder/testing`'s `createVitestProjects` preset
 * (Wallow-0q2s.1.4), configured with wallow-web's app-local knobs.
 *
 * Two runtimes, one per project (owned by the preset):
 *
 *   node    — every pure-logic spec (`src/lib/*.test.ts`, the BFF spike) PLUS
 *             the SSR route specs that render via `react-dom/server`
 *             (`renderToString`) and never mount a live DOM (`__root.*`,
 *             `router`, `index`, `dashboard/route`), listed in `nodeTsxSpecs`.
 *             This keeps the `openid-client` alias and the inlined workspace SDK
 *             the BFF spec relies on (see below), and pays no browser overhead
 *             for pure-logic suites.
 *   browser — every component spec (`*.test.tsx` that mounts a component) plus
 *             the browser-native smoke spec, in headless Chromium via the
 *             preset's `playwright()` provider with ZERO jsdom involvement.
 *
 * The BFF spike tests (src/lib/bff-server.test.ts) hermetically mock
 * `openid-client` (`vi.mock`), then drive the SDK's BFF handlers through
 * bff-server.ts. Two app-local `nodeProjectOverrides` knobs make that mock reach
 * the SDK's transitive `openid-client` import (NODE project only — browser specs
 * never touch it):
 *   1. Inline the workspace SDK (realpath `packages/sdk`) so Vitest transforms
 *      it instead of loading the built dist via native ESM — a native import
 *      would bypass the module mock entirely.
 *   2. Alias `openid-client` to its single resolved entry so the specifier the
 *      test mocks and the specifier the SDK imports key to the SAME module id
 *      (pnpm otherwise resolves it to different ids across packages, so the
 *      factory mock would miss). Resolved from the SDK, which owns the dep, so
 *      the path tracks version bumps automatically.
 */
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

// Browser render/runtime libraries wallow-web pulls in beyond the shared preset
// baseline, pre-bundled so the browser provider does not discover them mid-run
// and trigger a Vite reload ("Vitest unexpectedly reloaded a test"), which flakes.
const extraBrowserOptimizeDeps: string[] = [
  "react",
  "react/jsx-dev-runtime",
  "react/jsx-runtime",
  "react-dom",
  "@tanstack/react-query",
  "@tanstack/react-router",
  "@tanstack/react-form",
];

const { node, browser } = createVitestProjects({
  nodeTsxSpecs,
  extraBrowserOptimizeDeps,
  nodeProjectOverrides: {
    resolve: { alias: { "openid-client": openidClientEntry } },
    test: { server: { deps: { inline: [/packages[/\\]sdk/u] } } },
  },
});

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
