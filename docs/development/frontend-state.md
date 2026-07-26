# Frontend State: TanStack Query vs. Zustand

Wallow's React apps (`apps/wallow-auth`, `apps/wallow-web`) split client state across exactly two
stores with a hard boundary between them. Getting this boundary right keeps the cache
authoritative for anything the API can answer and keeps ephemeral UI state out of the network
layer entirely.

## The two stores

**TanStack Query is the backend-data store.** It is the single source of truth for anything that
came from — or is derived from — an API response: organizations, members, apps, settings, MFA
status, the current user. Every one of those has exactly one `queryKey`, defined once in
`@bc-solutions-coder/sdk/query`'s key registry, so every `useQuery(xQueries.y())` call anywhere in
either app resolves to the *same* cache entry — there is no per-component duplicate fetch and no
way for two screens to disagree about whether an organization was just archived. Freshness is
controlled by `staleTime` per query (see [`userQueries.currentUser()`](#the-current-user-query)
below), and cache invalidation is explicit: a mutation's `onSuccess` invalidates the exact key(s)
its write affects.

**Zustand is the UI-only store.** It holds state that has no server representation and no
`queryKey`: sidebar collapsed/expanded, which step a multi-step wizard is on, whether a modal is
open, the active tab. If a piece of state can be re-derived by calling the API again, it does not
belong in a Zustand store — it belongs in a `queryOptions`/`mutationOptions` factory instead.

## The rules

- **No inline `queryKey` literals anywhere.** Every key comes from
  `@bc-solutions-coder/sdk/query`'s `queryKeys` registry (`packages/sdk/src/query/keys.ts`).
  Writing `useQuery({ queryKey: ["orgs", id], ... })` by hand in an app is not allowed, even for a
  one-off screen — it silently forks the cache from every other call site that reads the same
  data through `organizationsQueries.detail(id)`.
- **Invalidate via the same factory.** A mutation's `onSuccess` calls
  `queryClient.invalidateQueries({ queryKey: queryKeys.<domain>.<key>(...) })` using the identical
  key builder the corresponding query uses — never a re-typed literal. This is what makes
  definition and invalidation impossible to let drift apart.
- **Mutations use the SDK's mutation factories where they exist.** Domain mutations
  (`createOrganizationMutation`, `addMemberMutation`, `archiveOrganizationMutation`, etc.) live
  next to their query siblings in `@bc-solutions-coder/sdk/query` and already close over the
  `QueryClient` to invalidate the right key on success. A component calls the factory; it does not
  hand-roll a `useMutation` with its own inline `mutationFn` and cache wiring.
- **The current user is `userQueries.currentUser()`.** Both apps read the signed-in user through
  this one query (keyed off `queryKeys.auth.currentUser()`) rather than each maintaining its own
  auth-state store — see [The current user query](#the-current-user-query) below.
- **One-time secrets live in component/mutation state, in NEITHER store.** MFA enrollment's QR
  code and TOTP secret, or a freshly minted OAuth client secret, are shown to the user exactly
  once and must never be cached or persisted: they are not backend data with a stable identity
  (a second fetch mints a *new* secret, it doesn't return the old one), and they are not
  reusable UI state either. Keep them in local component state (or the resolved value of a
  `useMutation` call) that unmounts with the screen — see `apps/wallow-auth/src/features/mfa-enroll/
  components/MfaEnrollForm.tsx`, which threads the enrollment secret and QR URI through `useState`
  scoped to the enroll flow, never through Zustand or the query cache.

### The current user query

```ts
// packages/sdk/src/query/user.ts
export const userQueries = {
  currentUser: () =>
    queryOptions({
      queryKey: queryKeys.auth.currentUser(),
      queryFn: (): Promise<WallowUser | null> => {
        ensureQueryBootstrapped();
        // ...SSR-aware fetch of the signed-in user, or null if anonymous.
      },
      staleTime: 30_000,
    }),
};
```

The 30-second `staleTime` exists so a router `beforeLoad` calling `ensureQueryData` on every
navigation does not refetch the user on each route change — the cache, not a separate auth store,
is what both apps treat as "am I signed in, and as whom."

## How to add a query

Every domain query/mutation lives in `@bc-solutions-coder/sdk/query`, keyed from
`packages/sdk/src/query/keys.ts`, and is re-exported through the feature's `api.ts` so app code
never imports the SDK's query layer directly. `organizations` is the canonical template every
later vertical (`apps`, `settings`, `mfa`, `inquiries`, `user`, `auth`) copies — a new query
follows the same three steps.

1. **Add the key** to `queryKeys` in `packages/sdk/src/query/keys.ts`, built from its parent so an
   `invalidateQueries` on the parent key sweeps the whole subtree:

   ```ts
   export const queryKeys = {
     organizations: {
       all: ["orgs"] as const,
       detail: (id: string) => [...queryKeys.organizations.all, id] as const,
       members: (id: string) => [...queryKeys.organizations.detail(id), "members"] as const,
     },
     // ...
   };
   ```

2. **Add the `queryOptions` factory** to the domain module (e.g. `packages/sdk/src/query/
   organizations.ts`), starting the `queryFn` with `ensureQueryBootstrapped()` and calling the
   generated operation through `unwrap(...)`:

   ```ts
   export const organizationsQueries = {
     members: (id: string) =>
       queryOptions({
         queryKey: queryKeys.organizations.members(id),
         queryFn: () => {
           ensureQueryBootstrapped();
           return unwrap(getV1IdentityOrganizationsByIdMembers({ path: { id } }));
         },
       }),
   };
   ```

   A mutation that writes that data follows the same pattern, closing over the `QueryClient` to
   invalidate the matching key on success:

   ```ts
   export const addMemberMutation = (queryClient: QueryClient, orgId: string) => ({
     mutationFn: (body: AddMemberBody): Promise<unknown> => {
       ensureQueryBootstrapped();
       return unwrap(postV1IdentityOrganizationsByIdMembers({ path: { id: orgId }, body }));
     },
     onSuccess: (): void => {
       void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.members(orgId) });
     },
   });
   ```

3. **Re-export through the feature's `api.ts`.** The feature folder (e.g.
   `apps/wallow-web/src/features/organizations/api.ts`) stays a thin re-export seam so routes and
   components keep importing from `./api` — the "api.ts is the only data import" convention holds
   even though the query keys and invalidation logic live centrally in the SDK:

   ```ts
   export {
     organizationsQueries,
     addMemberMutation,
     // ...
   } from "@bc-solutions-coder/sdk/query";
   ```

A component then calls `useQuery(organizationsQueries.members(id))` or
`useMutation(addMemberMutation(queryClient, id))` — never a hand-rolled key, and never a
`fetch`/SDK operation call outside the `queryFn`/`mutationFn` closures above.

## See also

- [Frontend Setup](frontend-setup.md) — app bootstrap, shared packages, styling, and testing.
- [TypeScript SDK](../integrations/typescript-sdk.md) — the SDK's three entry points and the BFF
  session model the query layer's `queryFn`s run inside.
