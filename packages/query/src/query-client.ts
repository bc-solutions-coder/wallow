import { MutationCache, QueryCache, QueryClient } from "@tanstack/react-query";

/** What the client reports for a failure nothing else owned: which cache it came from, and the error. */
export interface UnhandledFailure {
  /** Whether a mutation or a query raised the error. */
  kind: "mutation" | "query";
  /** The rejection, exactly as the mutation or query function threw it. */
  error: unknown;
}

/** Options for {@link createQueryClient}. */
export interface CreateQueryClientOptions {
  /**
   * Called once per failure the app has not claimed: every mutation whose meta
   * does not carry {@link handledFailure}'s flag, and only the queries whose
   * meta carries {@link toastedFailure}'s. The app decides what to do with it —
   * this package knows nothing about toasts or message registries.
   */
  onUnhandledFailure?: (failure: UnhandledFailure) => void;
}

/** Meta a mutation or query may carry; the two flags below are the ones this package reads. */
type Meta = Record<string, unknown>;

/** Meta flag a mutation sets when its call site renders the failure itself. */
const FAILURE_HANDLED = "failureHandled";

/** Meta flag a query sets to route its failure to the client callback. */
const TOAST_FAILURE = "toastFailure";

/**
 * Mark a mutation's failure as owned by its call site (a form rendering the
 * banner, say), so the client callback stays quiet for it. Composes with
 * whatever `meta` the mutation already carries.
 */
export function handledFailure<T extends Meta>(meta?: T): T & { failureHandled: true } {
  return { ...meta, [FAILURE_HANDLED]: true } as T & { failureHandled: true };
}

/**
 * Opt a query into the client callback. Queries are silent by default — a
 * route loader or a banner usually owns their failure — so only the ones that
 * ask are reported. Composes with whatever `meta` the query already carries.
 */
export function toastedFailure<T extends Meta>(meta?: T): T & { toastFailure: true } {
  return { ...meta, [TOAST_FAILURE]: true } as T & { toastFailure: true };
}

/**
 * The single source of the React Query client wired into the router context
 * and the `__root` `QueryClientProvider`.
 *
 * Browser-safe (no Node APIs), so it lives in the package's `.` barrel — it is
 * imported from client-side bundles as well as SSR. It applies an explicit
 * query policy (retry disabled — deterministic tests, no silent backoff) and
 * mints a fresh client per call so an SSR request never shares cache with
 * another. The optional `onUnhandledFailure` is the one place an app hears
 * about failures nobody rendered.
 *
 * A query is reported once per failure streak, not once per fetch: a query
 * that keeps failing across focus and reconnect refetches would otherwise
 * raise the same toast each time. The streak ends at the next success.
 */
export function createQueryClient(options: CreateQueryClientOptions = {}): QueryClient {
  const report = options.onUnhandledFailure;
  const reported = new WeakSet<object>();

  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
      mutations: {
        retry: false,
      },
    },
    mutationCache: new MutationCache({
      onError: (error, _variables, _context, mutation) => {
        if (mutation.meta?.[FAILURE_HANDLED] === true) {
          return;
        }
        report?.({ kind: "mutation", error });
      },
    }),
    queryCache: new QueryCache({
      onError: (error, query) => {
        if (query.meta?.[TOAST_FAILURE] !== true || reported.has(query)) {
          return;
        }
        reported.add(query);
        report?.({ kind: "query", error });
      },
      onSuccess: (_data, query) => {
        reported.delete(query);
      },
    }),
  });
}
