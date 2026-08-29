# apps — Frontend Applications Agent Guide

Every app is a **TanStack Start** frontend consuming the `@bc-solutions-coder` workspace
packages via `workspace:*`. `forms`, `auth`, `navigation`, `logger` and `utils` are optional —
`minimal-app` omits all five. `config` is a build-time-only devDependency supplying
`wallowAppConfig()` to `vite.config.ts`, never imported by app code.

| App            | Port | What it is                                                               |
| -------------- | ---- | ------------------------------------------------------------------------ |
| `wallow-web/`  | 3000 | Reference dashboard demonstrating the full same-origin BFF OIDC flow.    |
| `wallow-auth/` | 3002 | Auth frontend — login / signup / MFA screens.                            |
| `minimal-app/` | 3010 | Smallest app wiring the core shared packages into a TanStack Start host. |

**No package build is needed before touching an app.** In-repo every `@bc-solutions-coder/*`
exports map resolves to that package's `src/`; `dist/` is a publish artifact swapped in at pack
time by `publishConfig.exports`. Only `pnpm check:exports` needs built packages, which is why
`pnpm build` precedes it in the `pnpm check` chain.

Per-app scripts (`pnpm --filter ./apps/<app> <script>`): `dev`, `build`
(`vite build` → `.output/server/index.mjs` + `.output/public`), `start`
(`node .output/server/index.mjs` — what the Dockerfiles and E2E containers run),
`typecheck`, `test`.

## Hosting and structure

- **Hosting is per-app and owned by Start.** Each app has one `vite.config.ts`
  (`tanstackStart` + `react` + `nitro` + `wallowStyles`) and no host files (no `server.ts`,
  `dev-server.ts`, or `vite.ssr.config.ts`). Backend-facing surface = **server routes**
  delegating to an SDK preset (`createApiPassthrough` for wallow-auth/minimal-app,
  `createWallowBffServer` for wallow-web). The generated route tree regenerates as a side effect
  of `vite dev`/`vite build` — never hand-edit it, and do not add a `routes:generate` script or
  `tsr.config.json`. File placement is per-app: the two zoned apps use `src/app/routes/**` +
  `src/app/routeTree.gen.ts` (selected by `srcDirectory: "src/app"`); minimal-app is flat
  (`src/routes/**`, `src/routeTree.gen.ts`, no `app/`).
- **`wallow-web` and `wallow-auth` are zoned; `minimal-app` is deliberately not.** In the zoned
  apps `src/` is `app/` (routes, router, entries, server-only modules), `features/<name>/` (one
  directory per screen or vertical, reachable only through its `index.ts` barrel) and `shared/`
  (what more than one feature genuinely needs). Cross-zone imports use aliases — `@app/*`,
  `@features/<name>`, `@shared/*` — declared **once** in the app's `tsconfig.json` `paths`; Vite
  (`resolve.tsconfigPaths: true`), vitest, and the `wallow/zone-dag` lint rule all read that map,
  so adding a zone is one edit. Relative specifiers stay correct _within_ a zone; the DAG is
  enforced by `wallow/zone-dag`, not convention.
- **Server-only modules live in `app/` and are named `*.server.*`.** `srcDirectory: "src/app"`
  must be paired with `importProtection: { include: ["src/**"] }`, or Start scopes its
  env-boundary check to `src/app` alone and silently stops checking `features/` and `shared/`.
  That pairing sets only the importer scope; what gets denied is the **filename** — Start's
  client rule blocks imports of `**/*.server.*` files and knows nothing about `redis` or
  `node:crypto`. A client module reaching a `*.server.*` file fails the build; a plainly-named
  server wrapper ships to the browser.
- **A hook lives in `features/<name>/hooks/` unless more than one feature needs it, then
  `shared/hooks/`.** `wallow/zone-dag` forbids feature-to-feature imports, so a hook parked in
  the first feature that needed it is unreachable from the second; `shared/` is the only zone
  both may import. Move it the moment a second feature wants it. The extraction test is the
  SCREEN's, not the hook's: when a component holds query wiring, derived narrowing and view
  state at once, the state moves out and the component keeps the markup.
- **Version catalogs**: `@tanstack/react-start`/`react-router`/`react-router-ssr-query` are
  pinned exactly via the **`start` catalog** in `pnpm-workspace.yaml` (`"catalog:start"` in app
  manifests). Ranged shared deps (`react`, `react-dom`, `@tanstack/react-form`/`react-query`,
  `zustand`) come from the **`react` catalog**. Do not collapse the two — a library peering a
  range against an app pinning exactly is correct, not drift. Every app spells out `server.port`
  in `vite.config.ts`.

## Component catalog and lint

App surfaces are built from the `@bc-solutions-coder/ui` component catalog, lint-enforced in all
three apps: each app's `.oxlintrc.json` forbids raw text elements (`p`, `span`, `h1`–`h6`, …)
in favour of `Text`/`PageHeader`, and enables the `wallow/*` rules (`no-sidebar-inversion`,
`no-tinted-text`, `text-heading-variant`, `zone-dag`, `no-hand-rolled-mutation`,
`no-source-tests`). Which config enables which rule, per-app divergences, and override ordering
live in `packages/lint/CLAUDE.md` — read it before editing any `.oxlintrc.json`. Do not
reintroduce a disk-sweeping guard spec for something a lint rule can say.

