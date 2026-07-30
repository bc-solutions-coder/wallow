import { fileURLToPath } from "node:url";

import { defineConfig } from "vite";

// Vite 8 library-mode build for the auth package (mirrors
// packages/query/vite.config.ts). Vite 8 bundles with Rolldown natively, but
// neither Vite nor Rolldown emits type declarations — those come from
// `tsc -p tsconfig.build.json` (see the package `build` script).
//
// This package exposes a single browser-safe `.` barrel (the shared authn/authz
// surface), so there is one named lib entry (`index` -> src/index.ts), ES output
// only, and every non-relative import is externalized so runtime deps are never
// bundled in. Externalizing is load-bearing here for the same reason it is in
// packages/query: a bundled copy of @bc-solutions-coder/query would hand
// consumers a second react-query instance with its own QueryClientProvider
// context, and a bundled copy of the SDK would give them a second generated
// client whose query keys no longer match the ones their app invalidates.
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
        chunkFileNames: "[name]-[hash].js",
      },
    },
  },
});

function isAbsoluteWindows(id: string): boolean {
  return /^[a-zA-Z]:[\\/]/u.test(id);
}
