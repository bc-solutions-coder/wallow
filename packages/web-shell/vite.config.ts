import { fileURLToPath } from "node:url";

import { defineConfig } from "vite";

// Vite 8 library-mode build for the web-shell package (mirrors
// packages/sdk/vite.config.ts). Vite 8 bundles with Rolldown natively, but
// neither Vite nor Rolldown emits type declarations — those come from
// `tsc -p tsconfig.build.json` (see the package `build` script).
//
// This package exposes a browser-safe `.` barrel (the query-client lands here in
// 8.2) plus a Node-only `./server` subpath (host/dev-server/vite-presets), so
// there are two named lib entries (`index` -> src/index.ts, `server/index` ->
// src/server/index.ts), ES output only, and every non-relative import is
// externalized so runtime deps are never bundled in.
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
  return /^[a-zA-Z]:[\\/]/u.test(id);
}
