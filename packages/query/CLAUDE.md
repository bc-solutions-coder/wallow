# packages/query — @bc-solutions-coder/query Agent Guide

## Why the wildcard re-export is pinned by identity

Two react-query copies in one graph give two `QueryClientProvider` contexts — a `useQuery`
from copy B inside a provider from copy A throws "No QueryClient set" at runtime.
`src/index.test.ts` therefore pins `facade[name] === tanstack[name]` (same bindings, not
wrappers), deriving the expected surface from the installed package, in **both** directions —
a dropped re-export fails, and so does an accidentally widened one. New helpers need a
deliberate addition to `FACADE_ADDITIONS`. Do not "simplify" the spec.

## `createQueryClient` (`src/query-client.ts`) — the policy is the contract

- **`retry: false`** by default, for queries and mutations alike — deterministic tests, no silent backoff.
- **A fresh client per call** — one SSR request never shares cache with another.
- **`onUnhandledFailure({ kind, error })`** is the ONE hook for failures nobody rendered, and the
  callback receives exactly those two members. The `MutationCache` calls it for every mutation
  whose `meta` lacks `failureHandled: true`; the `QueryCache` calls it only for queries whose
  `meta` carries `toastFailure: true` — queries are silent by default because a loader or
  banner usually owns them — and only **once per failure streak** (a `WeakSet` of failed
  queries, cleared on the next success), so focus and reconnect refetches of a query that stays
  broken do not stack identical toasts. `handledFailure(meta?)` and `toastedFailure(meta?)` set those flags
  and spread over existing `meta` without mutating it.
- **This package knows nothing about `ui`, the registry, or toasts.** The app builds the
  callback with its registry in scope and calls `toastFailure` itself; `query` imports only
  `@tanstack/react-query`.

## Tests

`index.test.ts` also carries a `skipIf(no dist/)` block over the **built** entry; run
`pnpm --filter @bc-solutions-coder/query build` to arm it.
