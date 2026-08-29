# packages/auth — @bc-solutions-coder/auth Agent Guide

The **shared authn/authz layer**: the one canonical answer to "who is signed in" for every
app in this workspace, plus the role/permission helpers that gate UI on it.

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

The SDK re-exports exist so an app's auth imports come from ONE package, and they are
re-exported **by reference identity** (`src/index.test.ts` pins it). That spec also pins the
surface in both directions — a dropped export fails and so does a widened one; anything new
needs a deliberate addition there.

## Canonical current-user semantics

Each decision is pinned by `src/current-user.test.ts`; do not "simplify" any of them away:

1. **The GENERATED query key** — `usersGetCurrentUserQueryKey({ client })`. A hand-rolled key
   is invisible to the SDK's `invalidations` predicates, so invalidations never reach it.
2. **A 401 is the ANSWER "anonymous", not a failure** — the SDK's `getCurrentUser` resolves
   `null`. Only 401 is soft; a 500 must reach the caller, or a backend outage signs every
   real user out.
3. **`sub`, renamed from the API's `id`** — makes the resolved user satisfy the SDK's
   `WallowUser` so `requireAuth` can read it. Falls back to `""` when the API answers without
   an `id`, because `WallowUser.sub` is non-optional.
4. **A 30-second `staleTime`** (`CURRENT_USER_STALE_TIME_MS`, module-private — deliberately
   not exported), paired with `ensureCurrentUser`'s `ensureQueryData` (not `fetchQuery`), so
   a `beforeLoad` running on every navigation does not refetch the user per route change.

Every call takes the **request-scoped** client (`context.sdk.client`), never a module-global
one — that instance carries the session cookie and the internal origin SSR needs.

## Boundaries

- **No router import.** Nothing here imports `@tanstack/react-router` (`src/index.test.ts`
  scans for it). `useCurrentUser` and `ensureCurrentUser` take the client and query client as
  arguments; screens get both off their router context.
- **Query facade.** Every react-query symbol arrives through `@bc-solutions-coder/query`,
  never `@tanstack/react-query` directly — a second react-query copy in a consumer's graph
  throws at runtime.
- **One user model.** `hasRole`/`hasPermission`/`isAdmin` over the typed `CurrentUser` are
  the ONE role/permission surface at the app boundary. Raw OIDC claim decoding lives in the
  SDK **server** entry's internal `server/claims.ts` — never an app import.

## Casing mirrors the server

- **Roles are case-INsensitive** — `ClaimsPrincipalExtensions.GetRoles()` deduplicates with
  `StringComparer.OrdinalIgnoreCase`; `isAdmin` is defined as `hasRole(user, "admin")` so the
  two cannot disagree.
- **Permissions are case-SENSITIVE** — `PermissionAuthorizationHandler` decides with an
  ordinal `Contains`. Answering leniently would render a control the API then refuses.

Both trim the looked-up name, and both answer `false` for anonymous or claimless users rather
than throwing — every call site is a UI gate, and a gate that throws takes the screen down.
Neither is an authorization decision; the API re-checks every request.

## Tests

Node-environment vitest, co-located in `src/` (`vitest run`); no browser project — what a
screen does with loading/anonymous/signed-in states belongs to the app suites that own those
screens. **Nothing is mocked**: the specs drive a real `createWallowSdk()` over a stub
transport through a real `createQueryClient()`, so the real generated operation, the real
`WallowError` interceptor and the real 401-softening are under test. No SDK build is needed —
in-repo its `exports` map resolves to `src/`.

Scripts: `pnpm --filter @bc-solutions-coder/auth build` (Vite lib mode +
`tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`.
