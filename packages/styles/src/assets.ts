/**
 * Where the shared brand assets live on disk — the `@bc-solutions-coder/styles/assets`
 * subpath.
 *
 * A consuming app needs the actual files at its served root (that is what makes
 * {@link appIconUrl} resolve), so its build has to copy them from somewhere.
 * That "somewhere" is this package, not a per-app copy of the icon: one asset,
 * one place a fork replaces it.
 *
 * Deliberately a SEPARATE subpath from the package's main entry: this module
 * reads `import.meta.url` through `node:url`, and the main entry is bundled into
 * consumers' browser builds. Keeping the two apart is what stops a `node:path`
 * import from following the branding types into the client bundle.
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
