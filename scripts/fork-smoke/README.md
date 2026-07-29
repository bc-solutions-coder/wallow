# fork-smoke scaffold

The scratch TanStack Start app `../fork-smoke.sh` builds. It is a **template**,
never built where it sits: the script copies it outside the repo, drops the
packed `@bc-solutions-coder/sdk` and `@bc-solutions-coder/styles` tarballs into
`vendor/`, and installs from there — so the app consumes the packages the way a
fork consumes the published ones, not through a workspace symlink.

Run it with `./scripts/fork-smoke.sh`; the script's header comment explains the
steps and the env knobs. CI runs the same script in the `fork-smoke` job of
`.github/workflows/ci.yml`.

Every route here earns its place by pulling in one export subpath the tarballs
declare, so a broken `exports` map fails the build:

| File                    | Subpath under test                            |
| ----------------------- | --------------------------------------------- |
| `src/start.ts`          | `@bc-solutions-coder/sdk`                     |
| `src/routes/index.tsx`  | `.../sdk/query`                               |
| `src/routes/health.ts`  | `.../sdk/server`                              |
| `src/routes/v1/$.ts`    | `.../sdk/server/passthrough`                  |
| `src/routes/__root.tsx` | `@bc-solutions-coder/styles`                  |
| `vite.config.ts`        | `.../styles/vite` (and `./assets` through it) |
| `src/styles.css`        | `.../styles/styles.css`                       |

Keep this app dependent on those two packages only. Reaching for
`@bc-solutions-coder/ui`, `web-shell` or `testing` would mean packing them too,
and the smoke stops being about the SDK surface.
