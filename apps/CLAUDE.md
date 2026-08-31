# apps — Frontend Applications Agent Guide

Every app is a **TanStack Start** frontend consuming the `@bc-solutions-coder` workspace
packages via `workspace:*`. `forms`, `auth`, `navigation`, `logger` and `utils` are optional.
**`minimal-app` is the external relying-party example**: it depends on the published `sdk`
alone (plus `redis` for the session store) — deliberately no `ui`, `styles`, `query`, `auth`,
`env` or `testing`, which an external consumer cannot install. `config` is a build-time-only
devDependency supplying `wallowAppConfig()` to `vite.config.ts`, never imported by app code.

**No package build is needed before touching an app.** In-repo every `@bc-solutions-coder/*`
exports map resolves to that package's `src/`; `dist/` is a publish artifact swapped in at pack
time by `publishConfig.exports`. Only `pnpm check:exports` needs built packages.

Each app's `start` script (`node .output/server/index.mjs`) is what the Dockerfiles and E2E
containers run. All three apps' vite/vitest scripts pass `--configLoader runner` — the configs
import workspace ESM before any bundle exists, which the default config loader cannot serve;
copying a script without the flag is how the config stops loading.

## Hosting and structure

- **Hosting is per-app and owned by Start.** One `vite.config.ts` per app (`tanstackStart` +
  `react` + `nitro`, plus `wallowStyles` in the zoned apps; minimal-app styles itself), no host
  files (no `server.ts`, `dev-server.ts`, `vite.ssr.config.ts`). Backend-facing surface =
  **server routes** delegating to an SDK preset (`createApiPassthrough` for wallow-auth,
  `createWallowBffServer` for wallow-web and minimal-app — minimal-app also mounts
  `createServiceClient()` behind its anonymous `POST /contact`).
  The generated route tree regenerates as a side effect of `vite dev`/`vite build` — never
  hand-edit it; no `routes:generate` script or `tsr.config.json`. The zoned apps use
  `src/app/routes/**` + `src/app/routeTree.gen.ts` (`srcDirectory: "src/app"`); minimal-app is
  flat (`src/routes/**`, `src/routeTree.gen.ts`, no `app/`).
- **`wallow-web` and `wallow-auth` are zoned; `minimal-app` is deliberately not.** Zoned `src/`
  is `app/` (routes, router, entries, server-only modules), `features/<name>/` (one directory
  per screen, reachable only through its `index.ts` barrel) and `shared/`. Cross-zone imports
  use aliases — `@app/*`, `@features/<name>`, `@shared/*` — declared **once** in the app's
  `tsconfig.json` `paths`; Vite (`resolve.tsconfigPaths: true`), vitest, and `wallow/zone-dag`
  all read that map, so adding a zone is one edit.
- **Server-only modules live in `app/` and are named `*.server.*`.** `srcDirectory: "src/app"`
  must be paired with `importProtection: { include: ["src/**"] }`, or Start scopes its
  env-boundary check to `src/app` alone and silently stops checking `features/` and `shared/`.
  That pairing sets only the importer scope; denial is by **filename** — Start's client rule
  blocks imports of `**/*.server.*` files and knows nothing about `redis` or `node:crypto`. A
  plainly-named server wrapper builds clean and ships to the browser.
- **A hook lives in `features/<name>/hooks/` until a second feature needs it, then
  `shared/hooks/`** — `wallow/zone-dag` forbids feature-to-feature imports, so `shared/` is the
  only zone both may import. The extraction test is the SCREEN's, not the hook's: when a
  component holds query wiring, derived narrowing and view state at once, the state moves out
  and the component keeps the markup.
- **Two version catalogs — do not collapse them**: `@tanstack/react-start`/`react-router`/
  `react-router-ssr-query` pin exactly via the `start` catalog in `pnpm-workspace.yaml`; ranged
  shared deps (`react`, `react-dom`, `@tanstack/react-form`/`react-query`, `zustand`) come from
  the `react` catalog. A library peering a range against an app pinning exactly is correct.
