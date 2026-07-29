import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

/**
 * The one Vite config the app has: `vite dev` serves it and `vite build` emits
 * both environments plus the Nitro server bundle (`.output/server/index.mjs` +
 * `.output/public`). The separate client/SSR passes the web-shell presets drove
 * are gone, and so is `tsr generate` — the Start plugin owns route codegen.
 *
 * `vite dev` binds 3000 when `PORT` is unset, so this app's port is spelled out
 * here: a bare `pnpm dev` must land on 3010 like the deleted host did.
 *
 * There is deliberately NO `vite: { installDevServerMiddleware }` key. The Start
 * plugin auto-detects a non-runnable SSR environment — which is exactly what
 * `nitro()` installs — and skips its dev middleware; forcing the option on makes
 * `vite dev` fail to boot.
 */
/** The port a bare `pnpm dev` lands on, matching the host this app used to run. */
const DEFAULT_PORT = 3010;

export default defineConfig({
  server: { port: Number(process.env.PORT ?? DEFAULT_PORT) },
  environments: {
    // `nitro/vite` assumes it alone fills `.output/public` and forces the client
    // environment's `copyPublicDir` off. That silently drops the shared brand
    // assets `wallowStyles()` points `publicDir` at, so `/piggy-icon.svg` — the
    // favicon AND the attribution image — 404s in the built output while dev
    // still serves it. Nitro sets the flag with `??=`, so spelling it out here
    // wins and the copy happens again.
    client: { build: { copyPublicDir: true } },
  },
  plugins: [
    tanstackStart({
      // Specs are co-located with the code they cover, so a spec under
      // `src/routes/` would otherwise be codegen'd in as a route.
      router: { routeFileIgnorePattern: String.raw`\.(test|spec)\.(ts|tsx)$` },
    }),
    react(),
    nitro(),
    ...wallowStyles(),
  ],
});
