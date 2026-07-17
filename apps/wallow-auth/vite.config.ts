import { fileURLToPath } from "node:url";

import { brandAssetsDir } from "@bc-solutions-coder/styles/assets";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Browser bundle build for wallow-auth (Wallow-vec7.1.5).
//
// The dev server (dev-server.ts) does NOT use this file — it drives Vite in
// middlewareMode with `configFile: false` and its own inline plugins, serving
// the entry straight from its module graph. So this config only governs
// `vite build`.
//
// Task 0.4 shipped no browser bundle (the app was an SSR shell + reverse proxy)
// and aimed `vite build` at the SSR entry as a stand-in target. The hydration
// entry now exists — it has to, since the readiness signal only means anything
// once the client has hydrated — so the default build is the real client bundle
// and the SSR build moves to vite.ssr.config.ts. `pnpm build` runs both.
//
// Output contract: the emitted file must be `client.js` with a STABLE, unhashed
// name, because the document shell hardcodes `<script type="module"
// src="/client.js">` (routes/__root.tsx) rather than reading a build manifest.
// Hence a library-style entry build (explicit `input` + pinned `entryFileNames`)
// rather than Vite's html-entry app build: there is no index.html for Vite to
// crawl — the HTML is server-rendered.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  // The shared styles package owns the brand assets (piggy-icon.svg); pointing
  // publicDir at it copies them verbatim and unhashed into build.outDir
  // (dist/client), which server.ts already serves at the root — so `/piggy-icon.svg`
  // resolves with no per-app copy and no new server code. This app has no
  // public/ of its own, so nothing is displaced.
  publicDir: brandAssetsDir,
  build: {
    outDir: "dist/client",
    emptyOutDir: true,
    rollupOptions: {
      input: fileURLToPath(new URL("src/client.tsx", import.meta.url)),
      output: {
        entryFileNames: "client.js",
        chunkFileNames: "[name].js",
        assetFileNames: "[name][extname]",
      },
    },
  },
});
