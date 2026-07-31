import { readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

import type { ConfigEnv, ConfigPluginContext, Plugin, PluginOption, UserConfig } from "vite";
import { describe, expect, it } from "vitest";

import { brandAssetsDir } from "./assets";
import { forkResolvedBranding, renderThemeStyle } from "./branding";
import { brandAssetsPlugin, forkThemePlugin, THEME_MODULE_ID, wallowStyles } from "./vite";

/**
 * The `./vite` subpath is the one place a Wallow app's Tailwind + brand-assets
 * wiring lives. These tests pin the two behaviours apps depend on — the Tailwind
 * plugin is present, and the brand-assets plugin points `publicDir` at the
 * package's own assets directory — plus the manifest/build wiring that makes the
 * subpath resolve for a published consumer.
 */
const packageRoot: string = fileURLToPath(new URL("../", import.meta.url));

interface Manifest {
  readonly dependencies?: Readonly<Record<string, string>>;
  readonly devDependencies?: Readonly<Record<string, string>>;
  readonly exports?: Readonly<Record<string, unknown>>;
}

const manifest: Manifest = JSON.parse(
  readFileSync(join(packageRoot, "package.json"), "utf8"),
) as Manifest;

/**
 * `tailwindcss()` returns a nested `PluginOption` (an array of Vite plugins), so
 * flatten the tree `wallowStyles()` produces down to the concrete plugin objects
 * that carry a `name`.
 */
function flattenPlugins(options: readonly PluginOption[]): Plugin[] {
  const plugins: Plugin[] = [];

  for (const option of options) {
    if (!option) {
      // skip falsy plugin slots
    } else if (Array.isArray(option)) {
      plugins.push(...flattenPlugins(option));
    } else if (typeof option === "object" && "name" in option) {
      plugins.push(option as Plugin);
    }
  }

  return plugins;
}

/**
 * Invoke a plugin's `config()` hook (function or object-with-handler form) the
 * way Vite would, returning the partial config it contributes.
 */
function invokeConfigHook(plugin: Plugin): UserConfig {
  const env: ConfigEnv = { command: "serve", mode: "development" };
  const hook = plugin.config;
  const ctx: ConfigPluginContext = {
    ...plugin,
    warn: () => {},
    error: () => {},
    info: () => {},
    debug: () => {},
  } as unknown as ConfigPluginContext;

  if (typeof hook === "function") {
    return (hook.call(ctx, {}, env) as UserConfig | null | undefined) ?? {};
  }

  if (hook && typeof hook.handler === "function") {
    return (hook.handler.call(ctx, {}, env) as UserConfig | null | undefined) ?? {};
  }

  return {};
}

describe("wallowStyles", () => {
  it("includes the Tailwind Vite plugin", () => {
    const plugins: Plugin[] = flattenPlugins(wallowStyles());
    const names: string[] = plugins.map((plugin) => plugin.name);

    expect(names.some((name) => name.includes("tailwind"))).toBe(true);
  });

  it("includes the brand-assets plugin", () => {
    const plugins: Plugin[] = flattenPlugins(wallowStyles());

    expect(plugins).toContain(brandAssetsPlugin);
  });

  it("includes the fork-theme plugin AHEAD of Tailwind's", () => {
    // `virtual:wallow-theme.css` ends in `.css`, so `@tailwindcss/vite` would
    // claim the id first and try to compile it as a Tailwind entry. `enforce:
    // "pre"` plus this ordering is what keeps it ours.
    const names: string[] = flattenPlugins(wallowStyles()).map((plugin) => plugin.name);

    expect(names).toContain(forkThemePlugin.name);
    expect(names.indexOf(forkThemePlugin.name)).toBeLessThan(
      names.findIndex((name) => name.includes("tailwind")),
    );
  });
});

describe("forkThemePlugin", () => {
  /** Call a plugin hook that may be declared as a function or `{ handler }`. */
  function invokeHook(plugin: Plugin, hookName: "resolveId" | "load", id: string): unknown {
    const hook = plugin[hookName];
    const handler = typeof hook === "function" ? hook : hook?.handler;

    return (handler as (this: unknown, value: string) => unknown).call({}, id);
  }

  it(String.raw`resolves the theme id to a \0-prefixed virtual id`, () => {
    // Vite's convention: the `\0` prefix keeps other plugins (and the dep
    // optimizer's scanner) off a module that has no file behind it.
    expect(invokeHook(forkThemePlugin, "resolveId", THEME_MODULE_ID)).toBe(`\0${THEME_MODULE_ID}`);
  });

  it("claims nothing else", () => {
    expect(invokeHook(forkThemePlugin, "resolveId", "./styles.css")).toBeUndefined();
    expect(invokeHook(forkThemePlugin, "load", "./styles.css")).toBeUndefined();
  });

  it("serves the fork's resolved theme as CSS", () => {
    const css = invokeHook(forkThemePlugin, "load", `\0${THEME_MODULE_ID}`) as string;

    // Exactly `renderThemeStyle(forkResolvedBranding)` — the same bytes an app's
    // root route puts in its document head, so a spec measures the fork's real
    // palette rather than one hand-written for tests.
    expect(css).toBe(renderThemeStyle(forkResolvedBranding));
    // The values `styles.css`'s valueless colour tokens read; without a `:root`
    // block every `var()` is invalid-at-computed-value-time.
    expect(css).toContain(":root {");
    expect(css).toContain("--background:");
  });

  it("renders the theme per request rather than from a build artifact", () => {
    // The root `pnpm check` runs `test` BEFORE `build`, so a generated theme.css
    // on disk would be stale exactly when it matters. Two loads of a changed
    // branding must not be able to disagree, which is why `load` calls
    // `renderThemeStyle` itself instead of closing over a cached string.
    const source: string = readFileSync(join(packageRoot, "src/vite.ts"), "utf8");

    expect(source).toContain("renderThemeStyle(forkResolvedBranding)");
  });
});

describe("brandAssetsPlugin", () => {
  it("points publicDir at brandAssetsDir through its config() hook", () => {
    // Contributed via the config() hook (not a raw publicDir field) so it merges
    // with the rest of an app's Vite config instead of clobbering it.
    const config: UserConfig = invokeConfigHook(brandAssetsPlugin);

    expect(config.publicDir).toBe(brandAssetsDir);
  });
});

describe("the package manifest", () => {
  it("declares the Tailwind deps as real dependencies, not devDependencies", () => {
    // The apps stop owning these; the package that owns the Tailwind wiring owns
    // the deps. They must ship with the package, so they are dependencies.
    expect(manifest.dependencies?.["@tailwindcss/vite"]).toBeDefined();
    expect(manifest.dependencies?.["tailwindcss"]).toBeDefined();

    expect(manifest.devDependencies?.["@tailwindcss/vite"]).toBeUndefined();
    expect(manifest.devDependencies?.["tailwindcss"]).toBeUndefined();
  });

  it("exports the ./vite subpath, pointing at the built entry", () => {
    // Same dist-pointing shape as the "." and "./assets" exports.
    expect(manifest.exports?.["./vite"]).toEqual({
      types: "./dist/vite.d.ts",
      import: "./dist/vite.js",
    });
  });
});

describe("the library build wiring", () => {
  it("bundles the vite entry", () => {
    const viteConfig: string = readFileSync(join(packageRoot, "vite.config.ts"), "utf8");

    expect(viteConfig).toContain("src/vite.ts");
  });

  it("emits declarations for the vite entry", () => {
    const buildTsconfig: string = readFileSync(join(packageRoot, "tsconfig.build.json"), "utf8");

    expect(buildTsconfig).toContain("src/vite.ts");
  });
});
