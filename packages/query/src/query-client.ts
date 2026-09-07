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
   * Receives each unhandled mutation failure and opted-in query failures. Query failures are
   * reported once until that query succeeds; mutation reports are not deduplicated. Use
   * handledFailure for mutations and toastedFailure for queries to set the reporting flags.
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
 * Return new metadata that suppresses the client failure callback for a mutation. Preserves the
 * caller metadata without mutating it. Rendering the failure remains the caller responsibility.
 */
export function handledFailure<T extends Meta>(meta?: T): T & { failureHandled: true } {
  return { ...meta, [FAILURE_HANDLED]: true } as T & { failureHandled: true };
}

/**
 * Return new metadata that opts a query into the client failure callback. Preserves the caller
 * metadata without mutating it. Queries without this flag remain silent.
 */
export function toastedFailure<T extends Meta>(meta?: T): T & { toastFailure: true } {
  return { ...meta, [TOAST_FAILURE]: true } as T & { toastFailure: true };
}

/**
 * Create an independent QueryClient with query and mutation retries disabled. Use one per browser
 * app and a new one for each SSR request. The optional callback receives unhandled mutation
 * failures and opted-in query failures, once per query failure streak until success.
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
