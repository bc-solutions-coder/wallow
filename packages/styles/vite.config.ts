import { fileURLToPath } from "node:url";

import { defineConfig } from "vite";

// Vite 8 library-mode build, mirroring packages/sdk: Vite/Rolldown bundles the
// JS and `tsc -p tsconfig.build.json` emits the declarations (see the package
// `build` script) — neither Vite nor Rolldown emits .d.ts.
//
// The CSS entry (styles.css) is deliberately NOT part of this build. It is
// shipped as-authored and consumed through the "./styles.css" export: Tailwind
// v4 is CSS-first, so the consuming app's own Tailwind pass resolves the
// `@import "tailwindcss"` and applies its own `@source` scanning. Pre-building
// it here would bake in this package's (empty) source scan instead.
//
// Output contract: dist/index.js at a STABLE, unhashed name (see Wallow-do5e —
// no content hashing anywhere in this workspace without a build manifest).
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
        assets: fileURLToPath(new URL("src/assets.ts", import.meta.url)),
        vite: fileURLToPath(new URL("src/vite.ts", import.meta.url)),
      },
      formats: ["es"],
    },
    rollupOptions: {
      external: (id) => !id.startsWith(".") && !id.startsWith("/") && !isAbsoluteWindows(id),
      output: {
        entryFileNames: "[name].js",
        chunkFileNames: "[name]-[hash].js",
        assetFileNames: "[name][extname]",
      },
    },
  },
});

function isAbsoluteWindows(id: string): boolean {
  return /^[a-zA-Z]:[\\/]/u.test(id);
}