- No app spells out `server.port` — each passes `defaultPort` to `wallowAppConfig()`, and
  `packages/config` owns `server.port` (reads `process.env.PORT`).

## Component catalog and lint

Zoned-app surfaces are built from `@bc-solutions-coder/ui`, lint-enforced: wallow-web's and
wallow-auth's `.oxlintrc.json` forbid raw text elements (`p`, `span`, `h1`–`h6`, …) in favour
of `Text`/`PageHeader` and
enable the relevant `wallow/*` rules; minimal-app renders raw elements by design (no `ui`) and
enables only `wallow/no-source-tests`. Which config enables which rule, per-app divergences, and
override ordering live in `packages/lint/CLAUDE.md` — read it before editing any
`.oxlintrc.json`. Do not reintroduce a disk-sweeping guard spec for something a lint rule can say.

- Card headings are `<Text as="h2" variant="subheading">`; assert the computed size, never class
  strings — `cn()` merges a caller's `className` over the recipe.
- **wallow-auth's data boundary is lint's too**: under `src/features/**` and `src/app/routes/**`
  a screen reaches the API only through its feature's `api.ts` seam, and
  `wallow/no-hand-rolled-mutation` bans any inline `mutationFn` in the two zoned apps
  (`packages/lint/CLAUDE.md`).

## Cross-cutting patterns

- **A per-deployment value reaches the browser through the document, not router context.**
  `WALLOW_REPOSITORY_URL`/`WALLOW_DOCS_URL` are read once per request in `src/app/start.ts`,
  stated in `<head>` as an inline script, and read back by `src/shared/lib/fork-links.ts`'s
  plain `forkLinks()` accessor (falls back to `branding.json`). Router context is NOT the
  channel — it is rebuilt from nothing on the client, a hydration mismatch.
- **The theme class belongs on `document.documentElement`.** Each `__root.tsx` stamps
  `className={branding.defaultMode}` on `<html>`; a `<div className="dark">` wrapper renders
  the LIGHT palette. See `docs/development/frontend-setup.md#dark-mode`.
- **Logging**: both zoned apps use `@bc-solutions-coder/logger`, never `console` — one browser
  singleton at `src/shared/lib/log.ts` posting to a same-origin ingest route (`/bff/logs` in
  wallow-web, CSRF-gated; `/logs` in wallow-auth, guarded by a per-request origin allowlist).
  Events are NAMES (`bff.logout.failed`), not prose. Guides: `docs/development/logging.md`,
  `packages/logger/CLAUDE.md`.
- **A `.tsx` spec that never mounts a DOM** (renders via `react-dom/server`, or asserts a
  `beforeLoad` redirect) is named **`*.ssr.test.tsx`** — the name routes it onto the node
  vitest project; there is no per-app list.
- wallow-web's `test:e2e:cross-app` script runs its `e2e-cross-app/` suite.
- All three apps ship a `Dockerfile` whose build context is the **repo
  root** — the whole workspace is needed to resolve `workspace:*`.

## Frontend state boundary

TanStack Query is the only store for backend data; every key comes from the generated
`{operation}Options()` / `{operation}Mutation()` / `{operation}QueryKey()` artifacts in
`@bc-solutions-coder/sdk/query` — never inline key literals or hand-rolled factories. Keys are
flat with no prefix to sweep by, so invalidation goes through the curated `invalidations`
predicates (`queriesWithTag`, `queriesForOperation`) from the same entry. Import every
react-query symbol from `@bc-solutions-coder/query`, never `@tanstack/react-query`
(lint-enforced facade). Auth state comes from `@bc-solutions-coder/auth`
(`currentUserQuery`/`useCurrentUser`, `ensureCurrentUser`, `hasRole`/`hasPermission`/`isAdmin`),
never a per-app copy. Zustand holds UI-only global state, never API data.
See `docs/development/frontend-state.md`.
