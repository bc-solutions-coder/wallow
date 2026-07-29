import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

/**
 * The one Vite config wallow-auth has: `vite dev` serves it and `vite build`
 * emits both environments plus the Nitro server bundle (`.output/server/index.mjs`
 * + `.output/public`). The separate client/SSR passes the web-shell presets drove
 * are gone, and so is `tsr generate` — the Start plugin owns route codegen.
 *
 * `vite dev` binds 3000 when `PORT` is unset, so this app's port is spelled out
 * here. Playwright waits on 3002 and does not inject `PORT` into the `pnpm dev`
 * child it boots, so without this the whole E2E suite times out on a port the app
 * never claimed.
 *
 * There is deliberately NO `vite: { installDevServerMiddleware }` key. The Start
 * plugin auto-detects a non-runnable SSR environment — which is exactly what
 * `nitro()` installs — and skips its dev middleware; forcing the option on makes
 * `vite dev` fail to boot.
 */
/** The port a bare `pnpm dev` lands on — the port every fixture already expects. */
const DEFAULT_PORT = 3002;

export default defineConfig({
  server: { port: Number(process.env.PORT ?? DEFAULT_PORT) },
  resolve: {
    alias: [
      // `use-sync-external-store/shim` back-ports React 18's `useSyncExternalStore`
      // to React 17. On React 19 it is dead weight — and worse than dead weight
      // here: it is CJS whose `require("react")` rolldown leaves as a RUNTIME
      // `__require`, so the built server loads a SECOND React out of node_modules
      // beside the bundled one. Every Base UI component that reads a store
      // (`useIsHydrating`, so Tabs/Checkbox/Select — most of the login screen)
      // then throws "Invalid hook call" during SSR and the whole page falls back
      // to client-only rendering with an empty document. Pointing the shim at
      // React's own implementation is what it resolves to on React >= 18 anyway.
      //
      // Anchored regexes, not a bare string: a string alias matches by PREFIX, so
      // `use-sync-external-store/shim` would also swallow
      // `use-sync-external-store/shim/with-selector` and rewrite it to the
      // nonexistent `react/with-selector`. That subpath keeps its own
      // implementation (React ships no `useSyncExternalStoreWithSelector`) and
      // reads `useSyncExternalStore` off whatever this alias resolves to.
      { find: /^use-sync-external-store\/shim$/u, replacement: "react" },
      { find: /^use-sync-external-store\/shim\/index\.js$/u, replacement: "react" },
    ],
    // One React in the graph, from any resolution path.
    dedupe: ["react", "react-dom"],
  },
  environments: {
    // `nitro/vite` assumes it alone fills `.output/public` and forces the client
    // environment's `copyPublicDir` off. That silently drops the shared brand
    // assets `wallowStyles()` points `publicDir` at, so `/piggy-icon.svg` — the
    // favicon AND `AuthLayout`'s fork attribution image — 404s in the built
    // output while dev still serves it. Nitro sets the flag with `??=`, so
    // spelling it out here wins and the copy happens again.
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
