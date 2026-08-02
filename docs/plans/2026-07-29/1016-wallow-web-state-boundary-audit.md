# wallow-web frontend state boundary audit

**status: completed**

Scope: `apps/wallow-web` against the state boundary rule in
`docs/development/frontend-state.md` (and the summary in `CLAUDE.md` →
"Frontend state boundary"). Nothing else was audited — no styling, no a11y, no
backend.

## Verdict

The rule is **followed in app code**. All four hard rules — no inline `queryKey`
literals, no hand-rolled factories, sweeps only through `invalidations`
predicates, and Zustand kept UI-only — hold across every feature, route and
component. Two real problems sit one level below the rule text, in how the app
*constructs the client* the generated factories key off:

1. **SSR-primed queries never hydrate** — the server and browser build their SDK
   with different `baseUrl` strings, so every SSR-primed cache entry is dead on
   arrival and refetches in the browser. Confirmed by executing the generated key
   builder, not by reading. **This is a live bug.**
2. **Two different query definitions share the current-user cache entry** — the
   guard's `currentUserQuery` and the plain generated `usersGetCurrentUserOptions`
   resolve to a byte-identical key but carry different `queryFn` semantics and
   different `staleTime`. Confirmed by hashing both keys.

Neither is a violation of the rule as written; both defeat what the rule exists
to buy.

---

## Finding 1 — SSR-primed query keys do not match the browser's (high)

`apps/wallow-web/src/start.ts:72` builds the SSR-side SDK with an **absolute**
base URL:

```ts
baseUrl: `${requestOrigin}${API_MOUNT}`,   // "http://localhost:3000/api"
internalOrigin,
```

`apps/wallow-web/src/router.tsx:63` builds the browser-side SDK with a
**relative** one:

```ts
const sdk: WallowSdk = readRequestSdk() ?? createWallowSdk({ baseUrl: BROWSER_API_BASE_URL }); // "/api"
```

The generated key embeds that string verbatim
(`packages/sdk/src/generated/@tanstack/react-query.gen.ts:20`):

```ts
baseUrl: options?.baseUrl || (options?.client ?? client).getConfig().baseUrl
```

The browser always takes the relative fallback: `getGlobalStartContext` compiles
to `createIsomorphicFn().client(() => undefined)` in the client bundle, so
`readRequestSdk()` is `undefined` on every page load — this is the normal
hydration path, not an edge case.

