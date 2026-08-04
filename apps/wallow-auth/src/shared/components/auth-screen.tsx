import { Card, CardHeader, ErrorBanner } from "@bc-solutions-coder/ui";
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
  /** Form-level failure copy. `null` or absent renders no banner at all. */
  readonly error?: string | null;
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
  error,
  errorTestId,
  footer,
  children,
  spacing,
}: AuthScreenProps): ReactElement {
  return (
    <Card spacing={spacing}>
      <CardHeader title={title} description={description} />
      <ScreenError error={error} testId={errorTestId} />
      {children}
      {footer}
    </Card>
  );
}
