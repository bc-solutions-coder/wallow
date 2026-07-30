import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

import { resolveAlias } from "./aliases";
import { AUTH_BASE_PATH_ENV_KEY, normalizeBasePath, toViteBase } from "./src/shared/lib/base-path";

/**
 * The one Vite config wallow-auth has: `vite dev` serves it and `vite build`
 * emits both environments plus the Nitro server bundle (`.output/server/index.mjs`
 * + `.output/public`). The separate client/SSR passes the deleted shared
 * host-runtime presets drove are gone, and so is `tsr generate` — the Start
 * plugin owns route codegen.
 *
 * `vite dev` binds 3000 when `PORT` is unset, so this app's port is spelled out
 * here. Playwright waits on 3002 and does not inject `PORT` into the `pnpm dev`
 * child it boots, so without this the whole E2E suite times out on a port the app
 * never claimed.
 *
 * There is deliberately NO `vite: { installDevServerMiddleware }` key. The Start
 * plugin auto-detects a non-runnable SSR environment — which is exactly what the
 * `nitro` plugin installs — and skips its dev middleware; forcing the option on makes
 * `vite dev` fail to boot.
 */
/** The port a bare `pnpm dev` lands on — the port every fixture already expects. */
const DEFAULT_PORT = 3002;

/**
 * The URL prefix this build is served under, empty by default. It is a BUILD
 * input and not a runtime one because it is baked into every asset URL below, so
 * the Dockerfile takes it as an `ARG` promoted to an `ENV` before `pnpm build`.
 *
 * It has to be spelled in three places that each want a different shape: Vite's
 * own `base` (trailing slash), the Start plugin's `router.basepath` (no trailing
 * slash), and — the one that is easy to miss — nitro's `baseURL`. Vite's `base`
 * only rewrites the URLs written INTO the HTML; without nitro's `baseURL` the
 * server keeps serving `.output/public` at the root, so every prefixed script and
 * stylesheet 404s and the page renders but never hydrates.
 */
const BASE_PATH: string = normalizeBasePath(process.env[AUTH_BASE_PATH_ENV_KEY]);
const VITE_BASE: string = toViteBase(BASE_PATH);

export default defineConfig({
  base: VITE_BASE,
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
      // The three zone aliases, from the app-local map that `vitest.config.ts`
      // and `tsconfig.json` also mirror. Appended AFTER the shim regexes so the
      // anchored rewrites still match first.
      ...Object.entries(resolveAlias).map(([find, replacement]) => ({ find, replacement })),
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
      // The three-zone layout puts everything the host runtime owns under
      // `src/app/`: routes, router.tsx, start.ts, the generated route tree and
      // styles.css. `srcDirectory` is the ONE knob that relocates all of them —
      // the plugin resolves `router.routesDirectory`, `router.generatedRouteTree`
      // and every entry (router, start, client, server) RELATIVE to it. Setting
      // `routesDirectory: "src/app/routes"` instead would resolve to
      // `src/src/app/routes`, and the router entry — which is `required: true` —
      // would still not be found, so the build would hard-fail.
      srcDirectory: "src/app",

      // MANDATORY alongside the line above, not optional. With no `include`, the
      // import-protection plugin uses `srcDirectory` itself as the importer scope.
      // Narrowing srcDirectory to `src/app` would therefore silently stop
      // enforcing the server-only / client-bundle boundary for everything under
      // `src/features/**` and `src/shared/**`.
      importProtection: { include: ["src/**"] },

      router: {
        // The plugin derives this from `base` when it is left unset; spelled out
        // so the router's own basepath cannot silently drift from Vite's.
        basepath: BASE_PATH === "" ? undefined : BASE_PATH,
        // Specs are co-located with the code they cover, so a spec under
        // `src/app/routes/` would otherwise be codegen'd in as a route.
        routeFileIgnorePattern: String.raw`\.(test|spec)\.(ts|tsx)$`,
      },
    }),
    react(),
    nitro({ baseURL: VITE_BASE }),
    ...wallowStyles(),
  ],
});
