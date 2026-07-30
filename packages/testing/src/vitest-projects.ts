/**
 * Shared Vitest two-project (node + real-Chromium browser) preset, extracted
 * from the identical shape hand-rolled in apps/wallow-auth/vitest.config.ts and
 * apps/wallow-web/vitest.config.ts.
 *
 * `createVitestProjects(options)` returns the `{ node, browser }` project pair
 * consumed by `defineConfig({ test: { projects: [node, browser] } })`:
 *
 *   node    — pure-logic specs: `src/**\/*.test.ts` plus a caller-supplied list
 *             of pure-logic/SSR `*.test.tsx` specs (`nodeTsxSpecs`). No DOM;
 *             a 60s testTimeout (see the literal) covering the cold route-graph import.
 *   browser — every component spec (`src/**\/*.test.tsx`) MINUS `nodeTsxSpecs`,
 *             run in headless Chromium via the Vitest 4 `playwright()` factory
 *             provider (NOT the v3 `"playwright"` string, which throws).
 *
 * App-local knobs that must NOT live in the shared package (wallow-web's
 * `resolve.alias['openid-client']` + `test.server.deps.inline`) are passed
 * through `nodeProjectOverrides` and merged into the node project with Vite's
 * own `mergeConfig` (re-exported by `vitest/config`).
 */

import { playwright } from "@vitest/browser-playwright";
import { configDefaults, mergeConfig } from "vitest/config";

import { mergeOptimizeDeps } from "./browser-optimize-deps";

export interface VitestProjectsOptions {
  /** Pure-logic / SSR `*.test.tsx` specs that belong on node, not in Chromium. */
  nodeTsxSpecs?: string[];
  /** App-specific `optimizeDeps.include` entries added onto the shared baseline. */
  extraBrowserOptimizeDeps?: string[];
  /** App-local node-project overrides (e.g. resolve.alias, server.deps.inline). */
  nodeProjectOverrides?: Record<string, unknown>;
}

export interface VitestNodeTestConfig {
  name: string;
  environment: string;
  include: string[];
  exclude: string[];
  /** See the node project literal for why this is 60s and not vitest's 5s default. */
  testTimeout: number;
  [key: string]: unknown;
}

export interface VitestNodeProject {
  test: VitestNodeTestConfig;
  [key: string]: unknown;
}

export interface VitestBrowserInstance {
  /** Vitest browser engine; a literal union so the pair satisfies `defineConfig`. */
  browser: "chromium" | "firefox" | "webkit";
}

export interface VitestBrowserConfig {
  enabled: boolean;
  /**
   * Vitest 4 factory provider (`playwright()`), NOT the v3 `"playwright"` string.
   * Typed as the factory's return so the emitted pair satisfies vitest's
   * `defineConfig({ test: { projects } })` without a cast in each app config.
   */
  provider: ReturnType<typeof playwright>;
  headless: boolean;
  instances: VitestBrowserInstance[];
}

export interface VitestBrowserTestConfig {
  name: string;
  include: string[];
  exclude: string[];
  browser: VitestBrowserConfig;
}

export interface VitestBrowserProject {
  optimizeDeps: { include: string[] };
  test: VitestBrowserTestConfig;
}

export interface VitestProjectsPair {
  node: VitestNodeProject;
  browser: VitestBrowserProject;
}

/**
 * Build the shared `{ node, browser }` Vitest project pair. See the module
 * header for the node/browser split; app-local knobs arrive via `options`.
 */
export function createVitestProjects(options: VitestProjectsOptions = {}): VitestProjectsPair {
  const { nodeTsxSpecs = [], extraBrowserOptimizeDeps = [], nodeProjectOverrides = {} } = options;

  const node: VitestNodeProject = {
    test: {
      name: "node",
      environment: "node",
      include: ["src/**/*.test.ts", ...nodeTsxSpecs],
      exclude: [...configDefaults.exclude],
      // A route-root spec's FIRST `await import("./__root")` pays a cold Vite transform of the
      // whole route graph — measured at 19s for wallow-auth's __root.provider.test.tsx, against
      // 1ms for the second test in the same file once the module is cached. Vitest's 5s default
      // fails that import whenever this project competes with the browser project for CPU. The
      // cost is structural (TanStack Router/Start + react-query, NOT the @bc-solutions-coder/ui
      // barrel — swapping to ui subpaths moved it 19043ms -> 18805ms), so the budget belongs in
      // the shared preset. 60s clears the measurement 3x over while still failing a real hang.
      testTimeout: 60_000,
    },
  };

  const browser: VitestBrowserProject = {
    // Pre-bundle the browser render helpers so Vitest does not discover and
    // re-optimize them mid-run (a reload after the first import otherwise drops
    // the test runner — "Vitest failed to find the runner").
    optimizeDeps: {
      include: mergeOptimizeDeps(extraBrowserOptimizeDeps),
    },
    test: {
      name: "browser",
      include: ["src/**/*.test.tsx"],
      exclude: [...configDefaults.exclude, ...nodeTsxSpecs],
      browser: {
        enabled: true,
        // Vitest 4 factory provider, NOT the v3 `"playwright"` string (throws).
        provider: playwright(),
        headless: true,
        instances: [{ browser: "chromium" }],
      },
    },
  };

  // Vite's own config merge, so the preset folds overrides in exactly the way
  // vitest folds a workspace config into a project one: nested plain objects
  // merge (the preset's name/environment/include/exclude survive an override
  // that only sets `test.server`) and arrays concatenate rather than replace.
  // `mergeConfig` is typed as `Record<string, any>`, hence the cast back.
  return { node: mergeConfig(node, nodeProjectOverrides) as VitestNodeProject, browser };
}
