# packages/query — @bc-solutions-coder/query Agent Guide

## Why the wildcard re-export is pinned by identity

Two react-query copies in one graph give two `QueryClientProvider` contexts — a `useQuery`
from copy B inside a provider from copy A throws "No QueryClient set" at runtime.
`src/index.test.ts` therefore pins `facade[name] === tanstack[name]` (same bindings, not
wrappers), deriving the expected surface from the installed package, in **both** directions —
a dropped re-export fails, and so does an accidentally widened one. New helpers need a
deliberate addition to `FACADE_ADDITIONS`. Do not "simplify" the spec.

## `createQueryClient` (`src/query-client.ts`) — the policy is the contract

- **`retry: false`** by default — deterministic tests, no silent backoff.
- **A fresh client per call** — one SSR request never shares cache with another.

## Tests

`index.test.ts` also carries a `skipIf(no dist/)` block over the **built** entry; run
`pnpm --filter @bc-solutions-coder/query build` to arm it.
