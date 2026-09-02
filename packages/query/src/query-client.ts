import { MutationCache, QueryClient } from "@tanstack/react-query";

/** PROTOTYPE (#168): what the client tells the app about a failure no screen claimed. */
export interface UnhandledFailure {
  readonly kind: "mutation";
  readonly error: unknown;
}

/** PROTOTYPE (#168): the one hook an app supplies; `query` itself renders nothing. */
export interface CreateQueryClientOptions {
  readonly onUnhandledFailure?: (failure: UnhandledFailure) => void;
}

/**
 * The single source of the React Query client wired into the router context and
 * the `__root` `QueryClientProvider` (moved from
 * apps/{wallow-auth,wallow-web}/src/lib/query-client.ts in Wallow-0q2s.8.2, then
 * out of the shared frontend-runtime package this one superseded, in
 * Wallow-x4qn.11 — this package is the shared TanStack Query facade).
 *
 * Browser-safe (no Node APIs), so it lives in the package's `.` barrel — it is
 * imported from client-side bundles as well as SSR. It applies an explicit query
 * policy (retry disabled — deterministic tests, no silent backoff) and mints a
 * fresh client per call so an SSR request never shares cache with another.
 *
 * PROTOTYPE (#168): a `MutationCache.onError` forwards every mutation failure to
 * `onUnhandledFailure` unless the mutation's `meta.failureHandled` is `true`.
 * Queries are deliberately NOT forwarded: a failed read already owns a banner,
 * and a toast on top would say the same thing twice.
 */
export function createQueryClient(options: CreateQueryClientOptions = {}): QueryClient {
  return new QueryClient({
    mutationCache: new MutationCache({
      onError: (error, _variables, _context, mutation): void => {
        if (mutation.meta?.["failureHandled"] === true) {return;}
        options.onUnhandledFailure?.({ kind: "mutation", error });
      },
    }),
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
}
