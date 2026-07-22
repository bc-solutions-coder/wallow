import { join } from "node:path";

import { tanstackRouter } from "@tanstack/router-plugin/vite";
import { type UserConfig } from "vite";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createClientViteConfig, createSsrViteConfig } from "./vite-presets";

// The TanStack Router codegen plugin is stubbed so the seam's WIRING is what we
// assert — that the factory calls it, orders it before react, and derives its
// options from `appDir` — without booting the real generator. Its derived
// options are only observable through the recorded call args (a plugin's `.name`
// carries none), so the stub returns a single recognizably-named plugin object.
vi.mock("@tanstack/router-plugin/vite", () => ({
  tanstackRouter: vi.fn((options: unknown) => ({
    name: "tanstack:router-generator",
    __options: options,
  })),
}));

const tanstackRouterMock = vi.mocked(tanstackRouter);

beforeEach((): void => {
  tanstackRouterMock.mockClear();
});

/**
 * Acceptance-criteria guard for Wallow-0q2s.8.5 (vite preset factories). Both
 * factories are pure functions from an app's directory to a Vite `UserConfig`,
 * so these specs call them with a fixture `appDir` and assert the returned config
 * object's shape — no Vite build is run. What matters is the STABLE output
 * contract the standalone host + document shell depend on (the client bundle must
 * emit an unhashed `client.js` into `dist/client`; the SSR bundle must emit into
 * `dist/server` from `src/ssr.tsx`) and that the shared `wallowStyles()` Tailwind
 * + brand-assets plugins are composed into the CLIENT config only, mirroring the
 * four hand-written app configs this extraction replaces.
 *
 * They intentionally fail until the green phase implements the factories.
 */

const APP_DIR = "/tmp/wallow-vite-fixture";

/** Rollup's `output` can be a single object or an array; normalize to one object. */
function firstOutput(config: UserConfig): Record<string, unknown> {
  const output = config.build?.rollupOptions?.output;
  const resolved = Array.isArray(output) ? output[0] : output;
  return (resolved ?? {}) as Record<string, unknown>;
}

/** Recursively collect the `.name` of every resolved Vite plugin. */
async function collectPluginNames(plugins: unknown): Promise<string[]> {
  const resolved: unknown = await plugins;
  if (resolved === null || resolved === undefined || resolved === false) {
    return [];
  }
  if (Array.isArray(resolved)) {
    const nested: string[][] = await Promise.all(
      resolved.map((entry) => collectPluginNames(entry)),
    );
    return nested.flat();
  }
  if (typeof resolved === "object" && "name" in resolved) {
    return [String((resolved as { name: unknown }).name)];
  }
  return [];
}

/** Index of the first plugin whose resolved name matches, or -1. */
function firstIndex(names: string[], pattern: RegExp): number {
  return names.findIndex((name: string): boolean => pattern.test(name));
}

/** The single `options` object the (stubbed) tanstackRouter plugin was last called with. */
function lastRouterOptions(): Record<string, unknown> {
  expect(tanstackRouterMock).toHaveBeenCalled();
  return tanstackRouterMock.mock.calls.at(-1)?.[0] as Record<string, unknown>;
}

