import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

import { normalizeBasePath, toViteBase } from "@bc-solutions-coder/env/base-path";

import { wallowAppConfig } from "@bc-solutions-coder/config/vite/app";
import { AUTH_BASE_PATH_ENV_KEY } from "./src/shared/lib/base-path";

/**
 * The one Vite config wallow-auth has: `vite dev` serves it and `vite build`
 * emits both environments plus the Nitro server bundle (`.output/server/index.mjs`
 * + `.output/public`). The separate client/SSR passes the deleted shared
 * host-runtime presets drove are gone, and so is `tsr generate` — the Start
 * plugin owns route codegen.
 *
 * `wallowAppConfig()` supplies the half that is identical in all three apps and
 * that nothing in the test suite covers — the port, the SSR graph, the
 * `use-sync-external-store` aliases, the `copyPublicDir` restore. Read it before
 * changing any of them; each carries the blank-page symptom it prevents, and
 * both of the first two were found on THIS app's `/login`.
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
 * stylesheet 404s and the page renders but never hydrates. This is why `base` is
 * NOT part of the shared preset: it is one value with three shapes, and only
 * this app has it.
 */
const BASE_PATH: string = normalizeBasePath(process.env[AUTH_BASE_PATH_ENV_KEY]);
const VITE_BASE: string = toViteBase(BASE_PATH);

export default defineConfig({
  ...wallowAppConfig({ defaultPort: DEFAULT_PORT }),
  base: VITE_BASE,
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

      // MANDATORY alongside the line above, not optional. `include` is the
      // IMPORTER scope, and with no `include` the plugin falls back to
      // `srcDirectory` itself — so narrowing srcDirectory to `src/app` would
      // silently stop checking every importer under `src/features/**` and
      // `src/shared/**`, which is exactly where an accidental server-only import
      // would come from.
      //
      // What the rule then denies is a matter of NAMING, not of this option.
      // Start's default client ruleset (import-protection/defaults.js) blocks
      // two things: the specifiers `@tanstack/{react,solid,vue}-start/server`,
      // and any imported file matching `**/*.server.*`. `node:*` and
      // `@bc-solutions-coder/sdk/server` are NOT on that list, so a plainly-named
      // module wrapping them builds clean and ships to the browser (Wallow-v940).
      // The protection is real only because every server-only module in this app
      // is named `*.server.*` — here `shared/lib/api-passthrough.server.ts` and
      // `log-ingest.server.ts`. Nothing asserts the
      // convention any more: `src/server-only-naming.test.ts` went with the rest of
      // the source-reading guards (Wallow-xg9t.1), so a plainly-named server module
      // ships to the browser and only the build's own failure would catch it.
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
