# packages/config — @bc-solutions-coder/config Agent Guide

Shared build configuration: `./vite/library` (`defineLibraryConfig`) for every `packages/*`
library build, `./vite/app` (`wallowAppConfig`) for every `apps/*` Start frontend.

## Never built, never published — do not "fix" that

- Everything here is consumed only by a `vite.config.ts`, evaluated by Vite's own
  TypeScript-aware config loader; nothing imports it at runtime.
- A build would be circular: every `packages/*` build runs `vite build` against a config
  that imports this package.
- `tsc --noEmit` is the whole gate. `scripts/check-exports.sh` correctly omits it — publint
  and attw describe a published tarball, and there is none.

## No barrel, no relative imports between modules

A consumer's Vite config loads this package as **plain Node ESM**, which rejects the
extensionless relative specifiers `moduleResolution: "Bundler"` lets the rest of the
workspace write — a barrel (or any relative import between modules here) typechecks clean
and then fails every `packages/*` build with `ERR_MODULE_NOT_FOUND`. Subpaths point straight
at files, on purpose.

## Dependencies

- `react` is a peerDependency ONLY for the vendored
  `src/vite/use-sync-external-store-with-selector.mjs` alias — the why lives in that file's
  header and the alias comment in `src/vite/app.ts`.
- **Never add an app-shaped dependency** (Start, nitro, plugin-react,
  `@bc-solutions-coder/styles` — styles would introduce a workspace cycle: it depends on
  this package for its own build). See the note in `src/vite/app.ts` about why the app
  preset stops short of the plugin array.
