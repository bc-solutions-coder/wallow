/**
 * Shared Vitest two-project (node + real-Chromium browser) preset, extracted
 * from the identical shape hand-rolled in apps/wallow-auth/vitest.config.ts and
 * apps/wallow-web/vitest.config.ts.
 *
 * `createVitestProjects(options)` returns the `{ node, browser }` project pair
 * consumed by `defineConfig({ test: { projects: [node, browser] } })`:
 *
 *   node    — pure-logic specs: `src/**\/*.test.ts` plus a caller-supplied list
 *             of pure-logic/SSR `*.test.tsx` specs (`nodeTsxSpecs`). No DOM.
 *   browser — every component spec (`src/**\/*.test.tsx`) MINUS `nodeTsxSpecs`,
 *             run in headless Chromium via the Vitest 4 `playwright()` factory
 *             provider (NOT the v3 `"playwright"` string, which throws).
 *
 * App-local knobs that must NOT live in the shared package (wallow-web's
 * `resolve.alias['openid-client']` + `test.server.deps.inline`) are passed
 * through `nodeProjectOverrides` and deep-merged into the node project.
 */

import { playwright } from "@vitest/browser-playwright";
import { configDefaults } from "vitest/config";

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

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Recursively merge `override` into `base`: nested plain objects are merged;
 * everything else (arrays, primitives, regexes, functions) replaces. Used to
 * fold `nodeProjectOverrides` into the node project WITHOUT clobbering the
 * preset's `name`/`environment`/`include`/`exclude` fields.
 */
function deepMerge<T extends Record<string, unknown>>(
  base: T,
  override: Record<string, unknown>,
): T {
  const result: Record<string, unknown> = { ...base };
  for (const [key, value] of Object.entries(override)) {
    const existing = result[key];
    result[key] =
      isPlainObject(existing) && isPlainObject(value) ? deepMerge(existing, value) : value;
  }
  return result as T;
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

  return { node: deepMerge(node, nodeProjectOverrides), browser };
}
