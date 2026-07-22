/**
 * Browser-safe barrel for @bc-solutions-coder/web-shell.
 *
 * This entry is importable from client-side bundles (no Node APIs). It exposes
 * the shared React Query client factory (moved here in Wallow-0q2s.8.2); the
 * standalone-host runtime and other Node-only pieces live behind `./server`.
 */
export { createQueryClient } from "./query-client";
