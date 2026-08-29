# packages/auth — @bc-solutions-coder/auth Agent Guide

The **shared authn/authz layer**: the ONE canonical answer to "who is signed in" for every
app in this workspace, plus the role/permission helpers that gate UI on it. Before this
package existed, wallow-web and wallow-auth each carried their own current-user query and
they had already drifted apart (wallow-auth's copy had no `staleTime` and no `sub`).

## One entry, browser-safe by construction

| Entry                | Runs in | What it is                                                         |
| -------------------- | ------- | ------------------------------------------------------------------ |
| `.` (`src/index.ts`) | Browser | The whole surface below. No `./server` subpath, no Node-only code. |

## Surface

| Export                                                                    | From                                       | What it is                                                                           |
| ------------------------------------------------------------------------- | ------------------------------------------ | ------------------------------------------------------------------------------------ |
| `currentUserQuery(client)`                                                | `src/current-user.ts`                      | The canonical `queryOptions` for the signed-in user; resolves `null` when anonymous. |
| `type CurrentUser`                                                        | `src/current-user.ts`                      | `CurrentUserResponse & WallowUser` — the API's response plus `sub`.                  |
| `useCurrentUser(client)`                                                  | `src/use-current-user.ts`                  | `useQuery(currentUserQuery(client))`. The hook every screen reads the user through.  |
| `ensureCurrentUser(options)`                                              | `src/ensure-current-user.ts`               | `queryClient.ensureQueryData(currentUserQuery(client))`, for a route's `beforeLoad`. |
| `interface EnsureCurrentUserOptions`                                      | `src/ensure-current-user.ts`               | `{ queryClient, client }` — both request-scoped, off the router context.             |
| `hasRole(user, role)`                                                     | `src/authorization.ts`                     | Role membership over `CurrentUser.roles`. Case-INsensitive.                          |
| `hasPermission(user, permission)`                                         | `src/authorization.ts`                     | Permission membership over `CurrentUser.permissions`. Case-SENSITIVE.                |
| `isAdmin(user)`                                                           | `src/authorization.ts`                     | `hasRole(user, "admin")`, named — the one role gate any app renders.                 |
| `requireAuth`, `loginRedirect`                                            | re-exported from `@bc-solutions-coder/sdk` | The SDK's route guards, by reference — not wrapped.                                  |
| `type WallowUser`, `type RequireAuthOptions`, `type LoginRedirectOptions` | re-exported from `@bc-solutions-coder/sdk` | Their companion types.                                                               |

The SDK re-exports are here so an app's auth imports come from ONE package instead of being
split across two. They are re-exported **by reference identity** (`src/index.test.ts` pins
it): `requireAuth` from here IS the SDK's `requireAuth`.

`src/index.test.ts` also pins the surface in **both** directions — a dropped export fails,
and so does an accidentally widened one. Anything new needs a deliberate addition there.

## Canonical current-user semantics

Four decisions make `currentUserQuery` canonical rather than just another probe. Each one is
pinned by `src/current-user.test.ts`; do not "simplify" any of them away:

1. **The GENERATED key.** `queryKey: usersGetCurrentUserQueryKey({ client })`. A hand-rolled
   key would be invisible to `usersGetCurrentUserQueryKey` and to the SDK's `invalidations`
   predicates, so an invalidation raised anywhere in the app would never reach this query.
2. **A 401 is the ANSWER "anonymous", not a failure.** The SDK's `getCurrentUser` owns the
   softening and resolves `null`. Without it every signed-out visitor hits a route's error
   boundary instead of its login gate. Only 401 is soft — a 500 must reach the caller, or a
   backend outage would sign every real user out.
3. **`sub`, renamed from the API's `id`.** That is what makes the resolved user satisfy the
   SDK's `WallowUser` so `requireAuth` can read it. It is a rename, not an
   invention: `UsersController.GetCurrentUser` fills `Id` from `User.GetUserId()`, i.e. the
   very `sub` claim `AuthorizationController` issued. It falls back to `""` when the API
   answers without an `id`, because `WallowUser.sub` is non-optional.
4. **A 30-second `staleTime`** (`CURRENT_USER_STALE_TIME_MS`, module-private — deliberately
   NOT exported). Paired with `ensureCurrentUser`'s `ensureQueryData` it is what keeps a
   `beforeLoad` running on every navigation from re-reading the user on each route change.
   `ensureQueryData`, not `fetchQuery`, for the same reason.

Every one of these takes the **request-scoped** client (`context.sdk.client`) — never a
module-global one. That instance is what carries the session cookie and the internal origin
an SSR render needs, so one query works on both sides.

## No router import

Nothing in this package imports `@tanstack/react-router` (`src/index.test.ts` scans for it).
`useCurrentUser` and `ensureCurrentUser` take the client and the query client as arguments
precisely so this package needs no router dependency and stays testable outside one — the
same rule `packages/sdk/src/route-context.ts` already follows. Screens get both off their
router context.

## The query facade

Every react-query symbol arrives through `@bc-solutions-coder/query`, never from
`@tanstack/react-query` directly (`src/index.test.ts` pins both halves). A direct import here
would put a second react-query copy in a consumer's graph, and a `useQuery` from copy B
inside a `QueryClientProvider` from copy A throws at runtime.

## One user model — the SDK's claim-bag readers are deleted

This package's `hasRole`/`hasPermission`/`isAdmin` over the typed `CurrentUser` are the ONE
role/permission surface at the app boundary. The SDK used to export a second, same-named
family (`packages/sdk/src/claims.ts` — `hasRole`/`isAdmin`/`getRoles`/… walking a free-form
`WallowUser` claim bag); Wallow-j7qk deleted it, because two same-named `hasRole`s with
different semantics invited importing the wrong one. Raw OIDC claim decoding still exists,
but as the SDK **server** entry's internal `server/claims.ts` — never an app import.

Casing is not a style choice — it mirrors the server, because a browser helper that answers
differently from the API promises something the next request refuses:

- **Roles are case-INsensitive.** `ClaimsPrincipalExtensions.GetRoles()` deduplicates with
  `StringComparer.OrdinalIgnoreCase`. `isAdmin` is defined as `hasRole(user, "admin")`, so
  the two cannot disagree about one user.
- **Permissions are case-SENSITIVE.** `PermissionAuthorizationHandler` decides with a plain
  `permissions.Contains(requirement.Permission)` — ordinal. Answering leniently would render
  a control the API then refuses, which is the one direction that produces a broken screen
  rather than a hidden one.

Both trim the name being looked for (no role or permission the API issues has surrounding
whitespace, so it is always caller noise), both answer `false` for anonymous or claimless
users rather than throwing (every call site is a UI gate, and a gate that throws takes the
screen down instead of hiding a button), and neither is an authorization decision — the API
re-checks every role and permission on every request.

## Tests

Node-environment vitest, co-located in `src/` (`vitest run`). There is **no browser project**
(same posture as `packages/query`): `useCurrentUser` is a one-line `useQuery`, and what a
screen does with its loading/anonymous/signed-in states belongs to the app component suites
that own those screens.

**Nothing is mocked.** `current-user.test.ts` and `ensure-current-user.test.ts` drive a real
`createWallowSdk()` instance over a stub transport, through a real `QueryClient` from
`createQueryClient()` — so the assertions run through the real generated operation, the real
`WallowError` interceptor and the real 401-softening. A `vi.mock` of the SDK would assert
only that this package calls what it calls.

No SDK build is needed first — in-repo `@bc-solutions-coder/sdk`'s `exports` map resolves to
its `src/`, so this package typechecks against SDK source. `dist/` is a publish artifact,
needed only by `pnpm check:exports`.

Scripts: `pnpm --filter @bc-solutions-coder/auth build` (Vite lib mode +
`tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`.
