/**
 * Native form shell with schema-owned validation and shared submit state.
 */

import { type FormEvent, type ReactElement, type ReactNode, useMemo } from "react";

import { AppFormContext, type AppFormContextValue } from "./app-form-context";
import type { WallowFormExtras } from "./use-app-form";

/**
 * The minimal form surface the shell needs — satisfied by any TanStack Form
 * instance (its `handleSubmit` also accepts an optional submit-meta argument,
 * which is assignable to this narrower type).
 */
export interface AppFormInstance {
  /** Validate the form and run its configured submission callback. */
  handleSubmit: () => Promise<void>;
  /**
   * Present when the instance came from `useAppForm`, which is where `pending`
   * and `serverError` already live. The shell falls back to these so a call site
   * does not repeat `pending={form.wallow.pending}` on every form; it stays
   * optional so a plain `useForm` instance still renders through the shell.
   */
  readonly wallow?: WallowFormExtras;
}

/**
 * Native form shell options. Explicit pending and serverError props override values from
 * form.wallow.
 */
export interface AppFormProps {
  readonly form: AppFormInstance;
  /** The prefix every child derives its testid from, e.g. `"inquiry"`. */
  readonly testIdPrefix: string;
  /**
   * Override the form element test ID, which defaults to {testIdPrefix}-form. Child IDs still
   * derive from testIdPrefix.
   */
  readonly testId?: string;
  /**
   * Whether a submit is in flight. Omit it for a `useAppForm` instance, which
   * already knows: the shell then reads `form.wallow.pending`.
   */
  readonly pending?: boolean;
  /**
   * The form-level server error. Omit it for a `useAppForm` instance, which
   * already knows: the shell then reads `form.wallow.serverError`.
   */
  readonly serverError?: string | null;
  readonly children: ReactNode;
  /** Replaces the shell's default vertical rhythm. */
  readonly className?: string;
}

/** The default vertical rhythm, replaced wholesale by a caller `className`. */
const DEFAULT_RHYTHM = "space-y-5";

/**
 * Render a native form and provide shared state to catalog fields, FormError, and SubmitButton.
 * Prevents browser submission and native validation, clears previous server errors, then invokes
 * form.handleSubmit().
 */
export function AppForm({
  form,
  testIdPrefix,
  testId,
  pending,
  serverError,
  children,
  className,
}: AppFormProps): ReactElement {
  /*
   * A `useAppForm` instance already tracks both, so a call site does not repeat
   * `pending={form.wallow.pending}` on every form. An explicitly passed prop
   * still wins — including an explicit `null`, which is why `serverError` is
   * compared against `undefined` rather than coalesced.
   */
  const resolvedPending: boolean = pending ?? form.wallow?.pending ?? false;
  const resolvedServerError: string | null =
    serverError === undefined ? (form.wallow?.serverError ?? null) : serverError;

  const context = useMemo<AppFormContextValue>(
    () => ({ testIdPrefix, pending: resolvedPending, serverError: resolvedServerError }),
    [testIdPrefix, resolvedPending, resolvedServerError],
  );

  function handleSubmit(event: FormEvent<HTMLFormElement>): void {
    // `preventDefault` keeps the native submit from navigating away from the
    // SPA; `stopPropagation` keeps a form nested inside another submit handler
    // from firing it too. `handleSubmit` is deliberately not awaited — the
    // result is observed through the mutation, never by the submit handler.
    event.preventDefault();
    event.stopPropagation();
    // Before validation, not after: `handleSubmit` aborts on a field that is
    // still carrying the previous submit's server error, so clearing from
    // inside the form's own `onSubmit` would be too late (see
    // `WallowFormExtras.clearServerErrors`).
    form.wallow?.clearServerErrors();
    void form.handleSubmit();
  }

  return (
    <AppFormContext.Provider value={context}>
      {/* `noValidate`: the schema owns validation, so the browser must not
          double-validate a `type="email"` control and pop a native bubble. */}
      <form
        data-testid={testId ?? `${testIdPrefix}-form`}
        className={className ?? DEFAULT_RHYTHM}
        onSubmit={handleSubmit}
        noValidate
      >
        {children}
      </form>
    </AppFormContext.Provider>
  );
}
