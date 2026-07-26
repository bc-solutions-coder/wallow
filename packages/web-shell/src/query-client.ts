import { QueryClient } from "@tanstack/react-query";

/**
 * The single source of the React Query client wired into the router context and
 * the `__root` `QueryClientProvider` (moved from
 * apps/{wallow-auth,wallow-web}/src/lib/query-client.ts in Wallow-0q2s.8.2).
 *
 * Browser-safe (no Node APIs), so it lives in the package's `.` barrel — it is
 * imported from client-side bundles as well as SSR. It applies an explicit query
 * policy (retry disabled — deterministic tests, no silent backoff) and mints a
 * fresh client per call so an SSR request never shares cache with another.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
}
