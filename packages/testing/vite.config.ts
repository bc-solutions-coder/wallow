import { fileURLToPath } from "node:url";

import { defineConfig } from "vite";

// Vite 8 library-mode build for the testing preset package (mirrors
// packages/sdk/vite.config.ts). Vite 8 bundles with Rolldown natively, but
// neither Vite nor Rolldown emits type declarations — those come from
// `tsc -p tsconfig.build.json` (see the package `build` script).
//
// This package exposes a config-safe `.` barrel (`index` -> src/index.ts, plus
// `sdk-harness`, which imports no browser-only module) alongside browser-only
// subpaths (`render` -> src/render.tsx, `render-with-wallow` ->
// src/render-with-wallow.tsx), so each gets its own named lib entry. ES output
// only, and every non-relative import is externalized so runtime/test deps are
// never bundled in.
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
        render: fileURLToPath(new URL("src/render.tsx", import.meta.url)),
        "sdk-harness": fileURLToPath(new URL("src/sdk-harness.ts", import.meta.url)),
        contrast: fileURLToPath(new URL("src/contrast.ts", import.meta.url)),
        "render-with-wallow": fileURLToPath(new URL("src/render-with-wallow.tsx", import.meta.url)),
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
  return /^[a-zA-Z]:[\\/]/u.test(id);
}