**wallow-auth's data boundary is lint's too.** Under `src/features/**` and `src/app/routes/**`,
`no-restricted-imports` bans `@bc-solutions-coder/sdk/query` and the raw data operations, so a
screen reaches the API only through its feature's `api.ts` seam (the seam itself is exempted by
a later override — order is the mechanism; see `packages/lint/CLAUDE.md`).
`wallow/no-hand-rolled-mutation` reports any `mutationFn` property, so a write goes through the
generated `{operation}Mutation()` factory; it is on in all three apps.

**A card heading is 20px (`text-xl`) across the whole component catalog** — `Text`'s
`subheading` step and the four surface title recipes in `packages/ui` (`cardTitleRecipe`,
`dialogTitleRecipe`, `alertDialogTitleRecipe`, `drawerTitleRecipe`). The `text-sm`
`toastTitleRecipe`/`popoverTitleRecipe` are transient chrome, deliberately outside the standard.
Auth screens compose `<Text as="h2" variant="subheading" color="onCard">` — no `weight` prop.
Enforced at the call site by `wallow/text-heading-variant` and at the recipe level by measured
`HeadingScale` stories in `packages/ui`. Assert the computed size, never the class string —
`cn()` merges a caller's `className` over the recipe.

## Cross-cutting patterns

- **A per-deployment value reaches the browser through the document, not context.**
  `WALLOW_REPOSITORY_URL`/`WALLOW_DOCS_URL` are read once per request in `src/app/start.ts`,
  stated in `<head>` as an inline script (`forkLinksScript`), and read back by
  `src/shared/lib/fork-links.ts`'s plain `forkLinks()` accessor (falls back to `branding.json`).
  It is a function, not a hook or provider — the value cannot change within a document. Router
  context is NOT the channel: it is rebuilt from nothing on the client, which is a hydration
  mismatch. The accessor imports no `@tanstack/react-start`, keeping `node:async_hooks` out of
  every screen that renders a fork link.
- **The theme class belongs on `document.documentElement`.** Each app's `__root.tsx` stamps
  `className={branding.defaultMode}` on `<html>`, runs `<ThemeScript/>` blocking in `<head>`,
  and wraps the body in `<ThemeProvider/>`. A `<div className="dark">` wrapper renders the LIGHT
  palette — a browser spec that needs a scheme must stamp `documentElement`. See
  `docs/development/frontend-setup.md#dark-mode`.
- **Both zoned apps log through `@bc-solutions-coder/logger`, not `console`.** One browser
  singleton at `src/shared/lib/log.ts` posts to a same-origin ingest route — `/bff/logs` in
  wallow-web (CSRF-gated), `/logs` in wallow-auth (no session, no token). Both mount the SAME
  handler; the load-bearing guard is an origin allowlist resolved per request
  (`createRequestOriginResolver(process.env)`). Server-side code uses `createServerLogger`.
  Events are NAMES, not prose (`bff.logout.failed`). `@bc-solutions-coder/logger/server` is in
  both apps' `SERVER_ONLY_SPECIFIERS` — only the `*.server.*` filename keeps it out of a page.
  Guide: `docs/development/logging.md`; detail: `packages/logger/CLAUDE.md`.
- **Tests**: vitest with the node/browser project split from `@bc-solutions-coder/testing`;
  component specs run in real headless Chromium, never jsdom. A `.tsx` spec that never mounts a
  DOM (renders via `react-dom/server`, or asserts a `beforeLoad` redirect) is named
  **`*.ssr.test.tsx`** — that name routes it onto the node project; there is no per-app list.
  See `.claude/rules/TESTING.md`.
- **E2E**: `test:e2e` (Playwright, per-app `e2e/`) and, for wallow-web only,
  `test:e2e:cross-app` (`e2e-cross-app/`, needs an external three-origin stack). Read
  `.claude/rules/E2E.md` first.
- `wallow-web` and `wallow-auth` each ship a `Dockerfile` whose build context is the **repo
  root** — the whole workspace is needed to resolve `workspace:*`.

## Frontend state boundary

TanStack Query is the only store for backend data. Every key comes from the **generated**
per-operation artifacts in `@bc-solutions-coder/sdk/query` — `{operation}Options()` for a read,
`{operation}Mutation()` for a write, `{operation}QueryKey()` for the key alone; no inline key
literals, never a hand-rolled factory. Keys are flat (`[{ _id, baseUrl, tags, ...args }]`) with
no prefix to sweep by, so invalidation goes through the curated `invalidations` predicates
(`queriesWithTag`, `queriesForOperation`) from the same entry.

react-query enters this workspace in exactly one place: import every react-query symbol from
**`@bc-solutions-coder/query`**, never `@tanstack/react-query` directly. The facade owns
`createQueryClient`, the pinned version, and the one `QueryClientProvider` context. Lint-enforced
by a root `no-restricted-imports` entry; only `packages/query` itself plus the few
`packages/sdk` files needing react-query's types are exempt.

Auth state comes from **`@bc-solutions-coder/auth`**, never a per-app copy: `currentUserQuery` /
`useCurrentUser`, `ensureCurrentUser` for a route's `beforeLoad` gate, and `hasRole` /
`hasPermission` / `isAdmin` (plus the SDK's `requireAuth` / `loginRedirect`, re-exported by
reference).

Zustand holds UI-only global state; it never stores API data.
See `docs/development/frontend-state.md`.
