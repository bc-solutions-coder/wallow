/**
 * Ambient Vite env typing. The app's tsconfig sets `types: ["node"]` and does
 * not pull in `vite/client`, so `import.meta.env` is otherwise untyped.
 * `__root.tsx` branches on `import.meta.env.DEV` (the boolean Vite statically
 * replaces per build target) to pick the client entry and stylesheet paths, so
 * we declare just that flag here. Interface merging augments Node's `ImportMeta`
 * (which supplies `url`/`dirname`) rather than replacing it.
 */

interface ImportMetaEnv {
  /** True on Vite's dev server, false in a production build. */
  readonly DEV: boolean;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
