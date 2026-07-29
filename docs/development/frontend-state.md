# Frontend State: TanStack Query vs. Zustand

Wallow's React apps (`apps/wallow-auth`, `apps/wallow-web`) split client state across exactly two
stores with a hard boundary between them. Getting this boundary right keeps the cache
authoritative for anything the API can answer and keeps ephemeral UI state out of the network
layer entirely.

## The two stores

**TanStack Query is the backend-data store.** It is the single source of truth for anything that
came from — or is derived from — an API response: organizations, members, apps, settings, MFA
status, the current user. Every one of those has exactly one key, and that key is **generated**:
`@bc-solutions-coder/sdk/query` emits a `{operation}Options()` factory, a `{operation}QueryKey()`
builder, and a `{operation}Mutation()` factory for every operation in the OpenAPI document, so
every `useQuery(usersGetCurrentUserOptions({ client }))` call anywhere in either app resolves to
the _same_ cache entry — there is no per-component duplicate fetch and no way for two screens to
disagree about whether an organization was just archived. Freshness is controlled by `staleTime`
per query (see [The current user query](#the-current-user-query) below), and cache invalidation is
explicit: a mutation's `onSuccess` sweeps the entries its write affects.

**Zustand is the UI-only store.** It holds state that has no server representation and no
`queryKey`: sidebar collapsed/expanded, which step a multi-step wizard is on, whether a modal is
open, the active tab. If a piece of state can be re-derived by calling the API again, it does not
belong in a Zustand store — it belongs behind a generated options factory instead.

## The generated key shape

A generated key is a **single-segment array holding one object**, not a hierarchical tuple:

```ts
organizationsGetByIdQueryKey({ client, path: { id } });
// [{ _id: "organizationsGetById", baseUrl: "/api", tags: ["Organizations"], path: { id } }]
```

`_id` is the operation name, `tags` are the operation's OpenAPI tags, and the call arguments
(`path`, `query`, `body`, `headers`) round out the identity. Two consequences follow, and both
drive the rules below:

- **There is no key prefix to invalidate by.** Nothing is built parent-from-child, so
  `invalidateQueries({ queryKey: ["orgs"] })` matches nothing. Subtree sweeps go through the
  curated `invalidations` helpers instead.
- **The key embeds `baseUrl`, not the transport.** A server-side instance and a browser instance
  built with the same `baseUrl` emit byte-identical keys, which is what lets an SSR-primed cache
  hydrate in the browser instead of refetching. `createWallowSdk`'s `internalOrigin` deliberately
  applies inside `fetch` only, so it never reaches the key.

## The rules

- **No inline `queryKey` literals anywhere.** Every key comes from the generated
  `{operation}QueryKey()` builder in `@bc-solutions-coder/sdk/query` — usually indirectly, because
  `{operation}Options()` already carries it. Writing `useQuery({ queryKey: ["orgs", id], ... })` by
  hand in an app is not allowed, even for a one-off screen: it silently forks the cache from every
  other call site that reads the same data.
- **Invalidate through `invalidations`, never by prefix.** `@bc-solutions-coder/sdk/query` exports
  two predicates over the flat keys, and they are the only supported sweeps:
  - `queriesWithTag(tag)` — every cached entry carrying that OpenAPI tag, i.e. everything one
    backend controller serves.
  - `queriesForOperation(exemplarKey)` — every cached entry for one operation whatever arguments
    it was called with. Pass a key built by that operation's `{operation}QueryKey()`, so no call
    site depends on the generator's internal `_id` spelling.
- **Bind every call to an explicit client.** Generated factories take `{ client }`; pass the
  request-scoped instance from `createWallowSdk(...)`. There is no module-global client to
  configure and nothing to bootstrap before first use.
- **Never re-implement a generated artifact.** If an operation is missing a factory, the fix is to
  regenerate (see [TypeScript SDK](../integrations/typescript-sdk.md)), not to hand-roll a
  `useMutation` with its own `mutationFn` and cache wiring.
- **One-time secrets live in component/mutation state, in NEITHER store.** MFA enrollment's QR
  code and TOTP secret, or a freshly minted OAuth client secret, are shown to the user exactly
  once and must never be cached or persisted: they are not backend data with a stable identity
  (a second fetch mints a _new_ secret, it doesn't return the old one), and they are not
  reusable UI state either. Keep them in local component state (or the resolved value of a
  `useMutation` call) that unmounts with the screen — see `apps/wallow-auth/src/features/mfa-enroll/
components/MfaEnrollForm.tsx`, which threads the enrollment secret and QR URI through `useState`
  scoped to the enroll flow, never through Zustand or the query cache.

### The current user query

Both apps read the signed-in user through one generated query rather than each maintaining its own
auth-state store. Per-query overrides are applied by spreading the generated options:

```ts
import { usersGetCurrentUserOptions } from "@bc-solutions-coder/sdk/query";

const currentUser = queryOptions({
  ...usersGetCurrentUserOptions({ client: sdk.client }),
  staleTime: 30_000,
});
```

The 30-second `staleTime` exists so a router `beforeLoad` calling `ensureQueryData` on every
navigation does not refetch the user on each route change — the cache, not a separate auth store,
is what both apps treat as "am I signed in, and as whom."

## How to add a query

There is no step for adding a key or a factory: both are generated. Adding a backend endpoint and
regenerating is what makes its query and mutation artifacts exist.

1. **Regenerate** after the OpenAPI document changes, and commit `openapi/v1.json` together with
   `packages/sdk/src/generated/**`. CI fails on drift between the snapshot and a live API.

2. **Call the generated factory** from the component or route loader, binding the request-scoped
   client:

   ```ts
   import { organizationsGetMembersOptions } from "@bc-solutions-coder/sdk/query";

   const members = useQuery(organizationsGetMembersOptions({ client: sdk.client, path: { id } }));
   ```

3. **Write through the matching mutation factory**, and sweep what the write invalidated:

   ```ts
   import {
     organizationsAddMemberMutation,
     queriesForOperation,
     organizationsGetMembersQueryKey,
   } from "@bc-solutions-coder/sdk/query";

   const queryClient = useQueryClient();
   const addMember = useMutation({
     ...organizationsAddMemberMutation({ client: sdk.client }),
     onSuccess: (): void => {
       void queryClient.invalidateQueries(
         queriesForOperation(organizationsGetMembersQueryKey({ client: sdk.client, path: { id } })),
       );
     },
   });
   ```

   Reach for `queriesWithTag("Organizations")` instead when a write invalidates a whole feature's
   worth of reads rather than one operation's.

## See also

- [Frontend Setup](frontend-setup.md) — app bootstrap, shared packages, styling, and testing.
- [TypeScript SDK](../integrations/typescript-sdk.md) — the SDK's four entry points and the BFF
  session model the query layer's `queryFn`s run inside.
- [Integration Cookbook](../integrations/integration-cookbook.md) — the `features/<name>/api.ts`
  seam convention these rules are consumed through, end to end.
