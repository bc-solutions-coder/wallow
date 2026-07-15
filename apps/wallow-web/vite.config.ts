import { fileURLToPath } from "node:url";

import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Client bundle build for the tanstack-min BFF example.
//
// Output contract (preserved from the previous esbuild pipeline): the h3 BFF in
// server.ts serves `public/` statically and `public/index.html` hardcodes
// `<script type="module" src="/app.js">`. So the browser entry (src/app.ts) has
// to land at `public/app.js` with a STABLE, unhashed name — not Vite's default
// hashed asset in `dist/` with an injected <script>. We therefore drive a plain
// library-style entry build rather than Vite's html-entry app build:
//   • build.rollupOptions.input points at src/app.ts (no root index.html; the
//     app's index.html is a static asset in public/, not a Vite html entry).
//   • output.entryFileNames pins the emitted file to `app.js`.
//   • build.outDir is `public`, and publicDir is disabled so it does not collide
//     with outDir (Vite forbids publicDir === outDir).
//   • emptyOutDir is false so building the bundle never wipes the tracked
//     public/index.html sitting alongside it.
// @vitejs/plugin-react is wired in so this promoted reference app carries the
// workspace-standard React toolchain even though today's entry has no JSX.
export default defineConfig({
  plugins: [react()],
  publicDir: false,
  build: {
    outDir: "public",
    emptyOutDir: false,
    rollupOptions: {
      input: fileURLToPath(new URL("src/app.ts", import.meta.url)),
      output: {
        entryFileNames: "app.js",
        chunkFileNames: "[name].js",
        assetFileNames: "[name][extname]",
      },
    },
  },
});
