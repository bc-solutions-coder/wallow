import { fileURLToPath } from "node:url";

import { defineConfig } from "vite";

// Vite 8 library-mode build for the SDK. This replaces tsup for the JS bundle
// ONLY — Vite 8 bundles with Rolldown natively (rolldown@~1.1.4 ships as a
// dependency of vite, so no rolldown-vite alias/override is needed), but neither
// Vite nor Rolldown emits type declarations. Declarations still come from
// `tsc -p tsconfig.build.json` (see the package `build` script).
//
// We do NOT drive declaration emit through the bundler because that path drives
// the TypeScript compiler API programmatically, which is unstable on the
// TypeScript 7.0 GA native compiler (the stable programmatic API does not land
// until 7.1) — and the rest of the workspace targets TS7. The native
// `tsc --emitDeclarationOnly` CLI emits .d.ts correctly, so that is the
// declaration path instead. See tsconfig.build.json for the full rationale,
// including why this package's own typescript devDependency is pinned to TS6.
//
// Output contract (preserved from the previous tsup pipeline, matching the
// package.json exports map): dist/index.js, dist/index.js.map,
// dist/server/index.js, dist/server/index.js.map. Two named lib entries keep
// the `.` and `./server` subpaths pointing at stable, unhashed filenames, and
// every non-relative import is externalized so runtime deps are not bundled.
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
        "server/index": fileURLToPath(new URL("src/server/index.ts", import.meta.url)),
      },
      formats: ["es"],
    },
    rollupOptions: {
      external: (id) => !id.startsWith(".") && !id.startsWith("/") && !isAbsoluteWindows(id),
      output: {
        entryFileNames: "[name].js",
        chunkFileNames: "[name]-[hash].js",
      },
    },
  },
});

function isAbsoluteWindows(id: string): boolean {
  return /^[a-zA-Z]:[\\/]/.test(id);
}
