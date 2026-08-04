# packages/env — @bc-solutions-coder/env Agent Guide

The addressing a Start app derives from its **deployment**, not from its code: which origin the
browser reached it on, which origin it can reach itself on, and which URL prefix it is served
under. They feed the SDK's `baseUrl`/`internalOrigin`, and from there every generated query key.

Like `packages/utils` this sits at the bottom of the dependency graph — `dependencies` and
`peerDependencies` are both `{}`. Unlike `utils`, it takes web-standard `Request` objects, so
`lib` is the base config's (DOM included) rather than `["ESNext"]`.

## Entries

Subpath-only — deliberately **no `.` barrel**, so an import names the module it depends on.

| Entry               | What it holds                                                                                                                         |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| `./request-origin`  | `resolveRequestOrigin(request)` — the browser-facing origin, honouring `x-forwarded-proto`.                                           |
| `./internal-origin` | `resolveInternalOrigin(env, requestOrigin?)` + `INTERNAL_ORIGIN_ENV_KEY` (= `"WALLOW_WEB_INTERNAL_URL"`) — the self-reachable origin. |
| `./base-path`       | `normalizeBasePath`, `toViteBase`, `stripBasePath`, `withBasePath` — the URL-prefix string arithmetic.                                |

**`WALLOW_WEB_INTERNAL_URL` is not `WALLOW_API_INTERNAL_URL`.** The first is the app's **own**
origin, the one this package resolves; the second is the **upstream API** an app's passthrough
forwards to (`apps/minimal-app/src/lib/api-passthrough.ts`, both `playwright.config.ts` files) and
is nothing to do with `./internal-origin`. The names are one word apart and the failure modes are
not.

`./base-path` is loaded from `apps/wallow-auth/vite.config.ts` at **config load time**, as plain
Node ESM before any bundle exists. That is why it is its own subpath and why it must stay free of
validation and side effects: a module that threw on a missing RUNTIME variable would make
`vite build` fail, which is exactly backwards. `base-path.test.ts` pins it by re-importing the
module with `process.env` emptied.

## It reads no environment of its own

`resolveInternalOrigin` takes the env record as a **parameter**. That is the whole reason this
package can ship: every app's `start.ts` is aliased by TanStack Start into the **client** module
graph as well as the server one, so a module here that touched `process.env` at import time
would either break the client build or leak a server value into it. The single `process.env`
read stays at the call site, inside the server-only middleware callback:

```ts
internalOrigin: resolveInternalOrigin(process.env);
```

`src/charter.test.ts` used to assert this by reading every shipped module's source — no
`process.env`, no `import.meta.env`, no `import` statement at all — and it went with the rest of
the source-reading guards (`Wallow-xg9t.1`). Nothing enforces it now: the `types: []` compile
guard `utils` leans on cannot do the job here, because `lib` is not narrowed. **The constraint is
real even though it is unheld** — a `process.env` read at module scope here breaks a client build
or leaks a server value into one, so treat the rule above as load-bearing on review.

## What a bundler-substituted value cannot be

`AUTH_BASE_PATH` support is deliberately **split**: the string arithmetic is here, but the
constant naming _this build's_ prefix — `BASE_PATH`, plus the `toAppHref` that defaults to it —
stays in `apps/wallow-auth/src/shared/lib/base-path.ts`.

Vite replaces `import.meta.env.BASE_URL` with a **literal at build time**, and a library build has
no base. Measured: a module here reading `import.meta.env?.BASE_URL` compiles to `var PROBE = "/"`
in `dist/`. In-repo that never shows, because apps resolve this package from `src/` and the
substitution happens in the app's own build — so a `BASE_PATH` living here would look correct
right up until publish and then freeze every consumer's prefix to `/`. Anything a bundler
substitutes belongs in the bundle being built, not in a library.

## Why the helpers are shared rather than copied

`resolveRequestOrigin` was byte-identical in all three Start apps
(`wallow-web`, `wallow-auth`, `minimal-app`), and each had its own hand-rolled
`resolveInternalOrigin` beside it. Both values travel into the SDK's `baseUrl`, so a copy that
drifts costs an SSR cache hit on one app and not the others — the SSR pass builds an `http` key
the hydrating browser never matches.

`apps/wallow-web/src/shared-env.test.ts` was the cross-app guard — it reached into all three
apps' source to assert the local copies were gone and no module re-declared either resolver. It
is deleted (`Wallow-xg9t.1`). A re-declared resolver is a duplicate, not a break: it would still
compute the same origin, which is why the failure it watched for is drift over time rather than a
regression a spec catches on the day it lands. Import the subpath.

## `x-forwarded-proto` is validated, not trusted

The header is attacker-controllable. `resolveRequestOrigin` takes the first hop of a
comma-joined chain, strips a trailing colon, lower-cases it, and falls back to `url.origin`
unless the result is exactly `http` or `https`. It composes with `url.host`, never
`url.hostname` — dropping a non-default port would aim the SDK at `:80`.

## Boot-time validation lives elsewhere

This package validates nothing and throws nothing; it answers `undefined` when it cannot
resolve. The BFF's env contract — the one that fails a boot with every problem aggregated into
one error — is `loadBffConfigFromEnv` in `packages/sdk/src/server/config.ts`. Do not duplicate
it here: that would create an `sdk → env` edge and a second place for the contract to drift.

## Two tsconfigs, on purpose

`tsconfig.json` covers the shipped source with `types: []`. `tsconfig.node.json` covers the
specs and build configs, which read the manifest off disk and reach
`@bc-solutions-coder/config` (which imports `node:url`). `pnpm typecheck` runs **both**.

Adding a module is a new subpath: `src/<name>.ts` plus an `exports` entry, a
`publishConfig.exports` entry, a `vite.config.ts` lib entry and a `tsconfig.build.json`
include. All four, every time — the charter spec that used to diff them against `src/*.ts` went
with the rest of the source-reading guards (`Wallow-xg9t.1`), so nothing reminds you now, and a
missing lib entry only surfaces at `pnpm check:exports`.

Scripts: `pnpm --filter @bc-solutions-coder/env build` (Vite lib mode + `tsc -p
tsconfig.build.json`), `test`, `test:watch`, `typecheck`.
