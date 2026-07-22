/**
 * Vite authoring surface for `@bc-solutions-coder/styles` — the `./vite` subpath.
 *
 * A consuming app's whole Tailwind + brand-assets wiring collapses into a single
 * {@link wallowStyles} call in its Vite `plugins` array. This module owns the
 * `@tailwindcss/vite` plugin registration and the `publicDir = brandAssetsDir`
 * wiring so no app has to repeat either.
 *
 * Deliberately a SEPARATE subpath from the package's main entry (like `./assets`):
 * this is node-only Vite plugin-authoring code and must never bundle into a
 * consumer's browser build, so it stays off `./index.ts`.
 *
 */
import tailwindcss from "@tailwindcss/vite";
import type { Plugin, PluginOption, UserConfig } from "vite";

import { brandAssetsDir } from "./assets";

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
 * The complete set of Vite plugins a Wallow frontend needs for styling:
 * the Tailwind v4 plugin plus the brand-assets plugin.
 */
export function wallowStyles(): PluginOption[] {
  return [tailwindcss(), brandAssetsPlugin];
}
