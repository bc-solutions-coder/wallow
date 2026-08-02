# apps — Frontend Applications Agent Guide

Every app here is a **TanStack Start** frontend consuming the `@bc-solutions-coder` workspace
packages (`sdk`, `styles`, `ui`, `forms`, `navigation`, `query`, `auth`, `utils`, `env`, `logger`,
`testing`, `config`) via `workspace:*`. `forms`, `auth`, `navigation` and `logger` are the
optional ones — `examples/minimal-app` renders no form, has no signed-in user, has no shell and
records nothing, so it omits all four. `wallow-auth`'s screens sit in its own `auth-layout.tsx`,
which leaves `wallow-web` as `navigation`'s one consumer today. `config` is the odd one: a
build-time-only dependency supplying `wallowAppConfig()` to `vite.config.ts`, never imported by
app code.

| App                     | Port | What it is                                                               |
| ----------------------- | ---- | ------------------------------------------------------------------------ |
| `wallow-web/`           | 3000 | Reference dashboard demonstrating the full same-origin BFF OIDC flow.    |
| `wallow-auth/`          | 3002 | Auth frontend — login / signup / MFA screens.                            |
| `examples/minimal-app/` | 3010 | Smallest app wiring the core shared packages into a TanStack Start host. |

**Build the SDK before touching an app** — apps typecheck against `packages/sdk/dist/`:
`pnpm --filter @bc-solutions-coder/sdk build`.

Per-app scripts (`pnpm --filter ./apps/<app> <script>`): `dev` (`vite dev`), `build`
(`vite build` → `.output/server/index.mjs` + `.output/public`), `start`
(`node .output/server/index.mjs` — what the Dockerfiles and E2E containers run),
`typecheck`, `test`.

- **Hosting is per-app and owned by Start.** Each app has one `vite.config.ts`
  (`tanstackStart` + `react` + `nitro` + `wallowStyles`) and no host files: `server.ts`,
  `dev-server.ts`, `vite.ssr.config.ts`, and the hand-rolled host-runtime `./server` presets
  the deleted shared frontend-runtime package used to ship are all gone. Backend-facing surface = **server routes** under `src/app/routes/**` delegating to
  an SDK preset (`createApiPassthrough` for wallow-auth/minimal-app, `createWallowBffServer`
  for wallow-web). `src/app/routeTree.gen.ts` regenerates as a side effect of `vite dev`/`vite
build` — never hand-edit it, and do not add a `routes:generate` script or `tsr.config.json`.
- **`wallow-web` and `wallow-auth` are zoned; `examples/minimal-app` is deliberately not.**
  In the two zoned apps `src/` is `app/` (routes, router, entries, server-only modules),
  `features/<name>/` (one directory per screen or vertical, reachable only through its
  `index.ts` barrel) and `shared/` (what more than one feature genuinely needs).
  Cross-zone imports are spelled as aliases — `@app/*`,
  `@features/<name>`, `@shared/*` — declared **once**, in the app's `tsconfig.json` `paths`.
  Vite reads it natively (`resolve.tsconfigPaths: true`), vitest reads it inside each
  `test.projects` entry, and the `wallow/zone-dag` lint rule reads it to derive which prefixes
  it polices — so adding a zone is that one edit. Relative specifiers stay correct
  _within_ a zone. The DAG itself is enforced by a lint rule, not convention:
  `wallow/zone-dag` resolves every specifier against its importer's real directory and
  judges the edge. Two consequences worth knowing before you move a file: server-only modules
  belong in `app/` (that is what keeps `node:crypto`/`openid-client` out of the client graph),
  and `srcDirectory: "src/app"` in `vite.config.ts` must be paired with
  `importProtection: { include: ["src/**"] }` or Start scopes its env-boundary check to
  `src/app` alone and silently stops checking `features/` and `shared/`. That pairing sets
  only the importer SCOPE. What actually gets denied is the **filename**: Start's default
  client rule blocks an imported file matching `**/*.server.*` and knows nothing about
  `redis` or `node:crypto`, so every server-only module is named `*.server.*`
  (`wallow-web/src/app/lib/bff.server.ts`, `wallow-auth/src/shared/lib/api-passthrough.server.ts`)
  and `src/server-only-naming.test.ts` — byte-identical in both apps — keeps it that way.
- Every app spells out `server.port` in its `vite.config.ts` (`vite dev` binds 3000 when
  `PORT` is unset). `@tanstack/react-start`/`react-router`/`react-router-ssr-query` are still
  pinned exactly, but the pin now lives in the **`start` catalog** in `pnpm-workspace.yaml`:
  app manifests say `"catalog:start"`, and the exact version is edited in one place. Ranged
  shared deps (`react`, `react-dom`, `@tanstack/react-form`/`react-query`, `zustand`) come from
  the sibling **`react` catalog**. Do not collapse the two — a library peering
  `@tanstack/react-router@^1.170.18` against an app pinning `1.170.18` exactly is correct
  practice, not drift, and stays a literal.

