**status: active**

# Streamlining Plan — SDK, Codegen, and the TanStack Start Migration

Companion to `0908-sdk-review.md` (the findings record). This document is the **plan**: what to
change, in what order, and what it deletes. It incorporates a second review pass covering
TanStack Start usage and a workspace-wide "stop reinventing wheels" sweep.

Guiding goal: a developer installs `@bc-solutions-coder/sdk`, points it at a Wallow backend, and
builds a whole application with near-zero bespoke glue. Everything below serves that.

---

## The premise that changed

**The apps are not TanStack Start apps.** `@tanstack/react-start@1.168.28` is a declared
dependency of `wallow-web`, `wallow-auth`, and `minimal-app`, and is **never imported anywhere in
the repo** — the only mention is a comment explaining why it isn't used. The apps are TanStack
*Router* apps with a hand-rolled SSR host built on `@tanstack/react-router/ssr/server`
primitives. The SSR entry's own comment says "this is the same pipeline TanStack Start uses,"
which is the tell: Start was reimplemented from its parts.

So "switch to fully using TanStack Start" is not a refactor of how Start is used. It is adopting
Start for the first time. That subsumes several line items from the original plan.

### The mistaken spike that produced ~420 lines/app of glue

`apps/wallow-web/src/lib/bff-server.ts:19-33` justifies its hand-rolled h3 app by concluding that
`createServerFileRoute`/`createServerRoute` "does NOT exist in the installed stack."

**That conclusion is wrong, and it was double-checked because two reviewers disagreed about it.**
The API was not missing — it was *renamed and relocated*. Server routes now live in
`createFileRoute`'s `server` option, and the type augmentation ships in
`@tanstack/start-client-core@1.170.14`, which module-augments `@tanstack/router-core`:

```
node_modules/.pnpm/@tanstack+start-client-core@1.170.14/.../dist/esm/serverRoute.d.ts
  declare module '@tanstack/router-core' {
    interface FilebaseRouteOptionsInterface<...> { server?: RouteServerOptions<...> }
  }
  RouteServerOptions = { middleware?, handlers? }
  RouteMethod = 'ANY'|'GET'|'POST'|'PUT'|'PATCH'|'DELETE'|'OPTIONS'|'HEAD'
  RouteMethodHandlerCtx = { context, request: Request, params, pathname, next }
```

Grepping `router-core` or `react-router` alone misses this precisely *because* it is a module
augmentation living in the Start package. The handler signature is `Request → Response`, which is
already the shape `handleBffRequest` exposes — so the SDK handler bodies port over directly.

> **Do this first, independent of everything else:** correct the comment at
> `apps/wallow-web/src/lib/bff-server.ts:19-33`. It is wrong *today*, it is the first thing anyone
> investigating this file reads, and it will keep re-deriving the same wrong conclusion until the
> file is deleted in Phase 3. A comment edit; no behavior change.

### The Vite 8 question — resolved, low risk

Server routes only execute under the **Start Vite plugin**, which the repo never registers
(`packages/web-shell/src/server/vite-presets.ts` uses `tanstackRouter()` + `react()`).

**Verdict: adopt the plugin with `installDevServerMiddleware: true`. That is the whole
mitigation.** TanStack/router#7614 ("dev server returns 'Cannot GET /' on Vite 8", open since
2026-06-12) describes two bugs; the analysis below is read from the *installed* source at
`@tanstack/start-plugin-core@1.171.20/dist/esm/vite/dev-server-plugin/plugin.js`, not from the
issue text.

| Bug in #7614 | Applies here? | Why |
| --- | --- | --- |
| **Bug 1** — `'dispatchFetch' in serverEnv` early-return skips middleware, every request 404s | **Yes, but opt-out** | That guard and a second `if (config.server.middlewareMode) return` both sit *inside* the `installDevServerMiddleware == undefined` branch. Passing `true` skips the branch entirely. |
| **Bug 2** — `instanceof RunnableDevEnvironment` fails across Vite copies | **No** | Needs the plugin and the dev server in *different* Vite resolutions. They aren't. |

Bug 1's `middlewareMode` guard matters independently of Vite 8: today's `dev-server.ts` runs Vite
in middleware mode, so the plugin would silently no-op for that reason alone. Hence
`installDevServerMiddleware: true` is **required**, not optional.

