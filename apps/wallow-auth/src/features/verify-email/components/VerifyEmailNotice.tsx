import { Card } from "@bc-solutions-coder/ui";
import type { ReactNode } from "react";

import { signInHref } from "../sign-in-href";

/**
 * The VerifyEmail "check your inbox" screen (Wallow-vec7.3.3).
 *
 * Named `VerifyEmailNotice` rather than `VerifyEmail` to keep it distinct from
 * its token-consuming sibling `VerifyEmailConfirm` (`/verify-email/confirm`);
 * both are owned by this bead and live in this feature folder.
 *
 * The screen is inert: no SDK call, no state, one computed href. It exists to
 * tell the user to go read their email. That href drops an unsafe `returnUrl` —
 * a deliberate deviation from the oracle, explained in full on {@link signInHref}.
 *
 * `returnUrl` arrives as a prop rather than being read from the router here: the
 * route owns the query string (the oracle's `[SupplyParameterFromQuery]`) and
 * hands it down, keeping this component a pure function of its inputs and
 * testable without a router — the seam `ResetPasswordForm` established.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `verify-email-heading`, `verify-email-description`, `verify-email-back-link`.
 */

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1">
      <h2 className="text-lg font-semibold text-card-foreground" data-testid="verify-email-heading">
        Check your email
      </h2>
      <p className="text-sm text-muted-foreground" data-testid="verify-email-description">
        We&apos;ve sent a verification link to your email address.
      </p>
    </div>
  );
}

/** The oracle's `BbCardContent` — including the spam-folder hint. */
function Instructions() {
  return (
    <p className="text-sm text-muted-foreground">
      Click the link in your email to verify your account. If you don&apos;t see it, check your spam
      folder.
    </p>
  );
}

/** The oracle's `BbCardFooter` — this screen's only link, and its way out. */
function BackToSignIn({ returnUrl }: { readonly returnUrl?: string }) {
  return (
    <div className="text-center w-full">
      <a
        href={signInHref(returnUrl)}
        data-testid="verify-email-back-link"
        className="text-sm text-muted-foreground hover:text-foreground"
      >
        Back to sign in
      </a>
    </div>
  );
}

export interface VerifyEmailNoticeProps {
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
}

export function VerifyEmailNotice({ returnUrl }: VerifyEmailNoticeProps): ReactNode {
  return (
    <Card>
      <CardHeading />
      <Instructions />
      <BackToSignIn returnUrl={returnUrl} />
    </Card>
  );
}
