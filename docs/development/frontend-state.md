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
every `useQuery(organizationsGetAllOptions({ client }))` call anywhere in either app resolves to
the _same_ cache entry — there is no per-component duplicate fetch and no way for two screens to
disagree about whether an organization was just archived. Freshness is controlled by `staleTime`
per query (see [The current user query](#the-current-user-query) below), and cache invalidation is
explicit: a mutation's `onSuccess` sweeps the entries its write affects.

**Zustand is the UI-only store.** It holds state that has no server representation and no
`queryKey`: sidebar collapsed/expanded, which step a multi-step wizard is on, whether a modal is
open, the active tab. If a piece of state can be re-derived by calling the API again, it does not
belong in a Zustand store — it belongs behind a generated options factory instead.

The repo has exactly **one** Zustand store today: `useNavStore` in
`packages/navigation/src/nav-store.ts`, exported by
[`@bc-solutions-coder/navigation`](#the-navigation-store). `import { create } from "zustand"`
appears in that one file and nowhere else in `packages/*/src` or `apps/*/src`.

Three shared packages stand between an app and these stores, and all three are mandatory routes
rather than conveniences: [`@bc-solutions-coder/query`](#the-query-facade) is where react-query
itself comes from, [`@bc-solutions-coder/auth`](#the-shared-auth-package) owns the one query
that answers "who is signed in", and `@bc-solutions-coder/navigation` owns the nav store.

## The navigation store

`useNavStore` (`packages/navigation/src/nav-store.ts`) is the shell's global nav state and the
model for any UI store a fork adds. Three things about it are load-bearing:

- **Two independent axes, never derived from each other.** `isNavCollapsed` is desktop-only —
  whether the persistent rail is narrowed to icons. `isMobileNavOpen` is mobile-only — whether the
  overlay drawer is showing. Collapsing the rail must not close a drawer, and opening the drawer
  says nothing about how the rail should look when the viewport grows back.
- **It is a store rather than `useState` because of the component tree, not the data.** The
  controls that flip these flags live in `AppShell`'s main column; the rail and drawer that read
  them are siblings. Neither can pass props to the other.
- **It is a module-global singleton**, so `zustand` is a **peer** dependency of
  `@bc-solutions-coder/navigation` and the store is exported from exactly one entry. Two resolved
  copies would mean two stores and a toggle that silently stops moving the rail — the same hazard
  class `@bc-solutions-coder/query` exists to solve for `QueryClient`.

Subscribe with a selector so a component only re-renders for the slice it reads:

```tsx
import { useNavStore } from "@bc-solutions-coder/navigation";

const isNavCollapsed = useNavStore((state) => state.isNavCollapsed);
const toggleNavCollapsed = useNavStore((state) => state.toggleNavCollapsed);
```

## The query facade

`@bc-solutions-coder/query` (`packages/query`) is the **one place TanStack Query enters this
workspace**. It has a single browser-safe entry, `.`, exporting two things:

- **the whole react-query surface, re-exported by reference** — `useQuery`, `useMutation`,
  `useQueryClient`, `QueryClient`, `QueryClientProvider`, `queryOptions`, everything else. The
  re-export is a wildcard on purpose: a hand-kept list would lag react-query, and the first
  missing symbol is where the facade starts eroding.
- **`createQueryClient()`** — the shared client factory every app wires into its router context
  and its `__root` `QueryClientProvider`. Its policy is the contract: `retry: false` (no silent
  backoff, deterministic tests) and a fresh client per call, so one SSR request never shares a
  cache with another.

**Import react-query symbols from `@bc-solutions-coder/query`, never `@tanstack/react-query`
directly.** That holds for the apps and for every other shared package — `forms`, `auth`,
`testing` all go through the facade too. Only `packages/query` itself declares the react-query
dependency.

The rule is machine-enforced rather than a convention: the repo-root `.oxlintrc.json` carries a
`no-restricted-imports` entry for `@tanstack/react-query`, so a direct import fails
`pnpm lint` (and CI) with a message pointing at the facade. The only exemptions are
`packages/query/**` itself and the handful of `packages/sdk` files that need react-query's
_types_ to describe the generated artifacts.

Two things go wrong without it. Version drift is the boring one; the sharp one is **identity** —
two react-query copies in one dependency graph mean two `QueryClientProvider` contexts, and a
`useQuery` from copy B mounted inside a provider from copy A throws `No QueryClient set` at
runtime, in the browser, with a stack that points at neither package.

```ts
// Every consumer — apps and shared packages alike.
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
```

The same import from the raw package fails with the rule's own message: _"Import it from
`@bc-solutions-coder/query` instead. The facade owns the pinned version and the shared client
defaults, and keeps one `QueryClient` context instance across the workspace."_

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

## The shared auth package

`@bc-solutions-coder/auth` (`packages/auth`) owns the workspace's auth state — who is signed in,
what they may do, and the `beforeLoad` primer routes gate on. One browser-safe entry, `.`:

| Export                                       | What it is                                                                                                                             |
| -------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `currentUserQuery(client)`                   | The canonical `queryOptions` for the signed-in user; resolves `null` when anonymous.                                                   |
| `useCurrentUser(client)`                     | `useQuery(currentUserQuery(client))` — how a screen reads the user.                                                                    |
| `ensureCurrentUser({ queryClient, client })` | `ensureQueryData` over the same query, for a route's `beforeLoad`.                                                                     |
| `type CurrentUser`                           | The API's response plus the `sub` the SDK's claim helpers key off.                                                                     |
| `hasRole`, `hasPermission`                   | Membership over `CurrentUser.roles` / `.permissions`. Roles are case-insensitive, permissions case-sensitive — both mirror the server. |
| `requireAuth`, `loginRedirect`, `isAdmin`    | The SDK's route guards and claim helpers, re-exported **by reference** so an app's auth imports come from one package.                 |

**No app defines its own current-user query.** Before this package existed, wallow-web and
wallow-auth each carried one and they had already drifted (wallow-auth's copy had no `staleTime`
and no `sub`). Two definitions of "who is signed in" is what the package exists to end. Neither
is authorization: `hasRole`/`hasPermission` gate UI, and the API re-checks every role and
permission on every request.

Nothing here imports a router — `useCurrentUser` and `ensureCurrentUser` take the client and the
query client as arguments, which screens read off their router context.

### The current user query

Both apps read the signed-in user through `currentUserQuery` from `@bc-solutions-coder/auth`
rather than each maintaining its own auth-state store. It is the **generated** operation and the
**generated** key (`usersGetCurrentUserQueryKey`), so an invalidation raised anywhere in the app
reaches it:

```ts
import { currentUserQuery, ensureCurrentUser, useCurrentUser } from "@bc-solutions-coder/auth";

// In a component.
const { data: user } = useCurrentUser(sdk.client);

// In a route's `beforeLoad` — the same query, primed into the cache before the gate runs.
const gateUser = await ensureCurrentUser({ queryClient, client: sdk.client });
```

`useCurrentUser` **is** `useQuery(currentUserQuery(client))` and `ensureCurrentUser` **is**
`ensureQueryData` over the same options, so all three read one cache entry. Reach for
`currentUserQuery` yourself — with `useQuery` from `@bc-solutions-coder/query` — only when a call
site needs to override an option.

Two behavioural contracts its callers depend on:

- **A 401 is the answer "anonymous", not a failure.** The SDK's `getCurrentUser` softens it and
  the query resolves `null`, which is why a signed-out visitor reaches a route's login gate
  instead of its error boundary. **Only** 401 is soft — a 500 still reaches the caller, or a
  backend outage would sign every real user out.
- **A 30-second `staleTime`.** Paired with `ensureQueryData` (not `fetchQuery`) it is what keeps
  a `beforeLoad` running on every navigation from re-reading the user on each route change — the
  cache, not a separate auth store, is what both apps treat as "am I signed in, and as whom."

Pass the **request-scoped** client (`context.sdk.client`), never a module-global one: that
instance is what carries the session cookie and the internal origin an SSR render needs, so one
query works on both sides.

## How to add a query

There is no step for adding a key or a factory: both are generated. Adding a backend endpoint and
regenerating is what makes its query and mutation artifacts exist.

1. **Regenerate** after the OpenAPI document changes, and commit `openapi/v1.json` together with
   `packages/sdk/src/generated/**`. CI fails on drift between the snapshot and a live API.

2. **Re-export the artifacts you need through the feature's `api.ts` seam.** Each feature has one
   `src/features/<name>/api.ts` — a thin re-export over `@bc-solutions-coder/sdk/query`, listing
   the generated factories that feature uses plus the `invalidations` predicates. Routes and
   components import from `./api`, never from the SDK entry directly, so "`api.ts` is the only
   data import" stays true and one file shows a feature's whole data surface:

   ```ts
   // src/features/organizations/api.ts
   export {
     organizationsAddMemberMutation,
     organizationsGetMembersOptions,
     organizationsGetMembersQueryKey,
     queriesForOperation,
     queriesWithTag,
   } from "@bc-solutions-coder/sdk/query";
   ```

3. **Call the generated factory** from the component or route loader, binding the request-scoped
   client. The hooks come from the facade, the factories from the seam:

   ```ts
   import { useQuery } from "@bc-solutions-coder/query";

   import { organizationsGetMembersOptions } from "../api";

   const members = useQuery(organizationsGetMembersOptions({ client: sdk.client, path: { id } }));
   ```

4. **Write through the matching mutation factory**, and sweep what the write invalidated:

   ```ts
   import { useMutation, useQueryClient } from "@bc-solutions-coder/query";

   import {
     organizationsAddMemberMutation,
     organizationsGetMembersQueryKey,
     queriesForOperation,
   } from "../api";

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
- [Component Library](component-library.md) — the `@bc-solutions-coder/ui` catalog the nav shell
  composes onto.
- [Forms](forms.md) — how a form submits through one of those generated mutation factories, and how
  the failure it returns is split between the field messages and the form-level banner.
- [TypeScript SDK](../integrations/typescript-sdk.md) — the SDK's four entry points and the BFF
  session model the query layer's `queryFn`s run inside.
- [Integration Cookbook](../integrations/integration-cookbook.md) — the `features/<name>/api.ts`
  seam convention these rules are consumed through, end to end.