Bug 2's mechanism is real — `isRunnableDevEnvironment()` imported from `vite` has the body
`return environment instanceof RunnableDevEnvironment`, so class identity must match. It just
doesn't bite here. Verified via the pnpm symlink graph:

```
start-plugin-core → vite@8.1.4_@types+node@24.13.3
apps/wallow-web   → vite@8.1.4_@types+node@24.13.3
apps/wallow-auth  → vite@8.1.4_@types+node@24.13.3
packages/sdk      → vite@8.1.4_@types+node@22.20.1   ← second copy, unreachable from the plugin
```

The duplicate copy exists only because `@types/node` is an **optional peer** of Vite, so pnpm keys
the store path by it. Both `22.20.1` and `24.13.3` satisfy Vite's range — it is duplication, not
incompatibility. `packages/sdk` is a library build that never loads the Start plugin, so its copy
cannot participate in the identity mismatch.

**Consequences for anyone reading this plan:**

- Aligning `packages/sdk` to `@types/node ^24` is **hygiene, not a fix**. Do it (the repo targets
  Node 24 via `.nvmrc`), but do not expect it to change any behavior, and do not treat it as a
  prerequisite for the migration.
- **Do not adopt Vite+.** #7218 (open, 2026-04-17) and #6982 (closed, 2026-03-19) are both reports
  of Bug 2 caused *by* Vite+: aliasing `vite` → `@voidzero-dev/vite-plus-core` manufactures the
  two-class-identity mismatch this repo currently avoids. #7614 lists Vite+ 0.1.24 as affected.
- **Do not pin Vite 7.** That is #7614's documented workaround for a problem this repo doesn't have.

Nothing above has been executed — it is read from source and the dependency graph. The Phase 3
spike is what converts it from strongly-indicated to confirmed.

---

## The architectural decision this forces

