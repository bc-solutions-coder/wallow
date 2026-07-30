import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

/**
 * The one Vite config wallow-web has: `vite dev` serves it and `vite build`
 * emits both environments plus the Nitro server bundle (`.output/server/index.mjs`
 * + `.output/public`). The separate client/SSR passes the deleted shared host
 * presets drove are gone, along with the standalone `server.ts`/`dev-server.ts`
 * hosts that consumed their output — and so is `tsr generate`, since the Start
 * plugin owns route codegen as a side effect of dev/build.
 *
 * `vite dev` binds 3000 when `PORT` is unset, which is also this app's port, but
 * it is spelled out anyway: Playwright waits on 3000 and does not inject `PORT`
 * into the `pnpm dev` child it boots, so the app's own default is what actually
 * gets claimed and it must not drift from `playwright.config.ts`.
 *
 * There is deliberately NO `vite: { installDevServerMiddleware }` key. The Start
 * plugin auto-detects a non-runnable SSR environment — which is exactly what
 * `nitro()` installs — and skips its dev middleware; forcing the option on makes
 * `vite dev` fail to boot.
 */
/** The port a bare `pnpm dev` lands on — the port every fixture already expects. */
const DEFAULT_PORT = 3000;

export default defineConfig({
  server: { port: Number(process.env.PORT ?? DEFAULT_PORT) },
  resolve: {
    alias: [
      // `use-sync-external-store/shim` back-ports React 18's `useSyncExternalStore`
      // to React 17. On React 19 it is dead weight — and worse than dead weight
      // here: it is CJS whose `require("react")` rolldown leaves as a RUNTIME
      // `__require`, so the built server loads a SECOND React out of node_modules
      // beside the bundled one. Every component that reads an external store then
      // throws "Invalid hook call" during SSR (Base UI's `useIsHydrating`, and
      // zustand, which this app's UI-only stores are built on) and the whole page
      // falls back to client-only rendering with an empty document. Pointing the
      // shim at React's own implementation is what it resolves to on React >= 18
      // anyway.
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
    // The zone aliases (`@app/*`, `@features/*`, `@shared/*`) come from
    // `tsconfig.json` `paths` — Vite 8 reads it natively, so tsconfig is the ONE
    // place a zone is declared and there is no second copy to drift. The anchored
    // regexes above stay in `alias` because `paths` cannot express a regex, and
    // `alias` is evaluated first either way.
    tsconfigPaths: true,
    // One React in the graph, from any resolution path.
    dedupe: ["react", "react-dom"],
  },
  ssr: {
    /*
     * One React Query in the SERVER graph. This is not `resolve.dedupe`'s job:
     * there is exactly one `@tanstack/react-query` on disk, and the two copies
     * that used to reach the bundle came from Vite's SSR externalization split,
     * not from resolution.
     *
     * Vite externalizes every dependency for SSR EXCEPT linked ones, which it
     * always bundles so HMR works. `@bc-solutions-coder/query` is a workspace
     * link, so react-query arrived through it BUNDLED; `@tanstack/react-router-
     * ssr-query` is an ordinary dependency, so it stayed external and Nitro
     * later bundled it with a SECOND react-query of its own. Each copy calls
     * `createContext`, and `setupRouterSsrQueryIntegration` installs the
     * provider from its copy while components read the facade's — so a
     * `useQuery` under SSR throws "No QueryClient set", React falls back past
     * the failed subtree, and the route ships as a shell that only fills in on
     * the client (Wallow-ka3m).
     *
     * wallow-web carried the same duplicated context as wallow-auth. It went
     * unnoticed because the routes that render under SSR here are public and
     * query-free, while wallow-auth's `/login` calls `useQuery` on the way in —
     * so this app was one SSR-rendered query away from the same silent failure.
     *
     * Naming the integration here puts it in the same bundled graph as the
     * facade, so both resolve to one module and one context. react-query is
     * named alongside it so any FUTURE external consumer joins that graph too
     * rather than quietly reintroducing the split.
     *
     * Nothing in the test suite catches this: vitest never builds the Nitro
     * bundle. Only a booted `.output/server/index.mjs` or an E2E run does.
     */
    noExternal: ["@tanstack/react-router-ssr-query", "@tanstack/react-query"],
  },
  environments: {
    // `nitro/vite` assumes it alone fills `.output/public` and forces the client
    // environment's `copyPublicDir` off. That silently drops the shared brand
    // assets `wallowStyles()` points `publicDir` at, so `/piggy-icon.svg` — the
    // favicon AND the `PublicLayout` navbar mark — 404s in the built output while
    // dev still serves it. Nitro sets the flag with `??=`, so spelling it out
    // here wins and the copy happens again.
    client: { build: { copyPublicDir: true } },
  },
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
