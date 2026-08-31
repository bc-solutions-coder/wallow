import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

import { wallowAppConfig } from "@bc-solutions-coder/config/vite/app";

/**
 * The one Vite config the app has: `vite dev` serves it and `vite build` emits
 * both environments plus the Nitro server bundle (`.output/server/index.mjs` +
 * `.output/public`).
 *
 * `wallowAppConfig()` supplies the port, the SSR graph, the
 * `use-sync-external-store` aliases and the `copyPublicDir` restore. No
 * `wallowStyles()` — this app styles itself with a standalone `src/styles.css`,
 * the way an external consumer without the private packages would.
 *
 * This app is deliberately un-zoned, so it takes the Start plugin's defaults
 * rather than the `srcDirectory` / `importProtection` pairing the other two
 * need.
 *
 * There is deliberately NO `vite: { installDevServerMiddleware }` key. The
 * Start plugin auto-detects a non-runnable SSR environment — which is exactly
 * what `nitro()` installs — and skips its dev middleware; forcing the option on
 * makes `vite dev` fail to boot.
 */
/** The port a bare `pnpm dev` lands on. */
const DEFAULT_PORT = 3010;

export default defineConfig({
  ...wallowAppConfig({ defaultPort: DEFAULT_PORT }),
  plugins: [tanstackStart(), react(), nitro()],
});
