import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

import { wallowAppConfig } from "@bc-solutions-coder/config/vite/app";

/**
 * The one Vite config the app has: `vite dev` serves it and `vite build` emits
 * both environments plus the Nitro server bundle (`.output/server/index.mjs` +
 * `.output/public`). The separate client/SSR passes the deleted host runtime's
 * presets drove are gone, and so is `tsr generate` — the Start plugin owns route
 * codegen.
 *
 * `wallowAppConfig()` supplies the port, the SSR graph, the
 * `use-sync-external-store` aliases and the `copyPublicDir` restore. Two of
 * those this app did not have: it built two react-query graphs, so a `useQuery`
 * under SSR would have thrown "No QueryClient set" the moment the example grew a
 * query (Wallow-uc2c). Nothing was wrong with the app — the fix had simply been
 * discovered while debugging the other two, which is the argument for a preset
 * rather than three independent configs.
 *
 * This app is deliberately un-zoned, so it takes the Start plugin's defaults
 * rather than the `srcDirectory` / `importProtection` pairing the other two need.
 *
 * There is deliberately NO `vite: { installDevServerMiddleware }` key. The Start
 * plugin auto-detects a non-runnable SSR environment — which is exactly what
 * `nitro()` installs — and skips its dev middleware; forcing the option on makes
 * `vite dev` fail to boot.
 */
/** The port a bare `pnpm dev` lands on, matching the host this app used to run. */
const DEFAULT_PORT = 3010;

export default defineConfig({
  ...wallowAppConfig({ defaultPort: DEFAULT_PORT }),
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
