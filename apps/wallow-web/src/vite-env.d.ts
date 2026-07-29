/**
 * Ambient typing for the Vite-only surfaces this app's tsconfig does not pull in:
 * it sets `types: ["node"]` without `vite/client`.
 *
 * `import.meta.env.SSR` is the boolean Vite statically replaces per build target
 * (`true` in the SSR bundle, `false` in the browser bundle); `configureClient()`
 * in `src/lib/wallow-sdk.ts` branches on it to pick the SSR vs browser client
 * configuration. Interface merging augments Node's `ImportMeta` (which supplies
 * `url`/`dirname`) rather than replacing it.
 *
 * `DEV` is gone from this declaration along with the `import.meta.env.DEV`
 * branches `__root.tsx` used to carry: Start's `<Scripts/>` and route manifest
 * now pick the client entry and stylesheet, so nothing branches on dev-vs-built
 * by hand.
 *
 * The `*.css` module declaration is what makes `__root.tsx`'s side-effect
 * stylesheet import resolve. It declares no exported shape on purpose — the
 * import exists to pull the Tailwind pipeline into the graph, and nothing reads
 * a value from it; the `?url` form is what ships a 404ing stylesheet under
 * Start's two-environment build.
 */

interface ImportMetaEnv {
  /** True when the module is running in Vite's SSR (server) build/runtime. */
  readonly SSR: boolean;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

declare module "*.css";
