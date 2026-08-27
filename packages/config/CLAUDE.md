# packages/config — @bc-solutions-coder/config Agent Guide

Shared **build configuration**: the Vite presets every other workspace member builds with.

| Subpath          | Export                | Used by                                     |
| ---------------- | --------------------- | ------------------------------------------- |
| `./vite/library` | `defineLibraryConfig` | all eleven `packages/*` library builds      |
| `./vite/app`     | `wallowAppConfig`     | all three `apps/*` TanStack Start frontends |

Eleven, not thirteen, because `packages/config` and `packages/lint` are the two members with no
`build` script at all — there is no library build for the preset to configure. Everything else
(`auth`, `env`, `forms`, `logger`, `navigation`, `query`, `sdk`, `styles`, `testing`, `ui`,
`utils`) calls `defineLibraryConfig` from its `vite.config.ts`.

## This package is never built and never published

It is the one workspace member with **no `build` script, no `dist/`, and no `publishConfig`**.
Its `exports` map points at `src/` permanently, rather than as the in-repo half of the
publish-time swap every other package does. Do not "fix" that:

- Everything it exports is consumed by a `vite.config.ts`, evaluated by Vite's own
  TypeScript-aware config loader. Nothing imports it at runtime, so there is nothing for a
  bundle to contain.
- A build would be circular in practice. Each `packages/*` build runs `vite build` against a
  config that imports this package — so if it had to be built first, the thing that builds
  everything would be the one thing nothing could build.

`tsc --noEmit` is the whole of its gate. `scripts/check-exports.sh` names its packages
explicitly and does not include this one, which is correct: publint and attw describe a
published tarball, and there is none.

## No barrel — one file per subpath, on purpose

There is deliberately no `src/vite/index.ts` re-exporting the two modules. A consumer's Vite
config resolves this package as an ordinary external dependency, so **plain Node ESM** loads
it, and Node rejects the extensionless relative specifier (`./library`) that
`moduleResolution: "Bundler"` lets the rest of the workspace write. A barrel therefore fails
every `packages/*` build with `ERR_MODULE_NOT_FOUND` while typechecking clean. Two subpaths
pointing straight at two files have no relative imports at all and cannot hit it.

The same constraint applies to anything added here: **no relative imports between modules in
this package** unless every consumer that reaches them runs `--configLoader runner`.

## Dependencies

`vite` only, and it is a real `dependency` rather than a devDependency — the source imports
`defineConfig` and `UserConfig` from it.

`react` appears as a **peerDependency**, not a dependency, and only because of
`src/vite/use-sync-external-store-with-selector.mjs` — a vendored ESM replacement for the
CJS-only `use-sync-external-store/shim/with-selector` that the app preset aliases in
(Wallow-luni; the why lives in that file's header and the alias comment in `src/vite/app.ts`).
The vendored module is never imported by this package's own code — `app.ts` references it by
absolute file path as an alias replacement, so the no-relative-imports constraint above does
not apply to it, and react resolves in the consuming app's graph, never here.

Keep it that way. The presets were briefly kept under `tools/`, which has no manifest and so
resolves upward into the ROOT `node_modules` — meaning every import had to be added to the
root `package.json` to resolve at all, including exactly-pinned ones like
`@tanstack/react-start` whose whole point is that the version is edited in one place. That is
the reason this is a package. Adding an app-shaped dependency here (Start, nitro,
plugin-react, `@bc-solutions-coder/styles`) would undo it and, in the styles case, introduce a
workspace cycle — styles depends on this package for its own build. See the note in
`src/vite/app.ts` about why the app preset stops short of the plugin array.
