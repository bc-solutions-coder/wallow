/**
 * Vite config-preset factories shared by both apps' `vite.config.ts` (client
 * bundle) and `vite.ssr.config.ts` (SSR bundle) (Wallow-0q2s.8.5). Extracted from
 * the near-identical apps/{wallow-auth,wallow-web}/vite.config.ts and
 * vite.ssr.config.ts, which are byte-identical modulo comment text once W2's
 * Tailwind extraction collapsed the `wallowStyles()`/publicDir wiring.
 */
import { join } from "node:path";

import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackRouter } from "@tanstack/router-plugin/vite";
import react from "@vitejs/plugin-react";
import { type UserConfig } from "vite";

/**
 * The per-app seam over the Vite config presets. The only real difference between
 * the apps is their filesystem root — everything else (output naming, plugin set,
 * outDir) is shared — so the sole knob is the app's absolute directory, against
 * which the client entry (`src/client.tsx`) and SSR entry (`src/ssr.tsx`) resolve.
 */
export interface ViteConfigOptions {
  /** Absolute Vite root of the app (its `import.meta.dirname`). */
  appDir: string;
}

/**
 * Client (browser hydration) bundle config. Emits an unhashed `client.js` into
 * `dist/client` — the stable output contract the document shell and the
 * standalone host depend on (`__root.tsx` hardcodes `<script src="/client.js">`,
 * not a build manifest) — with `wallowStyles()` composed in for Tailwind + brand
 * assets. A library-style entry build (explicit `input` + pinned output names)
 * because there is no `index.html` for Vite to crawl: the HTML is server-rendered.
 */
export function createClientViteConfig(options: ViteConfigOptions): UserConfig {
  return {
    plugins: [
      tanstackRouter({
        target: "react",
        routesDirectory: join(options.appDir, "src", "routes"),
        generatedRouteTree: join(options.appDir, "src", "routeTree.gen.ts"),
        autoCodeSplitting: false,
      }),
      react(),
      ...wallowStyles(),
    ],
    build: {
      outDir: "dist/client",
      emptyOutDir: true,
      rollupOptions: {
        input: join(options.appDir, "src", "client.tsx"),
        output: {
          entryFileNames: "client.js",
          chunkFileNames: "[name].js",
          assetFileNames: "[name][extname]",
        },
      },
    },
  };
}

/**
 * Server (SSR) bundle config. Bundles `src/ssr.tsx` into `dist/server`; the
 * standalone host serves the emitted `dist/server/ssr.js`. React-only — the SSR
 * bundle does not need the Tailwind/brand-assets plugins the client build carries.
 */
export function createSsrViteConfig(options: ViteConfigOptions): UserConfig {
  return {
    plugins: [react()],
    build: {
      ssr: join(options.appDir, "src", "ssr.tsx"),
      outDir: "dist/server",
      emptyOutDir: true,
    },
  };
}
