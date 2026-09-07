/**
 * Shared Vitest projects for Node logic/SSR tests and headless Chromium component tests.
 * Consumers supply their own browser setup files and stylesheet plugins.
 */

import type { PluginOption } from "vite";

import { playwright } from "@vitest/browser-playwright";
import { configDefaults, mergeConfig } from "vitest/config";

import { mergeOptimizeDeps } from "./browser-optimize-deps";

/**
 * The node-project convention for `*.test.tsx` specs. Named `.ssr.` because
 * every spec that qualifies renders through `react-dom/server` or asserts a
 * route gate — the two things a browser project cannot do usefully.
 */
export const ssrSpecGlob = "src/**/*.ssr.test.tsx";

/**
 * Options for creating the Node and browser test projects. Browser styling and setup are opt-in.
 */
export interface VitestProjectsOptions {
  /**
   * Pure-logic / SSR `*.test.tsx` specs that belong on node, not in Chromium.
   * Defaults to the `*.ssr.test.tsx` convention; pass an explicit list only to
   * replace that default pattern.
   */
  nodeTsxSpecs?: string[];
  /** App-specific `optimizeDeps.include` entries added onto the shared baseline. */
  extraBrowserOptimizeDeps?: string[];
  /** App-local node-project overrides (e.g. resolve.alias, server.deps.inline). */
  nodeProjectOverrides?: Record<string, unknown>;
  /** Vite plugins for the BROWSER project only — in practice `wallowStyles()`. */
  browserPlugins?: PluginOption[];
  /** Browser-project setup files, resolved against each project's own root. */
  browserSetupFiles?: string[];
}

/**
 * Node test settings returned by the preset. Additional keys may come from nodeProjectOverrides.
 */
export interface VitestNodeTestConfig {
  name: string;
  environment: string;
  include: string[];
  exclude: string[];
  /** Timeout in milliseconds. The preset allows 60 seconds for cold route imports. */
  testTimeout: number;
  [key: string]: unknown;
}

/**
 * Node project configuration after merging consumer overrides.
 */
export interface VitestNodeProject {
  test: VitestNodeTestConfig;
  [key: string]: unknown;
}

/**
 * Browser engine configuration for one Vitest browser instance.
 */
export interface VitestBrowserInstance {
  /** Vitest browser engine; a literal union so the pair satisfies `defineConfig`. */
  browser: "chromium" | "firefox" | "webkit";
}

/**
 * Headless browser configuration and the Playwright provider used by the preset.
 */
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

/**
 * Component test file patterns, setup files, and browser runtime settings.
 */
export interface VitestBrowserTestConfig {
  name: string;
  include: string[];
  exclude: string[];
  setupFiles: string[];
  browser: VitestBrowserConfig;
}

/**
 * Browser project configuration including plugins and pre-bundled dependencies.
 */
export interface VitestBrowserProject {
  plugins: PluginOption[];
  optimizeDeps: { include: string[] };
  test: VitestBrowserTestConfig;
}

/**
 * Node and browser project configurations to pass to Vitest's test.projects array.
 */
export interface VitestProjectsPair {
  node: VitestNodeProject;
  browser: VitestBrowserProject;
}

/**
 * Create Node and headless Chromium project configurations for Vitest.
 *
 * Node runs .test.ts files and, by default, .ssr.test.tsx files. Other .test.tsx
 * files run in Chromium. nodeTsxSpecs replaces the SSR pattern; Node overrides
 * use Vite's config merge. Browser styles and setup files must be supplied.
 *
 * @returns The node and browser projects for defineConfig({ test: { projects } }).
 */
export function createVitestProjects(options: VitestProjectsOptions = {}): VitestProjectsPair {
  const {
    nodeTsxSpecs = [ssrSpecGlob],
    extraBrowserOptimizeDeps = [],
    nodeProjectOverrides = {},
    browserPlugins = [],
    browserSetupFiles = [],
  } = options;

  const node: VitestNodeProject = {
    test: {
      name: "node",
      environment: "node",
      include: ["src/**/*.test.ts", ...nodeTsxSpecs],
      exclude: [...configDefaults.exclude],
      // Cold route imports transform the full route graph and need more than the default timeout.
      testTimeout: 60_000,
    },
  };

  const browser: VitestBrowserProject = {
    plugins: browserPlugins,
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
      setupFiles: browserSetupFiles,
      browser: {
        enabled: true,
        // Vitest 4 factory provider, NOT the v3 `"playwright"` string (throws).
        // Clipboard access is granted up front: a copy affordance is asserted
        // by reading the clipboard back, and headless Chromium denies both
        // directions unless the context asks.
        provider: playwright({
          contextOptions: { permissions: ["clipboard-read", "clipboard-write"] },
        }),
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
