import { existsSync, readdirSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { defineConfig } from "vite";

// Vite 8 library-mode build for the ui component library (mirrors
// packages/testing/vite.config.ts). Vite 8 bundles with Rolldown natively, but
// neither Vite nor Rolldown emits type declarations — those come from
// `tsc -p tsconfig.build.json` (see the package `build` script).
//
// ES output only, and every non-relative import is externalized so
// react/react-dom (peer deps) and every other runtime/test dependency are never
// bundled in.
//
// `preserveModules` keeps one output module per source module so `dist/` mirrors
// `src/` and each component stays addressable at `dist/components/<name>/index.js`
// — the path the package's `"./*"` subpath export points at. Every component's
// folder barrel is also declared as an entry: a re-export-only module is
// otherwise inlined into its importer and no `index.js` is emitted for it.
export default defineConfig({
  build: {
    target: "es2023",
    outDir: "dist",
    emptyOutDir: true,
    sourcemap: true,
    minify: false,
    lib: {
      entry: {
        index: fileURLToPath(new URL("src/index.ts", import.meta.url)),
        ...componentEntries(),
      },
      formats: ["es"],
    },
    rollupOptions: {
      external: (id) => !id.startsWith(".") && !id.startsWith("/") && !isAbsoluteWindows(id),
      output: {
        entryFileNames: "[name].js",
        preserveModules: true,
        preserveModulesRoot: "src",
      },
    },
  },
});

function isAbsoluteWindows(id: string): boolean {
  return /^[a-zA-Z]:[\\/]/u.test(id);
}

/**
 * Every `src/components/<name>/index.ts`, keyed by the output path it must land
 * at — `entryFileNames: "[name].js"` turns the key into the emitted filename, so
 * `components/button/index` produces `dist/components/button/index.js`.
 */
function componentEntries(): Record<string, string> {
  const componentsDir = new URL("src/components/", import.meta.url);
  const entries: Record<string, string> = {};

  for (const entry of readdirSync(componentsDir, { withFileTypes: true })) {
    const barrel = new URL(`${entry.name}/index.ts`, componentsDir);

    if (entry.isDirectory() && existsSync(barrel)) {
      entries[`components/${entry.name}/index`] = fileURLToPath(barrel);
    }
  }

  return entries;
}
