import { join } from "node:path";

import { type UserConfig } from "vite";
import { describe, expect, it } from "vitest";

import { createClientViteConfig, createSsrViteConfig } from "./vite-presets";

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
    const nested: string[][] = await Promise.all(resolved.map(collectPluginNames));
    return nested.flat();
  }
  if (typeof resolved === "object" && "name" in resolved) {
    return [String((resolved as { name: unknown }).name)];
  }
  return [];
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
});