**Verified by execution** (both SDKs built from `packages/sdk/dist`, keys hashed
with TanStack's own `hashKey`):

```
SSR     key: [{"_id":"usersGetCurrentUser","baseUrl":"http://localhost:3000/api","tags":["Users"]}]
BROWSER key: [{"_id":"usersGetCurrentUser","baseUrl":"/api","tags":["Users"]}]
hashes equal: false
members hashes equal: false
control (same baseUrl, internalOrigin set) equal: true
```

Impact: every query primed during SSR — the current user on every gated page,
plus the organizations / apps / inquiries / settings loaders — dehydrates under a
key the browser never reproduces, so `setupRouterSsrQueryIntegration`'s handoff
silently no-ops and each one refetches on hydrate. This is precisely the
mechanism `frontend-state.md` describes `baseUrl`-in-the-key as existing to
enable.

`internalOrigin` is **not** the cause. The control line above shows that keeping
`baseUrl` relative while setting `internalOrigin` produces identical keys —
`createWallowSdk` already confines `internalOrigin` to its `fetch` closure
(`packages/sdk/src/create-sdk.ts:86-118`), exactly as documented.

**Fix:** pass `baseUrl: "/api"` on the server too, and let `internalOrigin` carry
the absolute self-fetch origin it already exists for. The server currently
duplicates the origin into both, and only the `internalOrigin` copy is needed to
make SSR self-fetch resolve.

**Why nothing caught it:** no test asserts key parity across the SSR boundary.
`grep` for `baseUrl|hydrat|dehydrat` across `apps/wallow-web/src/**/*.test.*`
returns nothing. A regression test that hashes a generated key from a
`start.ts`-shaped SDK against a `router.tsx`-shaped one would pin this.

## Finding 2 — two competing definitions over one current-user cache entry (medium)

Two definitions resolve to the **same** cache entry (verified: `hashKey` equal
for both):

- `apps/wallow-web/src/lib/current-user.ts:55-65` — `currentUserQuery()`, a
  hand-written `queryFn` over the generated key that softens 401 to `null`, adds
  `sub`, and sets `staleTime: 30_000`. Used by the gates in
  `routes/dashboard/route.tsx:42` and `routes/index.tsx`.
- The plain generated `usersGetCurrentUserOptions`, re-exported by
  `features/settings/api.ts` and used by
  `features/settings/components/ProfileSection.tsx:58` and the loader at
  `routes/dashboard/settings.tsx:36`. Its `staleTime` is `undefined` (verified),
  and its `queryFn` **throws** on 401 rather than resolving `null`.

`features/settings/api.ts` states the intent — "the same operation the
dashboard's auth guard reads, so both resolve to ONE cache entry rather than
two" — and the key half of that is true. What it misses is that sharing a key
while *not* sharing the options means the entry's behaviour depends on which
observer last serviced a fetch.

Two consequences, stated at the confidence each deserves:

- **Confirmed:** `ProfileSection` inherits no `staleTime` (the shared
  `createQueryClient` sets only `retry: false`, `packages/web-shell/src/query-client.ts:13-21`),
  so mounting `/dashboard/settings` refetches the user the 30-second policy was
  written to avoid.
- **Fragile, not confirmed broken:** the 401-softening and the added `sub` are
  guarantees only one of the two definitions makes. Which `queryFn` services a
  given refetch depends on TanStack Query observer ordering, which I did not
  trace to a specific reproducible failure. The guards in use today do not read
  `sub` (`requireAuth` only null-checks, `route-context.ts:123-131`; `isAdmin`
  reads roles, `claims.ts:118-120`), and the settings page is behind the auth
  gate, so I found **no live break** — but the anonymous-throw path is exactly
  the failure `current-user.ts`'s own header documents as the reason the
  softening exists.

**Fix:** have `currentUserQuery` spread the generated options
(`{ ...usersGetCurrentUserOptions({ client }), queryFn, staleTime }`) and route
settings through `currentUserQuery` too, so one definition owns the entry.

---

## What is compliant (positive evidence)

**No inline `queryKey` literals.** Across all of `apps/wallow-web/src`,
production code contains exactly one `queryKey:` property —
`lib/current-user.ts:57`, and it is built by the generated
`usersGetCurrentUserQueryKey({ client })`. No hand-rolled key factory or
`queryKeys` object exists. Every `features/*/api.ts` is a thin re-export seam
over `@bc-solutions-coder/sdk/query`; the names were checked against the real
generated exports.

**No hand-rolled mutation factories.** No `mutationFn` appears anywhere in app
code. Every write spreads a generated `{operation}Mutation({ client })` and adds
only `onSuccess`.

**Every sweep goes through the curated predicates.** All 12 production
`invalidateQueries` call sites pass `queriesWithTag(...)` or
`queriesForOperation(<generated>QueryKey(...))`. No prefix-array sweep, no bare
`invalidateQueries()`, and no `queriesForOperation` fed a hand-written `_id`
string.

**Zustand is UI-only.** `src/stores/ui-store.ts` is the only store in the app and
holds exactly `isNavCollapsed` and `isMobileNavOpen`. `ui-store.test.ts:153-167`
pins the store's key set, so server data creeping in fails a test. No React
context or `useReducer` exists anywhere in the app.

**One-time secrets are in neither store.** `RegisterAppForm.tsx` reads
`clientSecret` straight off `mutation.data` with no intermediate state — the
shape the docs recommend. `OrganizationDetail.tsx`'s client registration does the
same. `MfaEnrollFlow.tsx` copies the secret and QR URI into `useState` scoped to
the flow, populated only from the mutation's `onSuccess` and cleared on confirm.
None of these values ever gets a `queryKey`.

**Client binding is request-scoped.** `start.ts` mints one SDK per request
through Start middleware; `router.tsx` lifts it into router context; every
component threads `sdk.client` explicitly. There is no module-global client.

## Gaps worth filing (not rule violations)

- **Invalidation test coverage is uneven.** The `expectSwept` helper
  (`src/test/invalidation.ts`) runs the *real* predicate against a real generated
  key, which is a genuinely good harness — but it is applied to 8 of the 12 wired
  mutations. Not covered: `organizationsRemoveMember`, `organizationsArchive`,
  `organizationsReactivate`, `clientsCreate`, and `mfaRegenerateBackupCodes`
  (which shares a tested helper, so it is lower risk). All five are correct in
  production today; they are simply unprotected against a future edit.
- **`clientBrandingUpsertBrandingMutation`** is exported through
  `features/apps/api.ts:13` and identity-pinned in its test, but has no call site
  — `RegisterAppForm`'s branding section is a commented structural placeholder.
  Worth confirming it is intentionally dormant.
- **`routes/bff-demo.tsx:75-104`** keeps auth status and email in local `useState`
  fed by a raw `getUser()` call, rather than the generated current-user query.
  Read literally that is a second auth-state store; the file is an intentional
  low-level BFF demonstration, not a feature screen, and nothing else reads that
  state. Flagged for a judgement call rather than fixed.

## Method

Four read-only agents audited one slice each (key sourcing; invalidation; store
boundary and secrets; client binding and SSR). Nothing went into this report on
an agent's say-so: every finding above was re-derived directly — the two
headline findings by executing the generated key builder against SDKs built the
way `start.ts` and `router.tsx` build them and comparing `hashKey` output, the
compliance claims by direct grep over `apps/wallow-web/src` and reading the
cited lines. One agent claim (a `sub`-related guard break) was **downgraded**
after reading `requireAuth` and `isAdmin` showed neither reads `sub`.
