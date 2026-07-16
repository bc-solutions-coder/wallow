import { QueryClient } from "@tanstack/react-query";

/**
 * The single source of the React Query client wired into the router context and
 * the `__root` `QueryClientProvider` (Wallow-vec7.1.4). It applies an explicit
 * query policy (retry disabled — deterministic tests, no silent backoff) and
 * mints a fresh client per call so an SSR request never shares cache with
 * another.
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