Adopting Start raises a real tension with the SDK's goals. Start ships things the SDK hand-rolls:
`getRequest()`/`getRequestHeaders()`/`getRequestUrl()` (replacing the AsyncLocalStorage seam), a
full sealed-cookie session API (`Session`/`SessionManager`/`SessionConfig`, iron-style, with a
`ttl` in its `SealOptions` — the very gap flagged as security finding #7), and
`createCsrfMiddleware`.

**Do not adopt Start's session or CSRF inside the SDK.** The SDK is a published package whose
stated goals include framework-agnostic `queryOptions` and eventual React Native support.
Coupling it to TanStack Start would defeat both. The split:

- **Apps** go all-in on Start and use its request context, middleware, and server routes.
- **The SDK's BFF stays framework-neutral** — web-standard `Request`/`Response` in, out.
- **An optional `@bc-solutions-coder/sdk/start` adapter subpath** wraps the BFF handlers as Start
  server-route handlers, so forks on Start get one-line wiring while non-Start consumers keep a
  portable core.
- Steal the *lessons* regardless: pass an explicit `ttl` to iron seal/unseal (security finding #7).

**Prerequisite spike:** the SDK's `createBffHandlers`/`createApiProxy` are h3 `EventHandler`s. The
port to web-standard `Request`/`Response` is the enabler for all of this, and its one genuine risk
is multi-`Set-Cookie` handling (which `auth-server.ts` notes depends on h3's `toWebHandler`
round-trip) interacting with the SDK's session-cookie chunking. Verify before committing.

---

## Phase 0 — Security (unchanged, still first)

From `0908-sdk-review.md`. Nothing here depends on the Start decision; none of it should wait.

1. Delete `wallow-dev-client` from `api/seed.json` and its `FirstPartyClients` entry — a public,
   secret-less client with ~35 scopes that lets any browser hold tokens. Unused in-repo.
2. Wire `IScopeSubsetValidator` into `AuthorizationController` (currently only in `ApiKeysController`).
3. Explicit `"public": true` client flag + startup hard-fail, replacing "public means no secret
   was supplied" — which silently downgrades the production BFF client on a missing env var.
4. Replace the `sa-` prefix check in `TenantResolutionMiddleware` with a real operator flag.
5. Call the SDK's existing `isSafeReturnUrl` in the BFF login handler — one line, fixes the open redirect.
6. Pass an explicit `ttl` to iron `seal`/`unseal`; document Valkey as required for production.
7. Decide the global-`admin` design question (can a tenant admin mint cross-tenant admins?).
8. Add a targeted integration test for tenant filtering on the **compiled** queries in
   `StoredFileRepository`/`ServiceAccountRepository`.

## Phase 1 — Backend OpenAPI (unblocks everything downstream)

9. Operation transformer emitting `operationId` from `ControllerActionDescriptor.MethodInfo.Name`.
   All 149 operations currently lack one; the fallback names are unstable across route renames,
   silently breaking forks. Regenerate and absorb the one-time rename.
10. Add `[ProducesResponseType(typeof(T), 200)]` to the 67 operations with no typed success body.
11. Exclude the `Test Support` endpoint from the v1 document — it currently ships in the SDK.
12. Rewrite `openapi-regen.test.ts` as an "every operation has an operationId" invariant.

## Phase 2 — Collapse the hand-written SDK surface

13. Add hey-api's `@tanstack/query-core` plugin; move the React peer dep to optional. Covers all
    149 operations instead of the 21 wrapped today; deletes ~410 lines and unblocks Vue/Svelte/Solid.
    Decision: take the generated flat query keys, keep a small curated module for the few places
    needing hierarchical prefix invalidation.
14. Set `responseStyle: 'data'` + `throwOnError: true`; unify both apps on `WallowError`; delete
    both `unwrap` implementations and the `MfaUnwrap` seam.
15. Reduce `auth-client.ts` to a ~70-line `auth-extras.ts` keeping only the three real quirks:
    `getCurrentUser`'s 401-softening, the space-joined `scopes` shaping, the `clientId` key-omission guard.
16. Replace the module-global client with `createWallowSdk(options)` constructed per call. Fixes
    the non-idempotent interceptors, both once-guards, the cross-request SSR `baseUrl` bleed, and
    the baked-in `/api` default in one change.
17. Fix `login()`/`logout()` assigning bare-global `location` (500s under SSR). Use
    `createIsomorphicFn()` or a thrown `redirect()`. The apps work around this today; the landmine
    is still armed in the SDK for every fork.

## Phase 3 — TanStack Start migration

**Confirmation spike first (~30 min; expected to pass — see the Vite 8 section above).** In one
app, register `tanstackStart()` with **`installDevServerMiddleware: true`** and verify that the
dev server SSRs a route and that a production build boots. The flag is required, not optional.

If it fails unexpectedly, stop and re-open the Vite 8 analysis — but do **not** reach for Vite+ or
a Vite 7 pin; both are addressed above and neither is the right lever here.

Then:

18. Register `tanstackStart()` in the shared Vite config (it subsumes `tanstackRouter()`).
19. Move `/bff/*` and `/api/**` to file-based server routes (`src/routes/bff/$.ts`,
    `src/routes/api/$.ts`) with `server.handlers.ANY`. Same for wallow-auth's `/v1`, `/connect`,
    `/.well-known` passthrough. Deletes the h3 mounting/dispatch layers and `proxy-topology.ts`.
20. Replace the AsyncLocalStorage seam + `createSsrRequestContext` with `getRequest()`/`getCookie()`.
    **Keep `resolveSsrInternalOrigin`** — Start returns the browser-facing URL, which is the wrong
    answer for container self-fetch. Better still, moving self-fetches to server functions removes
    the problem entirely.
21. Adopt `<HeadContent/>` + `<Scripts/>` in `__root.tsx`. This is not cosmetic: the hardcoded
    `<script src="/client.js">` forces unhashed asset filenames, which **disables cache-busting in
    production**, blocks route-level code splitting (`autoCodeSplitting: false`), and is why React
    Fast Refresh is disabled in dev in both apps.
22. Install `@tanstack/react-router-ssr-query` and use `setupRouterSsrQueryIntegration`, replacing
    the JSON-string dehydrate workaround. The current approach can't stream and silently drops
    anything non-JSON-round-trippable (Errors, Dates, suspended-query promises). **`wallow-auth`
    has no dehydrate/hydrate at all** — every SSR-prefetched query is thrown away and refetched.
    That is a live performance bug and the strongest argument for converging the two apps.
23. Delete `apps/*/dev-server.ts`, `server.ts`, and the `web-shell` host runtime it replaces
    (~1,750 lines including tests).

## Phase 4 — Release automation

24. Spike `OpenApiDocumentsDirectory` build-time OpenAPI emit. Caveat: `GetDocument.Insider` halts
    at `builder.Build()`, so `/alive` and `/events` likely drop from the doc, and the drift check
    is a byte comparison — resolve that delta first. Payoff: `openapi-drift.yml` sheds Postgres,
    Valkey, and migrations.
25. Auto-regen on backend merge committing `feat(sdk):`. release-please is **already** configured
    for the `packages/sdk` component with `include-component-in-tag`, and `sdk-publish.yml` already
    triggers on `sdk-v*` — the loop is nearly closed.

## Phase 5 — Fork-first ergonomics

26. Ship `createWallowBffServer(config)` (or the Start adapter from the decision above), absorbing
    each fork's store selection, Redis bridging, and route mounting.
27. Add browser-side claim helpers (`isAdmin`, `hasRole`, `getRoles`) so route guards stop
    hand-parsing the roles claim.
28. Separate credential/transport from the generated client — cookie+CSRF for web, DPoP bearer for
    native. **Do this before React Native work starts.**

---

## Additional findings not in the original plan

### Type safety is silently off — highest value per character changed

`createRouter(): AnyRouter` in both apps' `router.tsx`, registered via `declare module` as the
global router type. `AnyRouter` is `RouterCore<any, any, any, any, any>`, so every `<Link to>`,
`useParams`, `useSearch`, `useRouteContext`, and `navigate()` in both apps is typed `any`. The
committed `routeTree.gen.ts` is doing nothing.

**Empirically confirmed**, not inferred: a probe file containing
`<Link to="/this-route-does-not-exist-xyz">` was added to `apps/wallow-web/src/`, verified present
in `tsc --listFiles`, and `tsc --noEmit` exited **0** with no error. A control file with a
deliberate type error in the same position *did* fail, confirming the harness was live.

Deleting the two `: AnyRouter` annotations restores inference — and will likely surface a batch of
real latent errors, which is the point. Do this early; it makes every later phase safer.

### Duplicated reverse proxies across three apps

`apps/wallow-auth/src/lib/auth-server.ts` (201 lines) and
`apps/examples/minimal-app/src/lib/proxy-server.ts` (139) are the same file with comments stripped —
identical `resolveApiInternalUrl`, `applyForwardedHeaders`, `createProxyHandler`, and h3 router
wiring, down to the `DEFAULT_API_INTERNAL_URL` constant; the only real difference is a 10-line
`X-Forwarded-For` append. `proxy-paths.ts` is byte-identical between them, and the two apps'
`branding.ts` shims differ by one word in a comment. ~423 source + 556 test lines, ~300 of it pure
duplication.

This has already caused an outage-class bug once: a drifted copy of the path predicate was missing
`/.well-known/**`, so OIDC discovery and JWKS 404'd under `pnpm dev`. The forwarded-headers logic
decides the scheme the API computes cookie `Secure` from, so divergence is security-relevant.

Extract `createPassthroughProxy({ apiInternalUrl, forwardClientIp })` into `web-shell`. Note Phase 3
step 19 partly subsumes this — sequence accordingly rather than doing both.

### ~2,800 lines of tests that assert config-file text

Twenty-five specs `readFileSync` a config file and assert its contents as strings: `package.json`
export shapes (three `package-scaffold.test.ts` files, ~460 lines), Dockerfile `COPY` ordering
(~160 — whose own comment concedes it is "a stand-in for the slow full docker build"), workflow
YAML trigger paths, and one-time migration guards still running on every invocation.

These were deliberate TDD acceptance criteria for scaffolding beads and several say so. They did
their job; scaffolding should come down after the build. Substitutions: `publint` +
`@arethetypeswrong/cli` for export shape (validates against files actually emitted, catching a
`types` path present in `package.json` but absent on disk — which the hand-written assertions
can't), an actual image build in CI for the Dockerfile, and deletion for workflow-text and
migration guards. Do it file-by-file — `service-identity.test.ts` and the route-codegen specs mix
genuine behavioral assertions in with structural ones.

### Small, free wins

- `@tanstack/react-start` is currently declared by all three apps and imported by none, which
  misleads readers into thinking Start drives them. Phase 3 makes the dependency real — so leave it
  in place and let Phase 3 resolve it. Only drop it if Phase 3 is abandoned.
- `packages/testing/src/vitest-projects.ts` hand-writes a recursive `deepMerge` (~20 lines);
  `mergeConfig` from `vitest/config` is already imported in that same file.
- `packages/sdk` declares `iron-webcrypto` directly while `h3` (also a direct dep) bundles the exact
  same resolved version. Import through h3 so they can't version-skew.
- Align `packages/sdk` to `@types/node ^24` — hygiene only (the repo targets Node 24 via `.nvmrc`).
  It is *not* a prerequisite for the Phase 3 spike: the SDK's separate Vite copy never loads the
  Start plugin, so it cannot trigger #7614's identity mismatch.
- `packages/ui` merges Tailwind classes by naive concatenation, so a caller's `className` override
  loses to source order rather than intent. `clsx` + `tailwind-merge` fixes it — marginal, do it
  only if someone hits the bug.

---

## Explicitly correct as-is — do not "simplify" these

Recorded so a later pass doesn't churn them:

- **`packages/sdk/src/server/proxy.ts`** (699 lines) — looks replaceable by a proxy library; is not.
  Silent OIDC refresh under a distributed lock, reactive retry on upstream 401, detection of .NET
  cookie-auth challenges disguised as 3xx, bounded `Retry-After` honoring, verbatim RFC 7807
  passthrough. No library does this combination.
- **CSRF double-submit validation** — `timingSafeEqual` with a length pre-check, correctly refusing
  to let an empty header satisfy a present session token. Textbook.
- **Session cookie chunking** — splits sealed values across indexed cookies under a size cap and
  clears stale higher-index chunks. h3's session utils don't chunk.
- **`server/oidc.ts`** — already thin over `openid-client`; the bespoke part (`rewriteOrigin`) is a
  real OpenIddict requirement.
- **`SessionStore` + the node-redis adapter** — `unstorage` is the tempting swap, but it has no
  equivalent of `withRefreshLock` (a `SET NX EX` distributed lock), which is the load-bearing
  method. Swapping trades a tested abstraction for a leaky one.
- **`packages/styles/branding.ts`** — not build-time codegen; it renders a theme block *per request*
  so a per-OAuth-client `themeJson` can overlay the fork palette. style-dictionary can't express that.
- **`packages/testing`**, **`packages/ui` primitives**, **`use-is-desktop.ts`** (correct
  `useSyncExternalStore` usage), **`resolveSsrInternalOrigin`**.

**`packages/web-shell`'s host runtime is *not* on this list.** Phase 3 item 23 deletes it, and
that is now the expected outcome rather than a contingency. It is listed here only to say: it is
well-documented and its non-obvious choices have recorded reasons, so **port that reasoning across
during the migration** instead of rediscovering it.

---

## Sequencing summary

Phase 0 (security), the `AnyRouter` fix, and the `bff-server.ts` comment correction are
independent of everything and should go first. Phase 1 (operationIds) gates Phase 2. The Phase 3
spike is a confirmation, not a decision point, but still run it before extracting
`createPassthroughProxy` — Phase 3 step 19 partly subsumes that extraction, which would otherwise
be deleted a week later. Phase 4 depends on Phase 1. Phase 5 depends on Phase 2.

### Claims in this document that were verified against the repo, not assumed

Re-deriving these wastes a session; they are settled.

- `@tanstack/react-start` is declared by all three apps and imported nowhere (grep, repo-wide).
- Start server routes exist as `createFileRoute`'s `server.handlers`, via a module augmentation in
  `@tanstack/start-client-core` — `bff-server.ts`'s comment saying otherwise is wrong.
- Route typesafety is fully erased by `createRouter(): AnyRouter` — proven with a `tsc` probe plus
  a control file, not inferred.
- #7614 Bug 1 applies and is cured by `installDevServerMiddleware: true`; Bug 2 does not apply,
  because the plugin and both apps share one Vite resolution.
- The second Vite copy is a pnpm optional-peer artifact of `@types/node`, reachable only from
  `packages/sdk`. Aligning it is hygiene.
- Vite+ *causes* #7614 Bug 2 (#7218, #6982). It is not a workaround.
