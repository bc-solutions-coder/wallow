/**
 * Vite plugins for Tailwind, shared public assets, and the virtual fork-theme stylesheet. This
 * Node-only entry belongs in build configuration.
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
 * Virtual stylesheet import that supplies the resolved fork palette for :root, .dark, and .light.
 * With wallowStyles installed, import virtual:wallow-theme.css when the document does not already
 * render renderThemeStyle output. The shared styles.css maps tokens but does not supply palette
 * values.
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
 * Return the fork-theme plugin, Tailwind plugin collection, and shared-assets plugin for a Vite
 * plugins array. Sets publicDir to the package brand assets and serves virtual:wallow-theme.css.
 * Consumers still import styles.css and declare their own Tailwind source scan.
 */
export function wallowStyles(): PluginOption[] {
  return [forkThemePlugin, tailwindcss(), brandAssetsPlugin];
}
