import { fileURLToPath } from "node:url";

import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-web — the shared node + real-Chromium two-project
 * split from `@bc-solutions-coder/testing`'s `createVitestProjects` preset,
 * configured with wallow-web's app-local knobs.
 *
 * Two runtimes, one per project (owned by the preset):
 *
 *   node    — every pure-logic spec (`src/lib/*.test.ts`, the BFF host wiring)
 *             PLUS the SSR route specs that render via `react-dom/server`
 *             (`renderToString`) or assert a route's `beforeLoad` redirect and
 *             never mount a live DOM, listed in `nodeTsxSpecs`.
 *   browser — every component spec (`*.test.tsx` that mounts a component) plus
 *             the browser-native smoke spec, in headless Chromium via the
 *             preset's `playwright()` provider with ZERO jsdom involvement.
 *
 * The `openid-client` alias and inlined workspace SDK this config used to carry
 * are gone with `src/lib/bff-server.test.ts`: the BFF wiring is the SDK's now,
 * and the host spec that replaced it (`src/lib/bff.test.ts`) mocks the SDK's
 * server entry directly, so nothing needs the transitive `openid-client` import
 * to resolve to one shared module id.
 */
const nodeTsxSpecs: string[] = ["src/routes/index.test.tsx", "src/routes/dashboard/route.test.tsx"];

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
  "zustand",
  "lucide-react",
  // The catalog components wallow-web mounts reach Base UI through per-component
  // SUBPATHS, and Vite pre-bundles a subpath only when it is named — the package
  // root does not cover them.
  "@base-ui/react/checkbox",
  "@base-ui/react/navigation-menu",
  "@base-ui/react/select",
  "@base-ui/react/toggle",
  "@base-ui/react/toggle-group",
];

// `src/router.tsx` imports `@tanstack/react-start`, which loads Start's
// per-request `AsyncLocalStorage` from `node:async_hooks` at module scope. A real
// client build never sees that module (the Start plugin compiles the isomorphic
// helper down to its client branch), but the browser project runs without that
// plugin and vitest externalises `node:async_hooks` to a throwing proxy, so every
// spec importing the router died at import. Point it at a real in-browser
// implementation instead; see the shim for why answering "no scope" is correct.
const nodeAsyncHooksShim: string = fileURLToPath(
  new URL("src/testing/node-async-hooks-browser-shim.ts", import.meta.url),
);

const { node, browser } = createVitestProjects({ nodeTsxSpecs, extraBrowserOptimizeDeps });

export default defineConfig({
  test: {
    projects: [
      node,
      { ...browser, resolve: { alias: { "node:async_hooks": nodeAsyncHooksShim } } },
    ],
  },
});