describe("createClientViteConfig", () => {
  it("builds the client bundle into dist/client, emptying it first", () => {
    const config: UserConfig = createClientViteConfig({ appDir: APP_DIR });

    expect(config.build?.outDir).toBe("dist/client");
    expect(config.build?.emptyOutDir).toBe(true);
  });

  it("uses the app's src/client.tsx as the rollup input, resolved against appDir", () => {
    const config: UserConfig = createClientViteConfig({ appDir: APP_DIR });

    expect(config.build?.rollupOptions?.input).toBe(join(APP_DIR, "src", "client.tsx"));
  });

  it("pins the stable, unhashed client.js output contract the host + shell depend on", () => {
    const output = firstOutput(createClientViteConfig({ appDir: APP_DIR }));

    expect(output.entryFileNames).toBe("client.js");
    expect(output.chunkFileNames).toBe("[name].js");
    expect(output.assetFileNames).toBe("[name][extname]");
  });

  it("composes the React plugin into the client build", async () => {
    const names: string[] = await collectPluginNames(
      createClientViteConfig({ appDir: APP_DIR }).plugins,
    );

    expect(names.some((name: string): boolean => /react/i.test(name))).toBe(true);
  });

  it("composes wallowStyles() (Tailwind + brand assets) into the client build by default", async () => {
    const names: string[] = await collectPluginNames(
      createClientViteConfig({ appDir: APP_DIR }).plugins,
    );

    expect(names).toContain("wallow:brand-assets");
  });

  it("resolves the input against the given appDir (per-app seam)", () => {
    const other = "/tmp/some-other-app";
    const config: UserConfig = createClientViteConfig({ appDir: other });

    expect(config.build?.rollupOptions?.input).toBe(join(other, "src", "client.tsx"));
  });

  it("wires the TanStack Router codegen plugin into the client build", async () => {
    const names: string[] = await collectPluginNames(
      createClientViteConfig({ appDir: APP_DIR }).plugins,
    );

    expect(names.some((name: string): boolean => /tanstack.?router/i.test(name))).toBe(true);
  });

  it("orders the router plugin BEFORE react (codegen must see untransformed routes)", async () => {
    const names: string[] = await collectPluginNames(
      createClientViteConfig({ appDir: APP_DIR }).plugins,
    );

    const routerIndex: number = firstIndex(names, /tanstack.?router/i);
    const reactIndex: number = firstIndex(names, /react/i);

    expect(routerIndex).toBeGreaterThanOrEqual(0);
    expect(reactIndex).toBeGreaterThanOrEqual(0);
    expect(routerIndex).toBeLessThan(reactIndex);
  });

  it("derives the router plugin's route paths from appDir and targets react", () => {
    createClientViteConfig({ appDir: APP_DIR });

    const options: Record<string, unknown> = lastRouterOptions();
    expect(options.target).toBe("react");
    expect(options.routesDirectory).toBe(join(APP_DIR, "src", "routes"));
    expect(options.generatedRouteTree).toBe(join(APP_DIR, "src", "routeTree.gen.ts"));
  });

  it("keeps automatic route-based code splitting OFF (runtime-behavior change is out of scope)", () => {
    createClientViteConfig({ appDir: APP_DIR });

    expect(lastRouterOptions().autoCodeSplitting).toBe(false);
  });

  it("derives the router plugin's route paths against the given appDir (per-app seam)", () => {
    const other = "/tmp/some-other-app";
    createClientViteConfig({ appDir: other });

    const options: Record<string, unknown> = lastRouterOptions();
    expect(options.routesDirectory).toBe(join(other, "src", "routes"));
    expect(options.generatedRouteTree).toBe(join(other, "src", "routeTree.gen.ts"));
  });
});

describe("createSsrViteConfig", () => {
  it("builds the SSR bundle from src/ssr.tsx into dist/server, emptying it first", () => {
    const config: UserConfig = createSsrViteConfig({ appDir: APP_DIR });

    expect(config.build?.ssr).toBe(join(APP_DIR, "src", "ssr.tsx"));
    expect(config.build?.outDir).toBe("dist/server");
    expect(config.build?.emptyOutDir).toBe(true);
  });

  it("composes the React plugin into the SSR build", async () => {
    const names: string[] = await collectPluginNames(
      createSsrViteConfig({ appDir: APP_DIR }).plugins,
    );

    expect(names.some((name: string): boolean => /react/i.test(name))).toBe(true);
  });

  it("does NOT compose wallowStyles() into the SSR build (react-only, matching the app configs)", async () => {
    const names: string[] = await collectPluginNames(
      createSsrViteConfig({ appDir: APP_DIR }).plugins,
    );

    expect(names).not.toContain("wallow:brand-assets");
  });

  it("resolves the SSR entry against the given appDir (per-app seam)", () => {
    const other = "/tmp/some-other-app";
    const config: UserConfig = createSsrViteConfig({ appDir: other });

    expect(config.build?.ssr).toBe(join(other, "src", "ssr.tsx"));
  });

  it("does NOT wire the router codegen plugin into the SSR build (it imports the generated tree)", async () => {
    const names: string[] = await collectPluginNames(
      createSsrViteConfig({ appDir: APP_DIR }).plugins,
    );

    expect(names.some((name: string): boolean => /tanstack.?router/i.test(name))).toBe(false);
  });
});
