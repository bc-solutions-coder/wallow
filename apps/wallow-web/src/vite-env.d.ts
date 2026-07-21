/**
 * Ambient Vite env typing (Wallow-cqoa).
 *
 * The app's tsconfig sets `types: ["node"]` and does not pull in `vite/client`,
 * so `import.meta.env` is otherwise untyped. `getWallowSdk()` branches on
 * `import.meta.env.SSR` — the boolean Vite statically replaces per build target
 * (`true` in the SSR bundle, `false` in the browser bundle) — so we declare just
 * that flag here. Interface merging augments Node's `ImportMeta` (which supplies
 * `url`/`dirname`) rather than replacing it.
 */

interface ImportMetaEnv {
  /** True when the module is running in Vite's SSR (server) build/runtime. */
  readonly SSR: boolean;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
