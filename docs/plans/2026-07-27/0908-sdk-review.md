**status: active**

> Coordination note (2026-07-27): the TanStack Start migration design
> (`0909-tanstack-start-migration-design.md`, section "Coordination with the SDK
> refactor") analyzes how this plan's phases interleave with the Start migration —
> including two collision points (SSR request context, query dehydration ownership)
> and a recommended merged sequence. Read both before turning either into a unified
> implementation plan.

# SDK Review — Architecture, Codegen, and Auth Security

Four-agent parallel review of `packages/sdk` plus the auth stack, run 2026-07-27. Goal:
assess the SDK against the fork-first vision (install the package, build a whole app against a
Wallow backend with near-zero custom glue), find unnecessary hand-rolled code, and ground the
"BFF-only, no browser tokens" stance in current best practice.

Reviewers: SDK architecture/DX, codegen pipeline, auth security audit, standards research.

---

## 1. The one finding that dominates everything

**All 149 API operations lack an `operationId`, and 67 of 149 (45%) declare no typed success
body.** Two reviewers found this independently.

Consequences:

- hey-api falls back to path-derived names (`getV1IdentityOrganizationsByIdMembers`). Unusable
  in app code, so `auth-client.ts` (376 lines) and most of `src/query/` (773 lines) exist
  largely to rename them.
- Those names are **unstable**. Renaming a route silently renames an exported SDK function —
  an unannounced breaking change for every fork, with no compiler signal on the producing side.
  The drift workflow catches that the *spec* moved; nothing flags that the *SDK surface* broke.
- Untyped responses are why `mfa-client.ts` hand-writes five response interfaces and 19 of 22
  `auth-client.ts` methods return `Promise<unknown>`.

**Fix:** one OpenAPI operation transformer alongside the four existing ones in
`api/src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs:45-48`, reading
`ControllerActionDescriptor.MethodInfo.Name`. The controllers already carry the right names
(`GetAnnouncements`, `DismissAnnouncement`). Fixes all 149 at once; effort S.

Do this before any TypeScript refactor — it changes the names the query plugin would generate,
and you want to absorb that rename exactly once.

Good news from the spec audit: schema names are clean PascalCase, all 26 tags are populated, and
every 4xx/5xx response carries a content schema. The `ProducesResponseType` discipline is paying off.

---

## 2. Security — three critical findings, all in the backend seed path

The TypeScript BFF is **clean**. No code path in `packages/sdk`, `apps/wallow-web`, or
`apps/wallow-auth` puts a token in a browser: zero `localStorage`/`sessionStorage` use, zero
client-side `Authorization` construction, and `auth-oidc.ts` (flagged as suspicious by name)
is pure URL string-building plus an open-redirect guard. Nothing to remove there.

The remembered "SPA tokens" decision is real, but it lives in **seed data**.

| # | Sev | Finding | Location |
| --- | --- | --- | --- |
| 1 | Critical | `wallow-dev-client` is a **public (secret-less)** OIDC client with ~35 scopes including every `.manage`, and is in `FirstPartyClients` so it skips consent. Any browser can run PKCE against it and hold access + ID + refresh tokens. **Unused in-repo — safe to delete.** | `api/seed.json:183`; `appsettings.json:78` |
| 2 | Critical | Scope→permission expansion is never validated against the caller's role. An `IScopeSubsetValidator` exists but is wired only into `ApiKeysController`, never `AuthorizationController`. Chains with #1: a plain `user` requests `roles.write users.manage` and receives admin-tier permissions. | `PermissionExpansionMiddleware.cs:39,75-83`; `ScopePermissionMapper.cs:12,16,17` |
| 3 | Critical | **Fail-open client type**: "public" means "no secret supplied" rather than an explicit flag. `docker-compose.production.yml:278-280` injects secrets by array index — a missing or misindexed env var silently registers the confidential BFF client as public, with no error. | `PreRegisteredClientOptions.cs:25` |
| 4 | High | `X-Tenant-Id` override accepts **any** `sa-`-prefixed client, not just operators — but every tenant's service account is `sa-`-prefixed (`OpenIddictServiceAccountService.cs:28`). A naming convention is doing an authorization decision's job. Mitigated: needs direct API access; the BFF proxy strips the header. | `TenantResolutionMiddleware.cs:31-47,99-103` |
| 5 | High | Global `admin` role + `RolesUpdate` lets one tenant's admin mint additional global admins with cross-tenant reach. **Design question** — depends on whether `admin` is intended as a realm superadmin. | `RolePermissionMapping.cs:19`; `UsersController.cs:142-178` |
| 6 | Med | **Open redirect** on `/bff/login?returnTo=` — taken verbatim, sealed into the tx cookie, redirected to after login. The SDK already ships the correct guard (`isSafeReturnUrl`) and `wallow-auth` uses it; the BFF just never calls it. One-line fix. | `handlers.ts:326-327,416` |
| 7 | Med | Cookie session store: `destroy()` is a no-op **and** iron's `ttl` defaults to `0`, so the sealed blob never expires cryptographically. A stolen cookie is replayable for the refresh token's full 7-day life and survives logout. Production uses Valkey (safe); the cookie store is the SDK **default for forks**. | `store/cookie.ts:44-46`; `session.ts:55` |
| 8 | Med | `AppsController` honours a caller-supplied `ClientType: "public"`. | `AppsController.cs:76` |
| 9 | Med | `DisableTransportSecurityRequirement()` is unconditional in all environments. | `IdentityInfrastructureExtensions.cs:117,128` |
| 10 | Low | No `__Host-`/`__Secure-` cookie prefix (blocked in dev by `COOKIE_SECURE=false`; needs to be conditional). | `config.ts:110` |
| 11 | Low | Proxy target built via `new URL(stripped, base)` — the classic SSRF escape shape. **Verified not exploitable today** (h3 collapses `//` before the handler sees it), but safety rests on a dependency's normalization. `auth-server.ts:150` shows the robust pattern. | `proxy.ts:601-603` |
| 12 | Low | No path allowlist on `/api/**` (reaches `/connect/*`); `/bff/logout` is a GET with no CSRF check (logout CSRF). | `proxy.ts`; `handlers.ts:431` |
| 13 | Low | No `FallbackPolicy` — a new controller without `[Authorize]` is silently anonymous. | `Program.cs:325-329,532` |
| 14 | Low | client_credentials `tenant_id` parsed from the client_id string, truncated; currently inert. | `TokenController.cs:139-147` |

### What the BFF gets right

Worth defending in review: confidential client enforced at startup (`config.ts:106`); full
protocol validation delegated to openid-client with `expectedState`/`expectedNonce`/PKCE
verifier; no session fixation (fresh `sessionId` + CSRF token minted at callback); **excellent
proxy header hygiene** — `proxy.ts:608-614` builds a fresh `Headers` forwarding only
`content-type` and `accept`, so inbound `Authorization`/`Cookie`/`X-Tenant-Id`/`X-Forwarded-*`
are dropped; timing-safe CSRF comparison; credential-aware log redaction; careful refresh
rotation under a lock that adopts a peer's session rather than double-spending a one-time token.

Multi-tenancy is solid: tenant comes from the validated `org_id` claim only, issued after a
server-side membership check. `TenantAwareDbContext` applies a query filter to every
`ITenantScoped` entity across all seven modules, and the save interceptor reverts attempts to
mutate `TenantId`. No Dapper anywhere. IDOR spot-checks on Inquiries, Storage, and ApiKeys all pass.

### Unresolved, needs a test rather than a read

Whether EF's `QueryFilterRewritingExpressionVisitor` correctly rebinds the per-instance tenant
constant for the **compiled** queries in `StoredFileRepository`/`ServiceAccountRepository`
(`TenantAwareDbContext.cs:35-38`). The code comment asserts it does; blast radius if it doesn't
is cross-tenant data exposure. Worth a targeted integration test.

---

## 3. Standards check — the BFF-only stance

**Verdict: correct, and it is the top tier of the spec — but state it honestly as a policy choice.**

The IETF "OAuth 2.0 for Browser-Based Applications" BCP is **still an Internet-Draft**
(`draft-ietf-oauth-browser-based-apps-27`, 6 July 2026), in the RFC Editor queue with **no RFC
number assigned**. Correct this if any doc or commit message claims otherwise.

It ranks three patterns "in decreasing order of security": **BFF** ("strongly recommended for
business applications, sensitive applications, and applications that handle personal data") >
token-mediating backend > browser-based public client. So it *ranks* rather than *mandates* —
"BFF-only" is a policy choice layered on top, which is the right call for a fork-first platform
where every downstream deployment inherits the default.

The strongest argument for the stance is §5.1.3: even with perfectly protected browser tokens,
an XSS attacker can run a *silent* auth-code flow in a hidden iframe to mint entirely fresh
tokens. "There are no practical security mechanisms for frontend applications that counter this
attack scenario. Short access token lifetimes and refresh token rotation are ineffective." Only a
confidential-client BFF defeats it — the attacker gets a code they cannot exchange.

Supporting: **RFC 9700** (OAuth 2.0 Security BCP, Jan 2025) — PKCE MUST for public clients,
ROPC MUST NOT be used, refresh tokens for public clients MUST be rotated or sender-constrained,
access tokens SHOULD be audience-restricted.

**CSRF caveat worth acting on:** OWASP now classifies the *naive* double-submit cookie as
**discouraged** — bypassable by an attacker who can write cookies on the domain (vulnerable
sibling subdomain, DNS takeover, plaintext-HTTP injection). The stateless variant must be
HMAC-bound to session data. Wallow uses a synchronizer token in the session *plus* a
double-submit cookie, which is the stateful pattern OWASP still recommends — verify the binding
holds. `SameSite` alone is explicitly *not* a replacement.

**React Native: do not extend the cookie BFF to it.** RFC 8252 (external user agent — never an
embedded webview) + public client with PKCE + refresh rotation + **DPoP** (RFC 9449, now named
by FAPI 2.0 as one of two acceptable sender-constraining mechanisms), tokens in
Keychain/Android Keystore. Worth tracking but not building on: OAuth 2.0 for First-Party
Applications (`draft-ietf-oauth-first-party-apps-03`) adds an Authorization Challenge Endpoint
for browserless native login — still a draft.

Also: FedCM does not affect this architecture (you operate your own issuer, same-origin BFF);
it matters only if you add third-party social login. Passkeys land in OpenIddict +
`apps/wallow-auth` and leave the BFF untouched — note NIST treats cloud-synced passkeys as
AAL2, not AAL3. OAuth 2.1 is not close (WG milestone Dec 2026).

---

## 4. SDK architecture — what should be deleted

Hand-written non-test lines in the browser + query entries: **1,882**. Roughly **950 deletable**
(~51%), plus ~1,000–1,300 lines of co-located test. `src/server/**` (~2,200 lines) is genuinely
hand-written and should stay: OIDC, session sealing, CSRF, the refresh proxy — that is a security
protocol implementation, and it is the SDK's actual product. "Fully generated" should mean the
*operation surface* is generated, not the BFF.

**The query layer wraps 21 of 149 operations (14%).** hey-api's installed version (0.99.0) ships
official plugins for `@tanstack/query-core`, react, vue, svelte, solid, preact, and angular,
emitting `queryOptions`/`mutationOptions`/`queryKeys`/`infiniteQueryOptions` per operation —
factory-style, hooks opt-in and off by default, exactly the shape `src/query/` hand-writes.
Realistic target: ~100 hand-written lines (a curated invalidation map) replacing 773, covering
all 149 operations.

One decision to make deliberately: `src/query/keys.ts` preserves hierarchical keys for
prefix-invalidation sweeps; generated keys are flat single-object arrays. You cannot have both.
Recommendation: take the generated keys for fetching, keep a small curated module for the
handful of places needing subtree invalidation.

**`unwrap()` is a config flag, not code.** `facade.ts:37-43` and the private `unwrap` in
`auth-client.ts:313-321` both hand-roll envelope unwrapping that the generated client already
supports: `responseStyle: 'data'` + `throwOnError: true` (`generated/client/types.gen.ts:47,53`).
Prerequisite: unify the two apps' error contracts on `WallowError` — `wallow-web`'s
`features/mfa/errors.ts:38-45` reads raw `ProblemDetails`, and that divergence is the only
reason the `MfaUnwrap` injection seam exists.

**The singleton is not clean.** Three competing configuration authorities:
`runtime-config.ts:9-13` bakes in `baseUrl: "/api"` + `credentials: "include"`, then
`client.ts:26-31` re-applies the identical values via `setConfig`. `/api` is wallow-web's
topology hardcoded into the generated client — `wallow-auth` must override it with
`configureBffClient({ baseUrl: "/" })`, and every fork inherits a wrong default it must remember
to undo.

Worse, `wireCsrfInterceptor` and `wireSsrCookieInterceptor` register unconditionally, so calling
either twice double-registers. That non-idempotency spawned two independent once-guards
(`facade.ts:51-67` and `query/bootstrap.ts:17-28`) that apps must hand-chain in the right order —
documented explicitly at `apps/wallow-auth/src/lib/wallow-auth-sdk.ts:96-101`. And
`registerQueryBootstrap` re-arms `bootstrapped = false` on every call, so a second registration
silently re-runs the configurator: the exact double-wire it exists to prevent.

Plus an SSR hazard: `configureSsrClient` sets a **module-global** `baseUrl` from one request's
origin. On a server handling concurrent requests from different hosts, first-request-wins.

**Framework-agnostic? The values are; the imports are not.** Every slice imports `queryOptions`
and `QueryClient` from `@tanstack/react-query`, which is also the sole `peerDependency` — so a
Vue/Svelte/Solid consumer must install React to get objects that are pure data. Both symbols are
re-exports of `@tanstack/query-core`. Changing the import specifier is nearly free.

**React Native blockers**, in severity order: (1) the same-origin cookie premise itself —
`credentials: "include"` against a relative `/api` has no meaning in RN, whose native cookie jars
don't follow browser semantics; (2) `login()`/`logout()` assign `location.href`, which doesn't
exist in RN (the Node-side version of this already caused an SSR 500, worked around in
`dashboard/route.tsx`); (3) `ssr.ts` is inert dead weight.

**Fork DX today: ~2/10 for a fresh fork.** Standing up wallow-web requires ~420 lines of glue
before a single feature screen: the h3 BFF server assembly (153), SSR origin resolution (66),
`AsyncLocalStorage` scoping, client configurator + bootstrap chaining (45), QueryClient
dehydrate/hydrate wiring (~40), auth gate + hand-parsed admin claim (~30), and five re-export
seam files (70). Several carry multi-paragraph comments explaining bugs already hit in
production. And 114 of 149 operations have no query layer at all — a fork touching Storage,
Notifications, Announcements, or ApiKeys writes its own slices from scratch.

---

## 5. Recommended sequence

### Phase 0 — security, before anything else

1. Delete `wallow-dev-client` from `seed.json` and its `FirstPartyClients` entry (#1). One test
   fixture and one docs section reference it; nothing else.
2. Wire `IScopeSubsetValidator` into `AuthorizationController` (#2).
3. Explicit `"public": true` client flag + startup hard-fail when a non-`sa-` client resolves to
   public (#3).
4. Replace the `sa-` prefix check with a real operator flag (#4).
5. Call `isSafeReturnUrl` in the BFF login handler (#6) — one line.
6. Pass an explicit `ttl` to iron `seal`/`unseal`; document Valkey as required for production (#7).
7. Decide the global-`admin` design question (#5).

### Phase 1 — unblock codegen (one afternoon)

8. Operation transformer emitting `operationId` from the controller method name; regenerate;
   absorb the one-time rename across both apps.
9. Add `[ProducesResponseType(typeof(T), 200)]` to the 67 operations lacking a typed success body.
10. Exclude the `Test Support` endpoint from the v1 document (it currently ships in the SDK).
11. Rewrite `openapi-regen.test.ts` as an "every operation has an operationId" invariant — it is
    currently a dated snapshot assertion that would have caught this years ago.

### Phase 2 — collapse the hand-written surface

12. Add the `@tanstack/query-core` plugin to `openapi-ts.config.ts`; move the React peer dep to
    optional. Deletes ~410 lines of `src/query/` and unblocks non-React consumers.
13. Set `responseStyle: 'data'` + `throwOnError: true`; unify both apps on `WallowError`; delete
    both `unwrap` implementations and the `MfaUnwrap` seam.
14. Delete `auth-client.ts` down to a ~70-line `auth-extras.ts` keeping only the three genuine
    quirks: `getCurrentUser`'s 401-softening, the space-joined `scopes` shaping, and the
    `clientId` key-omission guard.
15. Replace the global client with `createWallowSdk(options)` constructing per call. Fixes the
    interceptor non-idempotency, both once-guards, the SSR cross-request bleed, and the baked-in
    `/api` default simultaneously.

### Phase 3 — fork-first ergonomics

16. Ship `createWallowBffServer(config)` from `./server` (absorbs ~120 lines of every fork's
    `bff-server.ts`).
17. Ship an SSR preset — `createSsrScope()` with internal-origin resolution built in. This code
    has been debugged twice; no fork should re-derive it.
18. Add browser-side claim helpers (`isAdmin`, `hasRole`, `getRoles`) so route guards stop
    hand-parsing the roles claim.
19. Separate credential/transport from the generated client — cookie+CSRF for web, DPoP bearer
    for native. **Do this before RN work starts**; retrofitting after browser assumptions spread
    is materially harder.

### Phase 4 — close the automation loop

20. Spike `OpenApiDocumentsDirectory` build-time emit. `Microsoft.Extensions.ApiDescription.Server`
    is already in the transitive graph. Caveat: `GetDocument.Insider` halts at `builder.Build()`,
    so `/alive` and `/events` (mapped after it) would likely be absent — and the drift check is a
    byte comparison. Resolve that delta before switching. Payoff: `openapi-drift.yml` drops
    Postgres, Valkey, and migrations, going from minutes to seconds.
21. Auto-regen on backend merge, committing with a `feat(sdk):` message. **release-please is
    already configured** for the `packages/sdk` component with `include-component-in-tag`, and
    `sdk-publish.yml` already triggers on `sdk-v*` — the loop is nearly closed already.

### Ongoing guardrails

22. Make cross-tenant access attempts a **failing test gate** on every resource-bearing endpoint
    (OWASP API1:2023 explicitly: "Do not deploy changes that make the tests fail"). Highest-leverage
    control for a fork-first platform, because forks inherit the tests with the code.
23. Deny-by-default function-level auth — add a `FallbackPolicy` so an endpoint without an explicit
    attribute fails closed.
24. Audience-restrict access tokens; split scopes per module so a route compromise doesn't yield
    platform-wide authority.
25. Document the BFF-only rule honestly in the fork guide: the BCP ranks rather than mandates, BFF
    is its top tier, and forks get a documented escape hatch (Curity-style token handler) with the
    §5.1.3 silent-flow rationale. A rule forks understand is a rule they keep.

### Toolchain note

Stay on hey-api. Orval is hooks-first (`useShowPetById` can't be called outside a component) with
a known `queryOptions` renaming bug; kubb force-emits a React hook per operation. hey-api's flat
tree-shakeable functions with query artifacts in a separate `.gen.ts` match the `.` / `./query`
exports split `package.json` already declares. Migrating would cost real effort to land somewhere
worse.

Minor: `packages/sdk/openapi-ts-error-*.log` records `Post-processor "Prettier" failed to run:
spawnSync prettier ENOENT` — the generator silently fails its format pass in a repo whose
formatter is oxfmt. Set `output.format: false`. Two error logs are sitting uncommitted in the
package root.
