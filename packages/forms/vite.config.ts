import { fileURLToPath } from "node:url";

import { defineConfig } from "vite";

// Vite 8 library-mode build for the forms layer (mirrors packages/ui's config,
// minus its per-component entries). Vite 8 bundles with Rolldown natively, but
// neither Vite nor Rolldown emits type declarations — those come from
// `tsc -p tsconfig.build.json` (see the package `build` script).
//
// ES output only, and every non-relative import is externalized so react and
// react-dom (peer deps) and the query, ui and sdk workspace packages the host app
// already has are never bundled in — a second copy of any of them would duplicate
// a runtime.
//
// Unlike packages/ui this package publishes ONE entry: the curated `src/index.ts`
// barrel. There is no subpath export to back, so there are no extra entries to
// enumerate. `preserveModules` is still on so `dist/` mirrors `src/` and a
// consuming bundler can drop catalog fields the app never imports.
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
