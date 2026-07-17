import { fileURLToPath } from "node:url";

import { brandAssetsDir } from "@bc-solutions-coder/styles/assets";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Client bundle build for wallow-web (Wallow-ffpq.3.2).
//
// The dev server (dev-server.ts) does NOT use this file — it drives Vite in
// middlewareMode with its own inline plugins, serving the entry straight from
// its module graph. So this config only governs `vite build`.
//
// The previous config aimed `vite build` at the old BFF-demo entry
// (src/app.ts -> public/app.js). The hydration entry now exists (src/client.tsx,
// Wallow-ffpq.3.1) — it has to, since the app was inert without a client
// bundle — so the default build is the real client bundle and the SSR build
// moves to vite.ssr.config.ts. `pnpm build` runs both.
//
// Output contract: the emitted file must be `client.js` with a STABLE, unhashed
// name, because the document shell hardcodes `<script type="module"
// src="/client.js">` (routes/__root.tsx) rather than reading a build manifest.
// Hence a library-style entry build (explicit `input` + pinned `entryFileNames`)
// rather than Vite's html-entry app build: there is no index.html for Vite to
// crawl — the HTML is server-rendered.
//
// The shared styles package owns the brand assets (piggy-icon.svg); pointing
// publicDir at it copies them verbatim and unhashed into build.outDir
// (dist/client), which server.ts already serves at the root — so `/piggy-icon.svg`
// resolves with no per-app copy and no new server code (Wallow-ffpq.3.4). The
// tracked public/ here is the dead BFF-demo artefact (index.html + app.js), not
// a client static root, so nothing of value is displaced.
export default defineConfig({
  plugins: [react(), tailwindcss()],
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
