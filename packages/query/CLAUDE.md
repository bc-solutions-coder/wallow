# packages/query — @bc-solutions-coder/query Agent Guide

The **shared TanStack Query facade**: the one place `@tanstack/react-query` enters this
workspace. It re-exports react-query's entire runtime surface and adds exactly one symbol of
its own, `createQueryClient`.

## One entry, browser-safe by construction

| Entry                | Runs in | What it is                                                                                                                                                                                 |
| -------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `.` (`src/index.ts`) | Browser | `export * from "@tanstack/react-query"` plus `createQueryClient`. No Node APIs — it is imported from client bundles as well as SSR, and there is no `./server` subpath to hide one behind. |

## The facade rule

Every consumer — the apps, `packages/forms`, `packages/testing`, `packages/auth` — imports
react-query symbols (`useQuery`, `QueryClient`, `QueryClientProvider`, …) from
`@bc-solutions-coder/query`, **never from `@tanstack/react-query` directly**. Only this
package declares react-query as a dependency; a repo-root oxlint `no-restricted-imports` rule
enforces the import side.

It is a wildcard re-export pinned by reference identity (`src/index.test.ts`):

1. A hand-kept named list would silently lag react-query and push consumers back to the raw
   package; the spec derives the expected surface from the installed package instead.
2. **Identity, not just presence.** Two react-query copies in one graph give two
   `QueryClientProvider` contexts, and a `useQuery` from copy B inside a provider from copy A
   throws "No QueryClient set" at runtime. `facade[name] === tanstack[name]` proves the same
   bindings are re-exported, not wrapped.

The spec asserts the surface in **both** directions — a dropped re-export fails, and so does
an accidentally widened one. New helpers need a deliberate addition to `FACADE_ADDITIONS`.

## `createQueryClient`

`src/query-client.ts` — the single source of the React Query client every app wires into its
router context and its `__root` `QueryClientProvider`. Its policy is the contract:

- **`retry: false`** by default — deterministic tests, no silent backoff.
- **A fresh client per call**, so one SSR request never shares cache with another.

## Tests

Node-environment vitest, co-located in `src/` (`vitest run`; no browser project):

- `index.test.ts` — the facade pin above, plus a `skipIf(no dist/)` block checking the
  **built** entry: `dist/index.js`'s runtime surface matches the source barrel (react-query
  arrives through a live `export *`, not bundled in as a second copy) and `dist/index.d.ts`
  declares both halves. Run `pnpm --filter @bc-solutions-coder/query build` to arm it.
- `query-client.test.ts` — the `createQueryClient` policy.

Devtools panels are reached by dynamic `import()` behind a dev guard, never a static import —
a static import ships the panel into every production bundle.

Scripts: `pnpm --filter @bc-solutions-coder/query build` (Vite lib mode +
`tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`.