- **App surfaces are built from the catalog, and in BOTH apps that is lint-enforced.**
  `apps/wallow-web/.oxlintrc.json` and `apps/wallow-auth/.oxlintrc.json` (the root config carries
  none of these rules) add `react/forbid-elements` for raw `p`, `span`, `legend`, `code` and
  `h1`–`h6`, pointing each at `Text`/`PageHeader` so the catalog owns the type scale once, plus
  custom rules from the `@bc-solutions-coder/lint` plugin (`packages/lint`):
  `wallow/no-sidebar-inversion` (bans the `bg-foreground`/`text-background` inversion hack in
  favour of the recipes' `surface="sidebar"` axis), `wallow/no-tinted-text` (bans
  `text-<token>/<alpha>` — muted copy is `text-muted-foreground`; a translucent _surface_ such as
  the drawer scrim's `bg-foreground/40` stays legal), `wallow/text-heading-variant`
  (wallow-auth only: every `<Text as="h_">` must name its `variant`, an `h2` must be
  `subheading` and carry no `weight`, and no file but `auth-layout.tsx` may open an `h1`), and
  `wallow/zone-dag` (the import graph above). The first three are off for `*.test.*` and
  `*.stories.tsx`; `zone-dag` deliberately is NOT, because it judges a spec's edges too — its
  one spec exemption (`@app/*`) is inside the rule. The scoping is deliberate: `packages/ui`
  legitimately paints a `bg-foreground` backdrop, so the gate must never reach it, and the plugin
  is registered from the nested configs only so it stays invisible to
  `packages/sdk`'s guardrail test (which copies the ROOT config to a temp dir). The two apps are
  NOT identical — wallow-auth also forbids raw `<button>`, which wallow-web cannot because
  `bff-demo` deliberately ships four un-catalogued ones. These rules replaced ~1,400 lines of
  disk-sweeping guard specs (`catalog-adoption.test.ts`, both `typography.test.ts`,
  `dashboard-chrome-tokens.test.ts`); do not reintroduce a regex sweep for something a rule can
  say.
- **wallow-auth's data boundary is lint's too.** Under `src/features/**` and
  `src/app/routes/**` a `no-restricted-imports` override bans `@bc-solutions-coder/sdk/query`
  outright and bans the three raw data operations (`accountForgotPassword`,
  `accountResetPassword`, `mfaExchangeEnrollmentToken`) from the barrel by name, so a screen
  reaches the API only through its feature's `api.ts` seam; pure helpers and DTO types
  (`isSafeReturnUrl`, `buildExchangeTicketUrl`, `InvitationResponse`) issue no request and stay
  direct barrel imports. The seam itself is let past by a **later** override turning the rule
  `"off"` for `src/features/*/api.ts` and its co-located `api.test.ts` — oxlint has no
  `excludedFiles`, so ORDER is the mechanism, and an override's `no-restricted-imports` entry
  REPLACES the base one rather than merging, which is why that override restates every root ban
  it still wants. `wallow/no-hand-rolled-mutation` (wallow-auth's fifth plugin rule) reports any
  `mutationFn` property, so a write goes through the generated `{operation}Mutation()` factory.
  Together they replaced the table-driven halves of `generated-mutations.test.ts` and
  `features-api-seam.test.ts` (~950 lines down to ~290); what those two still hold is runtime
  fact about the generated surface and the seam's _shape_, derived from disk rather than from a
  hand-kept list of screen paths.
- **Backend data, react-query imports, and auth state each have exactly one source.** Stated in
  full under "Frontend state boundary" at the bottom of this file — read it before adding a query,
  a mutation, or a permission check.
- **A card heading is 20px (`text-xl`), catalog-wide.** That is `Text`'s `subheading` step,
  which already sat there, plus the four `packages/ui` "names the surface" title recipes moved
  onto it — `cardTitleRecipe`, `dialogTitleRecipe`, `alertDialogTitleRecipe` and
  `drawerTitleRecipe`, all previously `text-lg` (18px). 16px is the browser's default body
  size, so a 16px heading computes the same size as the copy beneath it and the hierarchy rests
  entirely on weight and colour; 20px keeps a heading one real step above body text. The two
  `text-sm` title recipes are deliberately NOT in this standard: `toastTitleRecipe` and
  `popoverTitleRecipe` are transient chrome, not surface headings. All 16 `wallow-auth` screens
  compose `<Text as="h2" variant="subheading" color="onCard">` — no `weight` prop, the step
  carries `font-semibold` itself. `heading-scale.test.tsx` measures the computed font-size
  across every screen in a real browser and `wallow/text-heading-variant` holds every file the
  linter reaches, so a new screen cannot skip it; in `packages/ui` the recipe-level pin is
  a measured `HeadingScale` story per title-bearing component (`.storybook/heading-scale.tsx`),
  because only the `storybook` project there loads Tailwind. Assert the computed size, never the
  class string — `cn()` merges a caller's `className` over the recipe, so `text-xl` can be
  present while the element paints something else.
- **A per-deployment value reaches the browser through the document, not through context.**
  `WALLOW_REPOSITORY_URL` / `WALLOW_DOCS_URL` are read once per request in `src/app/start.ts`
  (`resolveForkLinks(process.env)`), stated in `<head>` as an inline script beside `ThemeScript`
  (`forkLinksScript`), and read back by `src/shared/lib/fork-links.ts`'s plain `forkLinks()`
  accessor — the pattern for anything else the environment decides. It is a function, not a hook
  and not a provider: the value cannot change within a document, so there is nothing to subscribe
  to, and a bare-mounted spec or a story keeps working because the accessor falls back to
  `branding.json`'s pair. Router context is NOT the channel — it is rebuilt from nothing on the
  client, so a link read from it would render the deployment's URL on the server and the fork's in
  the browser, which is a hydration mismatch. The accessor deliberately imports no
  `@tanstack/react-start`: the request-side read lives in `__root.tsx`, which is what keeps Start's
  `node:async_hooks` storage out of every screen that renders a fork link (wallow-web's
  `vitest.config.ts` shims that module for exactly this reason).
- **The theme class belongs on `document.documentElement`.** Each app's `__root.tsx` stamps
  `className={branding.defaultMode}` on `<html>`, runs `<ThemeScript/>` blocking in `<head>`,
  and wraps the body in `<ThemeProvider/>`. A `<div className="dark">` wrapper anywhere renders
  the LIGHT palette — a browser spec that needs a scheme must stamp `documentElement`. See
  `docs/development/frontend-setup.md#dark-mode`.
- **Both zoned apps log through `@bc-solutions-coder/logger`, not `console`.** Each holds one
  browser singleton at `src/shared/lib/log.ts` posting to a same-origin ingest route it mounts
  itself — `/bff/logs` in wallow-web (CSRF-gated, because of where the route lives) and `/logs`
  in wallow-auth (no session, so no token to check). Both mount the SAME handler and neither
  reimplements a guard; the load-bearing one is an origin allowlist resolved per request to
  `resolveRequestOrigin(request)`, which is why there is no logging environment variable beyond
  the standard `OTEL_EXPORTER_OTLP_ENDPOINT`. Server-side code uses `createServerLogger`
  (wallow-web's is `src/app/lib/log.server.ts`, deliberately split out of `log-ingest.server.ts`
  to keep `bff.server.ts` off an import cycle); wallow-auth has none, because nothing in its
  server code records anything yet. Events are NAMES, not prose (`bff.logout.failed`), and
  `@bc-solutions-coder/logger/server` is in both apps' `SERVER_ONLY_SPECIFIERS` — it would bundle
  cleanly, so only the `*.server.*` filename keeps it out of a page. Guide:
  `docs/development/logging.md`; contributor detail: `packages/logger/CLAUDE.md`.
- **Tests**: `test` is vitest with the two-project node/browser split from
  `@bc-solutions-coder/testing`; component specs run in real headless Chromium, never jsdom.
  A `.tsx` spec that renders through `react-dom/server` or asserts a `beforeLoad` redirect — and
  so never mounts a DOM — is named **`*.ssr.test.tsx`**, which is how the preset routes it onto
  the node project; there is no per-app list to append to. Shared `resolve`/`ssr` settings are
  stated once at the config root and pulled into each project with `extends: true`.
  See `.claude/rules/TESTING.md`.
- **E2E**: `test:e2e` (Playwright, per-app `e2e/`) and, for wallow-web only,
  `test:e2e:cross-app` (`e2e-cross-app/`, needs an externally supplied three-origin stack).
  Read `.claude/rules/E2E.md` before editing anything under `e2e/`.
- `wallow-web` and `wallow-auth` each ship a `Dockerfile` whose build context is the **repo
  root** — the whole workspace is needed to resolve `workspace:*`.

## Frontend state boundary

TanStack Query is the only store for backend data. Every key comes from the **generated**
per-operation artifacts in `@bc-solutions-coder/sdk/query` — `{operation}Options()` for a read,
`{operation}Mutation()` for a write, `{operation}QueryKey()` when you need the key alone; no
inline key literals, and never a hand-rolled factory. Those keys are flat
(`[{ _id, baseUrl, tags, ...args }]`) with no prefix to sweep by, so invalidation goes through
the curated `invalidations` predicates (`queriesWithTag`, `queriesForOperation`) from the same
entry.

react-query itself enters this workspace in exactly one place. Import `useQuery`,
`useMutation`, `QueryClient`, `QueryClientProvider` and every other react-query symbol from
**`@bc-solutions-coder/query`**, never `@tanstack/react-query` directly — the facade also owns
`createQueryClient` and the pinned version, and it keeps one `QueryClientProvider` context
across the workspace. This is lint-enforced, not a convention: a root `.oxlintrc.json`
`no-restricted-imports` entry fails `pnpm lint` on a direct import, and only `packages/query`
itself plus the few `packages/sdk` files that need react-query's types are exempt.

Auth state comes from **`@bc-solutions-coder/auth`**, never from a per-app copy:
`currentUserQuery` / `useCurrentUser` for the current user, `ensureCurrentUser` for a route's
`beforeLoad` gate, and `hasRole` / `hasPermission` (plus the SDK's `requireAuth` / `isAdmin`,
re-exported by reference) for role and permission checks.

Zustand holds UI-only global state; it never stores API data.
See `docs/development/frontend-state.md`.
