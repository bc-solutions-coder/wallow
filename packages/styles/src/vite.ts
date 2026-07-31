/**
 * Vite authoring surface for `@bc-solutions-coder/styles` — the `./vite` subpath.
 *
 * A consuming app's whole Tailwind + brand-assets wiring collapses into a single
 * {@link wallowStyles} call in its Vite `plugins` array. This module owns the
 * `@tailwindcss/vite` plugin registration, the `publicDir = brandAssetsDir`
 * wiring, and the {@link THEME_MODULE_ID} virtual stylesheet, so no app has to
 * repeat any of them.
 *
 * Deliberately a SEPARATE subpath from the package's main entry (like `./assets`):
 * this is node-only Vite plugin-authoring code and must never bundle into a
 * consumer's browser build, so it stays off `./index.ts`.
 *
 */
import tailwindcss from "@tailwindcss/vite";
import type { Plugin, PluginOption, UserConfig } from "vite";

import { brandAssetsDir } from "./assets";
import { forkResolvedBranding, renderThemeStyle } from "./branding";

/**
 * Vite plugin that makes an app serve the shared brand assets from its root by
 * pointing `publicDir` at {@link brandAssetsDir}. It does this through the
 * `config()` hook (returning a partial config) rather than a raw `publicDir`
 * field so it composes when merged with the rest of an app's Vite config.
 */
export const brandAssetsPlugin: Plugin = {
  name: "wallow:brand-assets",
  config(): UserConfig {
    return { publicDir: brandAssetsDir };
  },
};

/**
 * Import specifier for the fork theme as a stylesheet — the custom-property
 * values (`:root` / `.dark` / `.light`) that `./styles.css`'s Tailwind colour
 * tokens read.
 *
 * `./styles.css` maps every colour token onto a VALUELESS custom property
 * (`--color-sidebar: var(--sidebar, var(--foreground))`), so without this module
 * `var(--sidebar)` is invalid-at-computed-value-time: a `bg-sidebar` element
 * paints transparent and its text falls back to inherited black. That makes a
 * rendered-colour assertion structurally incapable of catching a contrast
 * defect, which is why a test harness needs the theme and not just the
 * utilities.
 *
 * It is a VIRTUAL module rather than a JS import of `renderThemeStyle` for one
 * concrete reason. `@bc-solutions-coder/styles` is a LINKED workspace package, so
 * importing it from a Vitest setup file is Vite's first sight of that dependency:
 * Vite re-optimizes the dep graph mid-run and reloads the page, and the reload
 * hands the specs a SECOND `@tanstack/react-router` instance, after which a
 * `redirect` thrown through one module copy no longer satisfies `isRedirect`
 * from the other. A virtual id lives outside `node_modules`, so the optimizer
 * never scans it and there is nothing to discover — immune by construction
 * rather than immune by having remembered to list it in `optimizeDeps`.
 *
 * It is also NOT a generated `theme.css` file on disk: the root `pnpm check`
 * runs `test` BEFORE `build`, so a build-time artifact would be stale exactly
 * when it matters. Serving it from the plugin means the bytes are rendered from
 * `api/branding.json` on every request.
 *
 * The `.css` suffix is load-bearing twice over: Vite routes the module through
 * its CSS pipeline (so a bare `import` injects a `<style>`), and TypeScript's
 * `vite/client` `declare module "*.css"` wildcard matches it, so a consumer
 * needs no ambient declaration of its own.
 */
export const THEME_MODULE_ID = "virtual:wallow-theme.css";

/** Vite's convention for a resolved virtual id: `\0` keeps other plugins off it. */
const RESOLVED_THEME_MODULE_ID = `\0${THEME_MODULE_ID}`;

/**
 * Vite plugin serving {@link THEME_MODULE_ID} — the fork's resolved theme
 * rendered as CSS, the same `renderThemeStyle(forkResolvedBranding)` output an
 * app's root route puts in its document head.
 */
export const forkThemePlugin: Plugin = {
  name: "wallow:fork-theme",
  // Ahead of `@tailwindcss/vite`, which otherwise claims the `.css` id first.
  enforce: "pre",
  resolveId(id: string): string | undefined {
    return id === THEME_MODULE_ID ? RESOLVED_THEME_MODULE_ID : undefined;
  },
  load(id: string): string | undefined {
    return id === RESOLVED_THEME_MODULE_ID ? renderThemeStyle(forkResolvedBranding) : undefined;
  },
};

/**
 * The complete set of Vite plugins a Wallow frontend needs for styling: the
 * Tailwind v4 plugin, the brand-assets plugin, and the virtual fork theme
 * ({@link THEME_MODULE_ID}, which nothing imports unless it asks for it).
 */
export function wallowStyles(): PluginOption[] {
  return [forkThemePlugin, tailwindcss(), brandAssetsPlugin];
}
