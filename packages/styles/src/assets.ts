/**
 * Node-only location of shared brand assets for build tools. Browser consumers use the URL
 * helpers from the main package entry.
 */
import { fileURLToPath } from "node:url";

/**
 * Absolute path to the directory holding the shared brand assets.
 *
 * Resolved relative to this module rather than to the process's working
 * directory, so it answers the same whether the caller loaded the built entry or
 * (under vitest) the source — both sit one level under the package root.
 */
export const brandAssetsDir: string = fileURLToPath(new URL("../assets/", import.meta.url));
