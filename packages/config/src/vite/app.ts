import { fileURLToPath } from "node:url";
import type { UserConfig } from "vite";

/**
 * Absolute path to the vendored ESM `with-selector` (see that file's header
 * and the alias comment below). Resolved from this module's own URL so it
 * works wherever the consumer's `vite.config.ts` lives.
 */
const WITH_SELECTOR_ESM: string = fileURLToPath(
  new URL("use-sync-external-store-with-selector.mjs", import.meta.url),
);

/**
 * The non-plugin half of every TanStack Start app's Vite config — ports,
 * resolution, the SSR graph, and the public-dir restore.
 *
 * Deliberately NOT the plugin array. Building `[tanstackStart(), react(),
 * nitro(), ...wallowStyles()]` here would give this package an app-shaped
 * dependency graph — including `@bc-solutions-coder/styles`, which depends on
 * this package for its own build, so the workspace graph would gain a cycle to
 * hoist four lines nobody has trouble reading. Each app keeps its own visible
 * `plugins` array; what lives here is the part that is byte-identical and, more
 * to the point, the part nobody can be expected to rediscover.
 *
 * That last bit is the reason this file exists at all. Every invariant below was
 * found by debugging a blank page in a booted `.output/server/index.mjs`:
 * vitest never builds the Nitro bundle, so none of them has a spec, and a
 * regression surfaces as an empty document rather than a red test.
 * `apps/minimal-app` was missing two of them purely because they had
 * been discovered while working on the other two apps (Wallow-uc2c).
 *
 * Not here, and not shared, on purpose:
 *
 *  - **`plugins`** — see above.
 *  - **`base`** — only wallow-auth serves under a prefix, and it has to spell
 *    the value in three shapes (Vite's `base`, the Start router's `basepath`,
 *    nitro's `baseURL`), which is app knowledge.
 *  - **`resolve.dedupe: ["react", "react-dom"]`** — REMOVED, not relocated. It
 *    read as the fix for the two duplication problems below and is neither: the
 *    React copy comes from a CJS `__require` the bundler leaves behind, and the
 *    react-query copy from Vite's SSR externalization split. Dropping it
 *    produces byte-identical output.
 */
export interface AppConfigOptions {
  /**
   * The port a bare `pnpm dev` lands on when `PORT` is unset.
   *
   * Vite's own default is 3000 whatever the app, so every app passes this even
   * when 3000 is what it wants: Playwright waits on the app's assigned port and
   * does not inject `PORT` into the `pnpm dev` child it boots, so the app's own
   * default is what actually gets claimed and it must not drift from
   * `playwright.config.ts`.
   */
  readonly defaultPort: number;
}

