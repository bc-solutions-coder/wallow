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
`@bc-solutions-coder/ui`, `query` or `testing` would mean packing them too,
and the smoke stops being about the SDK surface.

## Why this directory has its own `.oxlintrc.json`

`@bc-solutions-coder/query` is private and unpublished, so the facade the root
config funnels every `@tanstack/react-query` import through is genuinely
unresolvable out here — `src/router.tsx` has to construct its `QueryClient` from
the real package. oxlint has no per-name partial disable, so the nested config
**re-declares** `no-restricted-imports` with the root's paths minus the
`@tanstack/react-query` entry, exactly as the facade's own exemption is written.
Switching the rule `"off"` instead would reopen all four bans and both pattern
groups for the whole template, silencing precisely the SDK-exports regressions
this app exists to catch.

Two things about that file are load-bearing. Its `extends` is what carries the
root's plugins and its pedantic/style categories down here; without it fork-smoke
silently drops to oxlint's defaults. And it must stay comment-free JSON — the
deleted-package deletion sweep in `packages/query/src/` exempts a lint config only
after `JSON.parse` proves every mention it makes is one of its own bans, and a
`//` comment makes that parse throw.
