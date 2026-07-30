# packages/query — @bc-solutions-coder/query Agent Guide

The **shared TanStack Query facade**: the one place `@tanstack/react-query` enters this
workspace. It re-exports react-query's entire runtime surface and adds exactly one symbol
of its own, `createQueryClient`.

## One entry, browser-safe by construction

| Entry                | Runs in | What it is                                                                       |
| -------------------- | ------- | -------------------------------------------------------------------------------- |
| `.` (`src/index.ts`) | Browser | `export * from "@tanstack/react-query"` plus `createQueryClient`. Must stay free |
|                      |         | of Node APIs; it is imported from client bundles as well as SSR.                 |

- **Never add a Node-only symbol here** — everything in this package is pulled into every
  consuming app's client bundle. There is no `./server` subpath to hide it behind.

## The facade rule

Every consumer — the apps, `packages/forms`, `packages/testing`, `packages/auth` — imports
react-query symbols (`useQuery`, `useMutation`, `QueryClient`, `QueryClientProvider`, …)
from `@bc-solutions-coder/query`, **never from `@tanstack/react-query` directly**. Only this
package declares react-query as a dependency; a repo-root oxlint `no-restricted-imports`
rule enforces the import side.

Two reasons it is a wildcard re-export pinned by reference identity (`src/index.test.ts`):

1. A hand-kept named list would silently lag react-query. The first consumer needing an
   unlisted symbol reaches for the raw package, and the facade erodes one import at a time.
   So the spec derives the expected surface from the installed package instead.
2. **Identity, not just presence.** Two copies of react-query in one graph give two
   `QueryClientProvider` React contexts, and a `useQuery` from copy B inside a provider from
   copy A throws "No QueryClient set" at runtime. `facade[name] === tanstack[name]` is what
   proves this package re-exports the same bindings rather than wrapping them.

The spec also asserts the surface in **both** directions — a dropped re-export fails, and so
does an accidentally widened one. This is a facade, not a grab bag: new helpers need a
deliberate addition to `FACADE_ADDITIONS`.

## `createQueryClient`

`src/query-client.ts` — the single source of the React Query client every app wires into its
router context and its `__root` `QueryClientProvider`. Its policy is the contract:

- **`retry: false`** by default — deterministic tests, no silent backoff.
- **A fresh client per call**, so one SSR request never shares cache with another.

## Tests

Node-environment vitest, co-located in `src/` (`vitest run`; the package has no browser
project):

- `index.test.ts` — the facade pin described above, plus a `skipIf(no dist/)` block that
  checks the **built** entry: `dist/index.js`'s runtime surface matches the source barrel
  (proving react-query still arrives through a live `export *` rather than being bundled in
  as a second copy) and `dist/index.d.ts` declares both react-query and `createQueryClient`.
  Run `pnpm --filter @bc-solutions-coder/query build` to arm those two.
- `query-client.test.ts` — the `createQueryClient` policy.
- `devtools-gating.test.ts` — the **repo-wide devtools sweep** lives here. It discovers every
  app under `apps/` and asserts no `*-devtools` package sits in an app's `dependencies` and no
  app module statically imports one (a dynamic `import()` behind a dev guard is the only
  permitted form). Its detectors are proven against a fixture tree first, so a green sweep is
  evidence of compliance rather than of a scanner that finds nothing. It binds all three apps,
  so no single app owns it; it lives here because the panels it gates are this client's and
  its router's. Keep exactly one copy.

Scripts: `pnpm --filter @bc-solutions-coder/query build` (Vite lib mode +
`tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`.
