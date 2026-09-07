# @bc-solutions-coder/auth

Current-user queries and UI access checks for Wallow applications. This private
workspace package has one browser-safe entry and does not require a router.

## Setup

Use `QueryClientProvider` from `@bc-solutions-coder/query`. Pass the client from
`createWallowSdk()` to each user query or hook. A browser app can share its SDK and
query client; create both per request during SSR and forward the incoming cookie
to the SDK. See [SDK integration](../../docs/integrations/typescript-sdk.md).

```tsx
import { hasPermission, useCurrentUser } from "@bc-solutions-coder/auth";
import type { WallowSdk } from "@bc-solutions-coder/sdk";

export function AccountStatus({ sdk }: { sdk: WallowSdk }) {
  const user = useCurrentUser(sdk.client);
  if (user.isPending) return <p>Loading account...</p>;
  if (user.isError) return <p>Account lookup failed.</p>;
  if (user.data === null) return <p>Sign in to continue.</p>;
  return <p>{hasPermission(user.data, "UsersRead") ? "Can read users" : "Signed in"}</p>;
}
```

## Exports

| Export                                                     | Behavior                                                                                  |
| ---------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `currentUserQuery(client)`                                 | Generated current-user key, a 30-second stale time, and `sub` copied from the API user ID |
| `useCurrentUser(client)`                                   | React query result; loading data is undefined, anonymous data is null                     |
| `ensureCurrentUser({ queryClient, client })`               | Return existing cached data or fetch when absent, useful before rendering a route         |
| `hasRole(user, role)`, `isAdmin(user)`                     | Case-insensitive role checks; `isAdmin` checks the organization admin role                |
| `hasPermission(user, permission)`                          | Case-sensitive permission check                                                           |
| `isGlobalAdmin(user)`                                      | Read the separate global administrator flag                                               |
| `loginRedirect`, `requireAuth`                             | SDK URL and authentication guards, re-exported by reference                               |
| `CurrentUser`, `EnsureCurrentUserOptions`                  | User profile and cache-primer input types                                                 |
| `WallowUser`, `LoginRedirectOptions`, `RequireAuthOptions` | Re-exported SDK guard types                                                               |

Only HTTP 401 becomes null. Other failures remain errors so an API outage does
not appear as sign-out. `ensureCurrentUser` returns cached data even when stale;
use query invalidation/refetching when an account or authentication change needs
a fresh answer. The role and permission helpers return false for anonymous users
and blank names. They control presentation; the API still authorizes requests.
