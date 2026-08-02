# Design: Migrate the frontend workspace to TanStack Start

**status: active**

## Context and motivation

A four-lens team review of the SSR setup (2026-07-27, 32 confirmed findings) established
that the three React apps do **not** run on TanStack Start today, despite comments and an
unused `@tanstack/react-start` dependency claiming otherwise. They run a hand-rolled SSR
layer built on `@tanstack/react-router/ssr/server` + `/ssr/client`, hosted by custom
`packages/web-shell` factories (dev server, standalone h3 host, two-pass Vite build,
pinned `/client.js` asset contract).

The hand-rolled layer exists because a previous spike (Wallow-8w1h.2.3) found Start's
server-route API missing at `@tanstack/react-start@1.168.28` and fell back. That blocker
is gone: current Start supports server routes via a `server` property on
`createFileRoute` (including splat routes like `routes/api/$.ts`), and hosting is plain
`vite build` + `node .output/server/index.mjs`.

Decision (owner, 2026-07-27): migrate all three apps to TanStack Start immediately rather
than polish the hand-rolled kit. The owner's other decisions all align with Start
defaults:

- Query SSR integration: `@tanstack/react-router-ssr-query` (Start's own integration).
- Head management: `HeadContent` + route `head()`.
- Asset delivery: full conventional pipeline (hashed filenames, code splitting, caching).

## End state

`apps/wallow-web`, `apps/wallow-auth`, and `apps/examples/minimal-app` are ordinary
TanStack Start applications. SSR, hydration, head, assets, and dev/prod hosting come from
framework convention. ~25 of the 32 review findings become obsolete because the criticized
code is deleted; the survivors (AnyRouter typing, split-QueryClient pattern, missing
notFoundComponent, stale comments) are fixed in-flight because those files are rewritten.

## Per-app shape (identical across all three apps)

**Dependencies**

- Upgrade `@tanstack/react-router` and `@tanstack/react-start` to current latest and pin
  **exact** versions (no `^` — version drift killed the last attempt).
- Add `@tanstack/react-router-ssr-query`.
- Route codegen moves to Start's Vite plugin; the `tsr` CLI (`@tanstack/router-cli`),
  `tsr.config.json`, and the `routes:generate` script are removed or repointed at the
  plugin as the single source of truth (also resolves the route-codegen drift finding).

**Files**

- One `vite.config.ts` per app: `tanstackStart()` + `react()` + `wallowStyles()`.
  Deleted: `vite.ssr.config.ts`, `dev-server.ts`, `server.ts`, two-pass build scripts.
- `src/router.tsx`: `createRouter` with **inferred** return type (fixes the high-severity
  `AnyRouter` finding), fresh `QueryClient` per request,
  `setupRouterSsrQueryIntegration({ router, queryClient })` owning context/Wrap/
  dehydration/streaming. The split-client bug class becomes unrepresentable.
- `src/routes/__root.tsx`: root-route `head()` + `<HeadContent/>` + `<Scripts/>` replace
  the hand-pinned `clientEntry`/`stylesheetHref` contract and most of `DocumentShell`.
  Branding theme injection and `ReadyIndicator` (the `data-app-ready` E2E marker) stay.
  All three apps register a branded `notFoundComponent` (wallow-auth's existing pattern).
- `src/ssr.tsx` / `src/client.tsx`: replaced by Start's conventional entries; custom
  server entry only if the SDK request context (below) requires one.

**BFF / proxy mounting**

File routes with `server` handlers delegate to the existing framework-agnostic bridges:

- wallow-web: `routes/api/$.ts`, `routes/bff/$.ts`, `routes/health.ts` →
  `handleBffRequest` from `src/lib/bff-server.ts` (written to "delegate unchanged" the
  day this API landed).
- wallow-auth: same pattern over `src/lib/auth-server.ts` (`/v1/**`, `/connect/**`
  prefixes — preserve the existing prefix list; see the OIDC-metadata memory).
- minimal-app: same pattern over `src/lib/proxy-server.ts`.

The SDK's `setSsrRequestContextResolver` wiring moves into Start's global request
middleware.

## Hosting and orchestration

- Build: `vite build` → `.output/`; production start: `node .output/server/index.mjs`
  (Start hosting guide). `PORT` is respected by the output server.
- Dockerfiles: entrypoint swaps from the standalone host to the `.output` server.
- `docker/docker-compose.test.yml`: image commands updated.
- Aspire AppHost: Node resources keep launching `pnpm dev` (now plain `vite dev`
  underneath); no orchestration redesign expected.
- Playwright configs: `webServer` commands unchanged (`pnpm dev`), boot path different.

Deleted rather than fixed: standalone h3 host, static-asset reader, HMR port offset,
both Node/WHATWG bridge copies, the dev server's inline plugin set.

## packages/web-shell (decision: delete the server layer)

After the last app migrates, delete `packages/web-shell/src/server/*` and
`static-assets.ts` (git history preserves them). The package remains for genuinely shared
browser-safe glue: `createQueryClient` and future cross-app helpers. Its CLAUDE.md,
exports map, and tests shrink accordingly.

## Testing and verification

- **Safety net:** the per-app Playwright suites and the cross-app OIDC journey suite run
  unchanged — same URLs, same `data-testid`s, same `data-app-ready` marker, same seeded
  admin smoke. Each app must pass `pnpm check` + its E2E suite before the next app starts;
  the cross-app journey (`pnpm --filter ./apps/wallow-web test:e2e:cross-app`) is the
  final gate.
- **Expected churn:** architecture-pinning vitest specs (route-codegen, build-config,
  static-assets, standalone-host, vitest-preset-migration) are rewritten or deleted with
  the code they pin. Component/browser-mode tests are unaffected.
- CI: the route-tree-drift workflow repoints at the Start plugin's codegen.

## Sequencing (decision: minimal-app first)

1. **minimal-app** — proves the Start app shape, proxy mounting via server routes,
   `wallowStyles()` compatibility, and the `.output` server under Docker, at minimum cost.
2. **wallow-auth** — adds the real OIDC surface and its E2E suite.
3. **wallow-web** — BFF + dashboard + query dehydration tests, once the pattern is proven
   twice.
4. **web-shell server-layer deletion + docs** — `frontend-setup.md`, `bff-pattern.md`,
   CLAUDE.md files, and removal of stale "TanStack Start" comments that are no longer
   lies (the apps will actually be Start apps).

Each step lands green before the next begins, so the workspace is never half-broken.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Start API churn between doc-check and implementation | Pin exact versions on day one; verify APIs against the pinned version's docs, not memory |
| `.output` server behavior under Docker/Aspire (env, signals, ports) | Exercised in step 1 with minimal-app before higher-stakes apps |
| `wallowStyles()` `publicDir` repointing vs Start's plugin | Checked in step 1; fallback is explicit `publicDir` config |
| SDK SSR request context in Start's runtime | Global request middleware; verified by wallow-web's existing ssr-origin tests |
| Fork disruption (web-shell server API removal) | Conventional-commit `feat!:` with migration notes; old layer readable in git history |

## Coordination with the SDK refactor (`0908-sdk-review.md`)

A parallel four-agent SDK review (same date, `docs/plans/2026-07-27/0908-sdk-review.md`)
proposes its own phased refactor: backend security fixes, OpenAPI `operationId` codegen,
collapsing the hand-written query/auth surface onto hey-api plugins, a per-request
`createWallowSdk(options)` replacing the global singleton, and fork-first server/SSR
presets (`createWallowBffServer`, `createSsrScope`). Any unified plan must merge that
sequence with this migration. Analysis for the merge:

### Where the plans agree

- Both are BFF-only. The SDK audit confirmed the TypeScript side already holds no tokens
  in the browser; the actual SPA enablement is the backend `wallow-dev-client` public
  client (SDK review §2 finding #1). The Start migration keeps all apps same-origin BFF
  consumers and makes the tunnel *more* structural (file-based server routes).
- Both delete hand-rolled code in favor of documented happy paths (hey-api query plugin
  on their side, `tanstackStart()` + `@tanstack/react-router-ssr-query` on this side).
- Their item 16 (`createWallowBffServer`) is synergistic with this design's server-route
  mounting: `routes/api/$.ts` / `routes/bff/$.ts` should delegate to the SDK-provided
  handler instead of an app-assembled h3 app, shrinking each app's `bff-server.ts` /
  `auth-server.ts` / `proxy-server.ts` to a configuration call.

### Collision points the unified plan must resolve

1. **SSR request context has two competing designs.** SDK review item 17 ships
   `createSsrScope()` (internal-origin resolution + `AsyncLocalStorage` scoping) from the
   SDK; this design moves `setSsrRequestContextResolver` into Start's global request
   middleware. These must become ONE design: the SDK ships the per-request primitives
   (their items 15 + 17), and Start's request middleware is the consumption site. If the
   migration lands first, the middleware wires around the module-global singleton (whose
   cross-request `baseUrl` bleed their audit documents) and gets rewritten later —
   avoid that.
2. **Ordering dependency on `createWallowSdk` (their item 15).** The per-request client
   replaces the config authority the migration would otherwise wire into middleware.
   It must land before, or as part of, the first app migration (minimal-app), so the
   Start pattern is proven against the final SDK shape.
3. **Same files, two epics.** Their "~420 lines of fork glue" deletion list
   (`bff-server.ts`, `ssr-origin.ts`, configurator/bootstrap chains in `lib/*-sdk.ts`,
   dehydrate/hydrate wiring) is largely the same file set this migration rewrites or
   deletes. The two efforts must be sequenced, never run in parallel across the same app.
4. **Query dehydration ownership (decision needed, recommendation below).** Their audit
   counts the ~40 lines of dehydrate/hydrate wiring as fork glue the SDK should absorb;
   this design assigns that job to `@tanstack/react-router-ssr-query`.
   **Recommendation: ssr-query owns dehydration; the SDK must NOT ship its own
   dehydration preset.** Two owners of the same mechanism is how the current triplicated
   drift happened.
5. **Query keys rule rewrite.** Their generated-keys decision (flat hey-api keys for
   fetching + a small curated invalidation module) composes fine with ssr-query, but it
   invalidates the CLAUDE.md rule "all query keys come from the SDK's `queryKeys`
   factory". Settle the new rule before feature code churns, and update CLAUDE.md +
   `docs/development/frontend-state.md` in the same change.

### Recommended merged sequence (input to the unified plan)

1. **SDK Phase 0 (backend security) — immediately, independent.** Touches no frontend
   file; the critical seed/scope findings must not wait on migration work.
2. **SDK Phase 1 (operationId + typed success bodies + regen) — before any frontend
   refactor.** Their doc already mandates this ("absorb the rename exactly once"); both
   later efforts want the stable operation names.
3. **SDK item 15 (`createWallowSdk` per-request) — before or with the minimal-app
   migration** (see collision #2).
4. **Start migration app-by-app (this design's sequencing), with SDK Phase 3 items
   16–17 folded in**: `createWallowBffServer` and `createSsrScope` land in the SDK as
   minimal-app migrates and are consumed via server routes + request middleware; each
   subsequent app reuses them. Their item 18 (claim helpers) slots naturally into the
   wallow-web step (dashboard route guards).
5. **SDK Phase 2 remainder (query-core plugin, `responseStyle: 'data'`, `WallowError`
   unification, auth-client deletion) — after the migration**, when app shells are
   stable and only feature files churn.
6. **SDK Phase 4 (build-time OpenAPI emit, auto-regen loop) — orthogonal; anytime.**
   Their item 19 (credential/transport split for future React Native) is also
   independent of Start and can be scheduled on its own merits.

End-state test for the unified plan: a fresh fork should get a working app from
"install the SDK, add `tanstackStart()` to `vite.config.ts`, mount the splat server
routes, write feature screens" — with no hand-written BFF assembly, SSR origin
resolution, bootstrap chaining, or dehydration wiring.

## Explicitly rejected alternatives

- **Polish the hand-rolled kit** (ssr-query + HeadContent + hashed pipeline built by
  hand): hand-builds three things Start provides; work discarded on any later migration.
- **Spike-then-migrate phasing**: owner chose immediate migration; the two spike unknowns
  were instead resolved against current Start docs (server routes exist; hosting is
  documented) and the residual runtime risk is contained by migrating minimal-app first.
- **Deprecate web-shell server layer in place**: ships ~1,000 lines of dead commented
  code in a fork-first template; contradicts the review's readability goals.
