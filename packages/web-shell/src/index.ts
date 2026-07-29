/**
 * Browser-safe barrel for @bc-solutions-coder/web-shell — the package's only
 * entry point.
 *
 * Everything exported here is importable from client-side bundles, so it must
 * stay free of Node APIs. The Node-only `./server` subpath that once held the
 * hand-rolled SSR host runtime is gone: TanStack Start owns hosting now.
 */
export { createQueryClient } from "./query-client";
