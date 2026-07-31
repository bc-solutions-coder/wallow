import { fileURLToPath } from "node:url";

import { defineConfig } from "vite";
import type { UserConfig } from "vite";

/**
 * The one Vite library-mode build every `packages/*` shares.
 *
 * Rolldown IS the bundler in Vite 8 — it ships as part of vite, so there is no
 * separate dependency, no `rolldown-vite` alias and no package override. What it
 * does not do is emit type declarations; those come from
 * `tsc -p tsconfig.build.json`, which every package's `build` script runs
 * alongside `vite build`.
 *
 * Two invariants this preset exists to hold identically everywhere:
 *
 *  - **Every non-relative import is external.** Nothing a package depends on is
 *    bundled into its own output. That is load-bearing rather than tidy: a
 *    bundled copy of `@bc-solutions-coder/query` hands consumers a second
 *    react-query instance with its own `QueryClientProvider` context, a bundled
 *    SDK gives them a second generated client whose query keys no longer match
 *    the ones their app invalidates, and a bundled `react` breaks hooks outright.
 *  - **No content hashing on anything a consumer names** (Wallow-do5e — no
 *    hashed filenames anywhere in this workspace without a build manifest). Entry
 *    and asset names are stable and unhashed so the `exports` map can point
 *    straight at them; only internal chunks, which nothing outside the bundle
 *    references, carry a hash.
 *
 * This lives under `tools/` rather than in a workspace package deliberately: it
 * is imported by the build of `packages/testing` itself, so it must not be
 * something that has to be built first.
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

/** Build the Vite config for one workspace library. */
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
      // `rolldownOptions`, not `rollupOptions` — Vite 8 bundles with Rolldown
      // natively and the `rollupOptions` name is now a deprecated alias for this
      // one. Same option type either way (`RolldownOptions`).
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
