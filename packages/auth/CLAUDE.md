# packages/auth — @bc-solutions-coder/auth Agent Guide

The shared authn/authz layer: the one canonical "who is signed in" answer plus the
role/permission helpers that gate UI on it. One browser-safe entry (`src/index.ts`).

## Current-user semantics (pinned by `src/current-user.test.ts`)

- **The GENERATED query key** — `usersGetCurrentUserQueryKey({ client })`. A hand-rolled key
  is invisible to the SDK's `invalidations` predicates, so invalidations never reach it.
- **A 401 resolves `null` ("anonymous"); ONLY 401 is soft** — a 500 must propagate, or a
  backend outage signs every real user out.
- 30-second `staleTime` (module-private, deliberately unexported), paired with
  `ensureCurrentUser`'s `ensureQueryData` (not `fetchQuery`), so a per-navigation
  `beforeLoad` does not refetch the user.
- Every call takes the **request-scoped** client (`context.sdk.client`), never a
  module-global one — it carries the session cookie and the internal origin SSR needs.

## Boundaries

- **No router dependency, by design** — the manifest declares none. `useCurrentUser` and
  `ensureCurrentUser` take the client and query client as arguments; screens get both off
  their router context.
- `src/index.test.ts` pins the surface in both directions AND the SDK re-exports' reference
  identity — anything new needs a deliberate addition there.
- Raw OIDC claim decoding lives in the SDK **server** entry, never an app import;
  `hasRole`/`hasPermission`/`isAdmin` over `CurrentUser` are the one app-boundary surface.

## Casing mirrors the server

- **Roles are case-INsensitive** — the server's `GetRoles()` dedupes with `OrdinalIgnoreCase`.
- **Permissions are case-SENSITIVE** — `PermissionAuthorizationHandler` uses an ordinal
  `Contains`; answering leniently would render a control the API then refuses.
- Both answer `false` for anonymous/claimless users rather than throw — a UI gate that throws
  takes the screen down. Neither authorizes anything; the API re-checks every request.

Tests: node-only vitest; nothing mocked — a real `createWallowSdk()` over a stub transport.
