# packages/config — @bc-solutions-coder/config Agent Guide

Shared **build configuration**: the Vite presets every other workspace member builds with.

| Subpath          | Export                | Used by                                |
| ---------------- | --------------------- | -------------------------------------- |
| `./vite/library` | `defineLibraryConfig` | every `packages/*` library build       |
| `./vite/app`     | `wallowAppConfig`     | every `apps/*` TanStack Start frontend |

(`packages/config` and `packages/lint` themselves have no `build` script — there is no
library build for the preset to configure.)

## This package is never built and never published

It is the one workspace member with **no `build` script, no `dist/`, and no `publishConfig`**;
its `exports` map points at `src/` permanently. Do not "fix" that:

- Everything it exports is consumed by a `vite.config.ts`, evaluated by Vite's own
  TypeScript-aware config loader — nothing imports it at runtime.
- A build would be circular: each `packages/*` build runs `vite build` against a config that
  imports this package.

`tsc --noEmit` is the whole of its gate. `scripts/check-exports.sh` correctly omits it —
publint and attw describe a published tarball, and there is none.

## No barrel — one file per subpath, on purpose

There is deliberately no `src/vite/index.ts`. A consumer's Vite config resolves this package
as an ordinary external dependency, so **plain Node ESM** loads it, and Node rejects the
extensionless relative specifier (`./library`) that `moduleResolution: "Bundler"` lets the
rest of the workspace write — a barrel fails every `packages/*` build with
`ERR_MODULE_NOT_FOUND` while typechecking clean. Two subpaths pointing straight at two files
have no relative imports and cannot hit it.

The same constraint applies to anything added here: **no relative imports between modules in
this package** unless every consumer that reaches them runs `--configLoader runner`.

## Dependencies

`vite` only, and as a real `dependency` — the source imports `defineConfig` and `UserConfig`.

`react` appears as a **peerDependency**, only because of
`src/vite/use-sync-external-store-with-selector.mjs` — a vendored ESM replacement for the
CJS-only `use-sync-external-store/shim/with-selector` that the app preset aliases in (the why
lives in that file's header and the alias comment in `src/vite/app.ts`). The vendored module
is referenced by absolute file path as an alias replacement, never imported by this package's
own code, and react resolves in the consuming app's graph.

**Never add an app-shaped dependency here** (Start, nitro, plugin-react,
`@bc-solutions-coder/styles`) — this package exists so exactly-pinned versions are edited in
one place, and the styles case would introduce a workspace cycle (styles depends on this
package for its own build). See the note in `src/vite/app.ts` about why the app preset stops
short of the plugin array.
