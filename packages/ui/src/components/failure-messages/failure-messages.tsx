import { type FailureMessageRegistry, resolveFailureMessage } from "@bc-solutions-coder/api-errors";
import { createContext, type ReactElement, type ReactNode, useContext } from "react";

/**
 * The app's failure-message registry, published once at the root so every
 * surface below resolves the same sentence for the same code. The default is
 * empty: with no provider, `useFailureMessage` still answers, through
 * api-errors' shipped copy.
 */
const EMPTY_REGISTRY: FailureMessageRegistry = {};

const FailureMessagesContext = createContext<FailureMessageRegistry>(EMPTY_REGISTRY);

export interface FailureMessagesProviderProps {
  /** The app's registry, from `defineFailureMessages`. */
  readonly registry: FailureMessageRegistry;
  readonly children?: ReactNode;
}

/**
 * Publishes `registry` to the tree. A nested provider REPLACES the one above
 * it — there is no merging, so a feature that wants its own sentences passes
 * them per call site through `messages` instead of mounting a second provider.
 */
export function FailureMessagesProvider({
  registry,
  children,
}: FailureMessagesProviderProps): ReactElement {
  return (
    <FailureMessagesContext.Provider value={registry}>{children}</FailureMessagesContext.Provider>
  );
}

/** The per-call-site knobs of `useFailureMessage`, mirroring the resolver's. */
export interface UseFailureMessageOptions {
  /** Sentences for this call site alone; they win over the registry. */
  readonly messages?: FailureMessageRegistry | undefined;
  /** The call site's own last resort, ahead of the generic sentence. */
  readonly fallback?: string | undefined;
}

/**
 * The sentence to show for `error`, or `null` when there is no error to show —
 * a component can pass its query's `error` straight through.
 */
export function useFailureMessage(
  error: unknown,
  options: UseFailureMessageOptions = {},
): string | null {
  const registry: FailureMessageRegistry = useContext(FailureMessagesContext);

  if (error === null || error === undefined) {
    return null;
  }

  return resolveFailureMessage(error, {
    registry,
    messages: options.messages,
    fallback: options.fallback,
  });
}
