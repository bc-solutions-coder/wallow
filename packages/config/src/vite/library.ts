import { fileURLToPath } from "node:url";

import { defineConfig } from "vite";
import type { UserConfig } from "vite";

/**
 * Entry points and output layout for a workspace ESM library build. Type declarations are emitted
 * separately by the package TypeScript build.
 */
export interface LibraryConfigOptions {
  /**
   * The calling config's `import.meta.url`. Entry paths are resolved against it,
   * so they can be written package-relative (`"src/index.ts"`) and stay correct
   * no matter what directory the build is invoked from.
   */
  readonly configUrl: string;

  /**
   * Lib entries, keyed by the output path each must land at: `entryFileNames:
   * "[name].js"` turns the key into the emitted filename, so `server/index`
   * produces `dist/server/index.js`. Values are package-relative source paths.
   *
   * Every subpath in the package's `exports` map needs an entry here. A
   * re-export-only barrel especially: without its own entry it is inlined into
   * its importer and no file is emitted for it at all.
   */
  readonly entries: Readonly<Record<string, string>>;

  /**
   * Emit one output module per source module, mirroring `src/` into `dist/`.
   *
   * Set this when the package exports a wildcard subpath (`"./*"`) that must
   * resolve to a real per-directory file, or when consumers should be able to
   * tree-shake away the parts of a catalog their app never imports. Off by
   * default: a package with a fixed, enumerated set of entries is better served
   * by bundling each one.
   */
  readonly preserveModules?: boolean;
}

/** Rolldown hands absolute Windows paths through unchanged; they are not bare specifiers. */
function isAbsoluteWindows(id: string): boolean {
  return /^[a-zA-Z]:[\\/]/u.test(id);
}

/**
 * Create an unminified ES2023 ESM library build in dist with source maps. Entry and asset
 * filenames are stable, internal chunks carry hashes, and bare imports remain external. Vite
 * clears dist on build; declarations require a separate TypeScript invocation.
 *
 * @param options Source entries resolved relative to configUrl and optional source-module
 * preservation.
 */
export function defineLibraryConfig(options: LibraryConfigOptions): UserConfig {
  const entry: Record<string, string> = {};
  for (const [name, source] of Object.entries(options.entries)) {
    entry[name] = fileURLToPath(new URL(source, options.configUrl));
  }

  return defineConfig({
    build: {
      target: "es2023",
      outDir: "dist",
      emptyOutDir: true,
      sourcemap: true,
      minify: false,
      lib: { entry, formats: ["es"] },
      // Use the Vite Rolldown build configuration for external imports and stable entry filenames.
      rolldownOptions: {
        external: (id: string): boolean =>
          !id.startsWith(".") && !id.startsWith("/") && !isAbsoluteWindows(id),
        output: {
          entryFileNames: "[name].js",
          chunkFileNames: "[name]-[hash].js",
          assetFileNames: "[name][extname]",
          ...(options.preserveModules === true
            ? { preserveModules: true, preserveModulesRoot: "src" }
            : {}),
        },
      },
    },
  });
}
