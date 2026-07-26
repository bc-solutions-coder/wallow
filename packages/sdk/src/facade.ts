/**
 * Facade helper module (Wallow-0q2s.7.3).
 *
 * Both reference apps build a `getWallow*Sdk()` facade over the generated
 * `@hey-api` ops, and both hand-rolled the same two pieces of boilerplate:
 *
 *   - `unwrap()` — await a generated op's `{ data, error }` envelope and either
 *     return `data` on success or THROW the RAW `error` (RFC 7807
 *     `ProblemDetails`) on failure, so React Query surfaces the error and slice
 *     methods never leak `undefined`.
 *   - `createConfiguredOnce()` — the guarded-singleton wrapper that runs the
 *     one-time `configureBffClient` + interceptor wiring exactly once and then
 *     memoizes the built facade for every later call.
 *
 * These live in the SDK so both apps' facades collapse onto them and keep only
 * their app-specific slice definitions.
 *
 * DELIBERATE DIVERGENCE FROM `createAuthClient()`'s private `unwrap`: that helper
 * throws a typed {@link WallowError} parsed from the problem details, while this
 * one throws the RAW error object unchanged. wallow-web feature components read
 * the raw `ProblemDetails` shape directly (`(mutation.error as ProblemDetails)
 * .detail`), so the shared helper preserves the raw-throw semantics rather than
 * unifying onto the WallowError-throwing variant. See bd Wallow-0q2s.7.
 */

/** The `{ data, error }` envelope every generated `@hey-api` op resolves to. */
export interface SdkEnvelope<TData> {
  data?: TData;
  error?: unknown;
}

/**
 * Await a generated op and unwrap its `{ data, error }` envelope: return `data`
 * on success, THROW the raw `error` on failure. Only a defined `error` throws;
 * everything else resolves to `data`.
 */
export async function unwrap<TData>(pending: Promise<SdkEnvelope<TData>>): Promise<TData> {
  const { data, error } = await pending;
  if (error !== undefined) {
    throw error;
  }
  return data as TData;
}

/**
 * Wrap the one-time `configure` step and the facade `build` step into a
 * guarded-singleton getter: the first call runs `configure()` then `build()`,
 * memoizes the result, and every later call returns the same instance without
 * re-running either. Nothing runs until the getter is first invoked.
 */
export function createConfiguredOnce<TFacade>(
  configure: () => void,
  build: () => TFacade,
): () => TFacade {
  let facade: TFacade | undefined;
  let ready = false;

  return (): TFacade => {
    if (!ready) {
      configure();
      facade = build();
      ready = true;
    }

    return facade as TFacade;
  };
}