/** Build the shared, plugin-free half of one app's Vite config. */
export function wallowAppConfig(options: AppConfigOptions): UserConfig {
  return {
    server: { port: Number(process.env.PORT ?? options.defaultPort) },
    resolve: {
      alias: [
        // `use-sync-external-store/shim` back-ports React 18's
        // `useSyncExternalStore` to React 17. On React 19 it is dead weight —
        // and worse than dead weight here: it is CJS whose `require("react")`
        // Rolldown leaves as a RUNTIME `__require`, so the built server loads a
        // SECOND React out of node_modules beside the bundled one. Every
        // component that reads an external store then throws "Invalid hook
        // call" during SSR — Base UI's `useIsHydrating`, so Tabs/Checkbox/Select
        // and most of the login screen, plus zustand, which the UI-only stores
        // are built on — and the whole page falls back to client-only rendering
        // with an empty document. Measured on wallow-auth: `/login` collapses
        // from 9895 to 2621 body chars with the alias removed. Pointing the shim
        // at React's own implementation is what it resolves to on React >= 18
        // anyway.
        //
        // Anchored regexes, not a bare string: a string alias matches by PREFIX,
        // so `use-sync-external-store/shim` would also swallow
        // `use-sync-external-store/shim/with-selector` and rewrite it to the
        // nonexistent `react/with-selector` (React ships no
        // `useSyncExternalStoreWithSelector`).
        { find: /^use-sync-external-store\/shim$/u, replacement: "react" },
        { find: /^use-sync-external-store\/shim\/index\.js$/u, replacement: "react" },
        // The `/with-selector` subpath cannot alias to react (see above) but
        // cannot be left alone either (Wallow-luni): the npm package is
        // CJS-only, and when Vite bundles it into a zoned app's SSR chunk with
        // react external, its `require("react")` degrades to a runtime
        // `createRequire` — a second React out of node_modules at SSR runtime.
        // So it aliases to a vendored ESM copy whose `import "react"` Nitro
        // rewrites to the bundled `require_react()`, the linkage minimal-app
        // proves correct. Both specifier spellings exist in the wild (zustand
        // writes the `.js` form), hence two anchors. NOT fixable via
        // `ssr.external` (ignored outright in this environment) or
        // `ssr.noExternal: ["react", "react-dom"]` (boot-tested: puts a second
        // LIVE React in the Vite graph while react-dom's renderer stays in
        // Nitro's — /login 500s on a null hooks dispatcher). See the bead.
        {
          find: /^use-sync-external-store\/shim\/with-selector(?:\.js)?$/u,
          replacement: WITH_SELECTOR_ESM,
        },
      ],
      // The zone aliases (`@app/*`, `@features/*`, `@shared/*`) come from each
      // app's own `tsconfig.json` `paths` — Vite 8 reads it natively, so tsconfig
      // is the ONE place a zone is declared and there is no second copy to
      // drift. The anchored regexes above stay in `alias` because `paths` cannot
      // express a regex, and `alias` is evaluated first either way.
      tsconfigPaths: true,
    },
    ssr: {
      /*
       * One React Query in the SERVER graph. This is not `resolve.dedupe`'s job:
       * there is exactly one `@tanstack/react-query` on disk, and the two copies
       * that used to reach the bundle came from Vite's SSR externalization
       * split, not from resolution.
       *
       * Vite externalizes every dependency for SSR EXCEPT linked ones, which it
       * always bundles so HMR works. `@bc-solutions-coder/query` is a workspace
       * link, so react-query arrived through it BUNDLED;
       * `@tanstack/react-router-ssr-query` is an ordinary dependency, so it
       * stayed external and Nitro later bundled it with a SECOND react-query of
       * its own. Each copy calls `createContext`, and
       * `setupRouterSsrQueryIntegration` installs the provider from its copy
       * while components read the facade's — so a `useQuery` under SSR throws
       * "No QueryClient set", React silently falls back past the failed subtree,
       * and the route ships as a shell that only fills in on the client
       * (Wallow-ka3m).
       *
       * Naming the integration here puts it in the same bundled graph as the
       * facade, so both resolve to one module and one context. react-query is
       * named alongside it so any FUTURE external consumer joins that graph too
       * rather than quietly reintroducing the split.
       *
       * `ssr.external: ["@bc-solutions-coder/query"]` alone also collapses it to
       * one graph with an identical render, but it costs facade HMR in dev.
       *
       * This is the half that gets MORE necessary as more of the workspace
       * resolves from source, not less.
       */
      noExternal: ["@tanstack/react-router-ssr-query", "@tanstack/react-query"],
    },
    environments: {
      // `nitro/vite` assumes it alone fills `.output/public` and forces the
      // client environment's `copyPublicDir` off. That silently drops the shared
      // brand assets `wallowStyles()` points `publicDir` at, so
      // `/piggy-icon.svg` — the favicon AND the layouts' fork attribution mark —
      // 404s in the built output while dev still serves it. Nitro sets the flag
      // with `??=`, so spelling it out here wins and the copy happens again.
      client: { build: { copyPublicDir: true } },
    },
  };
}
