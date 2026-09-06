import type { FailureMessageRegistry } from "@bc-solutions-coder/api-errors";
import { Card, CardHeader, ErrorBanner, useFailureMessage } from "@bc-solutions-coder/ui";
import type { ReactElement, ReactNode } from "react";

/** The banner slot, split out to stay inside the app's `jsx-max-depth` budget. */
function ScreenError({
  error,
  testId,
}: {
  readonly error: string | null | undefined;
  readonly testId: string | undefined;
}): ReactElement | null {
  if (error === null || error === undefined) {
    return null;
  }

  return <ErrorBanner data-testid={testId}>{error}</ErrorBanner>;
}

export interface AuthScreenProps {
  readonly title: string;
  readonly description?: string;
  /**
   * The heading's testid, landing on the `<h2>` itself. Passed in for the same
   * reason `errorTestId` is: `{screen}-heading` is an existing per-screen
   * contract, not something a shell can derive.
   */
  readonly headingTestId?: string;
  /**
   * Form-level copy the screen already has in words — a guard's sentence, a
   * `?error=` parameter's. `null` or absent defers to `failure`; when both are
   * given, this wins.
   */
  readonly error?: string | null;
  /**
   * A rejected call, as thrown: the shell resolves its sentence through
   * `useFailureMessage` and the app registry. `null` or absent renders nothing.
   */
  readonly failure?: unknown;
  /** Sentences for this screen alone, ahead of the registry. */
  readonly messages?: FailureMessageRegistry;
  /**
   * The banner's testid. Passed in rather than derived: it is an E2E contract
   * each screen already owns, and eight distinct values are in use.
   */
  readonly errorTestId?: string;
  readonly footer?: ReactNode;
  /**
   * Optional, because a screen in a dead-end error state has no body at all —
   * `InvitationScreen` renders the banner and the way out and nothing between.
   */
  readonly children?: ReactNode;
  /** Overrides `Card`'s padding/rhythm block for the two measured outliers. */
  readonly spacing?: string;
}

/**
 * The skeleton all 16 wallow-auth screens open with: card surface, heading,
 * optional error banner, body, optional footer.
 *
 * The ORDER is the contract — every screen relied on it and none of them stated
 * it. An error banner rendered below the form is one a user scrolls past.
 *
 * App-local rather than catalog: the ordering and the error slot are this app's
 * composition, not a generic surface. The generic pieces it is built FROM
 * (`Card`, `CardHeader`, `ErrorBanner`) do live in the catalog.
 */
export function AuthScreen({
  title,
  description,
  headingTestId,
  error,
  failure,
  messages,
  errorTestId,
  footer,
  children,
  spacing,
}: AuthScreenProps): ReactElement {
  const failureMessage: string | null = useFailureMessage(failure, { messages });

  return (
    <Card spacing={spacing}>
      <CardHeader title={title} description={description} titleTestId={headingTestId} />
      <ScreenError error={error ?? failureMessage} testId={errorTestId} />
      {children}
      {footer}
    </Card>
  );
}
