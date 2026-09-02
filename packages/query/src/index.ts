/**
 * Browser-safe barrel for @bc-solutions-coder/query — the package's only entry
 * point, and the ONE place TanStack Query enters this workspace.
 *
 * **Facade rule:** every consumer — the apps, packages/forms, packages/testing,
 * packages/auth — imports react-query symbols (`useQuery`, `useMutation`,
 * `QueryClient`, `QueryClientProvider`, …) from `@bc-solutions-coder/query`,
 * never from `@tanstack/react-query` directly. A repo-root oxlint
 * `no-restricted-imports` rule enforces that; `src/index.test.ts` enforces the
 * other half — that the facade actually carries react-query's whole runtime
 * surface, by reference identity, so nobody has to reach past it.
 *
 * The re-export is a wildcard rather than a named list on purpose: a hand-kept
 * list would lag react-query, and the first missing symbol is where the facade
 * starts eroding. Identity matters too — two react-query copies in one graph mean
 * two `QueryClientProvider` contexts, and a `useQuery` from one inside a provider
 * from the other throws "No QueryClient set" at runtime.
 */
export * from "@tanstack/react-query";

export { createQueryClient } from "./query-client";

// PROTOTYPE (#168) — deleted with the branch.
export {
  failureReference,
  handledFailure,
  resolveFailureMessagePrototype,
} from "./failure-prototype";
export { type CreateQueryClientOptions, type UnhandledFailure } from "./query-client";
