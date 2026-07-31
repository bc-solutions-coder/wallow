import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

import { wallowAppConfig } from "@bc-solutions-coder/config/vite/app";

/**
 * The one Vite config wallow-web has: `vite dev` serves it and `vite build`
 * emits both environments plus the Nitro server bundle (`.output/server/index.mjs`
 * + `.output/public`). The separate client/SSR passes the deleted shared host
 * presets drove are gone, along with the standalone `server.ts`/`dev-server.ts`
 * hosts that consumed their output — and so is `tsr generate`, since the Start
 * plugin owns route codegen as a side effect of dev/build.
 *
 * `wallowAppConfig()` supplies the half that is identical in all three apps and
 * that nothing in the test suite covers — the port, the SSR graph, the
 * `use-sync-external-store` aliases, the `copyPublicDir` restore. Read it before
 * changing any of them; each carries the blank-page symptom it prevents.
 *
 * There is deliberately NO `vite: { installDevServerMiddleware }` key. The Start
 * plugin auto-detects a non-runnable SSR environment — which is exactly what
 * `nitro()` installs — and skips its dev middleware; forcing the option on makes
 * `vite dev` fail to boot.
 */
/** The port a bare `pnpm dev` lands on — the port every fixture already expects. */
const DEFAULT_PORT = 3000;

export default defineConfig({
  ...wallowAppConfig({ defaultPort: DEFAULT_PORT }),
  plugins: [
    tanstackStart({
      // The three-zone layout puts everything the host runtime owns under
      // `src/app/`: routes, router.tsx, start.ts, the generated route tree and
      // styles.css. `srcDirectory` is the ONE knob that relocates all of them —
      // the plugin resolves `router.routesDirectory`, `router.generatedRouteTree`
      // and every entry (router, start, client, server) RELATIVE to it
      // (start-plugin-core schema.js:48-49, planning.js:54-95). Setting
      // `routesDirectory: "src/app/routes"` instead would resolve to
      // `src/src/app/routes`, and the router entry — which is `required: true` —
      // would still not be found, so the build would hard-fail.
      srcDirectory: "src/app",

      // MANDATORY alongside the line above, not optional. `include` is the
      // IMPORTER scope, and with no `include` the plugin falls back to
      // `srcDirectory` itself (import-protection/adapterUtils.js:23) — so
      // narrowing srcDirectory to `src/app` would silently stop checking every
      // importer under `src/features/**` and `src/shared/**`, which is exactly
      // where an accidental server-only import would come from.
      //
      // What the rule then denies is a matter of NAMING, not of this option.
      // Start's default client ruleset (import-protection/defaults.js) blocks
      // two things: the specifiers `@tanstack/{react,solid,vue}-start/server`,
      // and any imported file matching `**/*.server.*`. `redis`, `node:*` and
      // `@bc-solutions-coder/sdk/server` are NOT on that list — a client module
      // importing a plainly-named `lib/bff.ts` builds clean and ships redis in
      // the browser chunk (Wallow-v940, measured at 512 KB). The protection is
      // real only because every server-only module in this app is named
      // `*.server.*`; `src/server-only-naming.test.ts` is what keeps that true.
      importProtection: { include: ["src/**"] },

      // Specs are co-located with the code they cover, so a spec under
      // `src/app/routes/` would otherwise be codegen'd in as a route.
      router: { routeFileIgnorePattern: String.raw`\.(test|spec)\.(ts|tsx)$` },
    }),
    react(),
    nitro(),
    ...wallowStyles(),
  ],
});
