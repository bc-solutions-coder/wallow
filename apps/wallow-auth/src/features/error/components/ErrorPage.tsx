import { Card, ErrorBanner } from "@bc-solutions-coder/ui";
import type { ReactNode } from "react";

/**
 * The Error screen (Wallow-vec7.3.3).
 *
 * This screen is the destination of every other screen's open-redirect refusal
 * (`/error?reason=invalid_redirect_uri`, per bd memory
 * `returnurl-guard-refuse-dont-sanitize`), and of the OIDC flows' `not_a_member`
 * / `access_denied` / `invalid_request` bail-outs. Its `reason` mapping is
 * therefore a contract other beads depend on, not a private detail.
 *
 * `reason` arrives as a prop rather than being read from the router here: the
 * route owns the query string (the oracle's `[SupplyParameterFromQuery]`) and
 * hands it down, keeping this component a pure function of its inputs and
 * testable without a router — the seam `ResetPasswordForm` established.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `error-heading`, `error-message`, `error-sign-out-link`, `error-back-link`.
 */

/** The one reason that means "you are signed in, as the wrong person". */
const NOT_A_MEMBER = "not_a_member";

/** The oracle's `_` arm: the message for anything this page has not heard of. */
const GENERIC_MESSAGE = "An unexpected error occurred. Please try again.";

/**
 * The oracle's switch arms, verbatim.
 *
 * A `Map` rather than a plain object literal, deliberately: `reason` is
 * attacker-supplied (`/error?reason=<anything>` is a URL anyone can construct
 * and send to a victim), and a record lookup resolves inherited keys —
 * `?reason=toString` would find `Object.prototype.toString` and hand a function
 * to the renderer. `Map.get` only ever sees the keys put here.
 */
const REASON_MESSAGES: ReadonlyMap<string, string> = new Map([
  [NOT_A_MEMBER, "You don't have access to this application."],
  ["invalid_redirect_uri", "The redirect destination is not permitted."],
  ["access_denied", "Access was denied. Please try again or contact support."],
  ["invalid_request", "The request was invalid. Please try again."],
]);

/**
 * The oracle's `ErrorMessage` computed property.
 *
 * The reason is a routing key, never copy: an unrecognised one falls back to the
 * generic message rather than being echoed, so attacker-controlled query text
 * never reaches the DOM. A reason this page has never heard of must still
 * produce a page — this IS the error screen; it has nowhere to escalate to.
 */
function errorMessage(reason: string | undefined): string {
  if (reason === undefined) {
    return GENERIC_MESSAGE;
  }

  return REASON_MESSAGES.get(reason) ?? GENERIC_MESSAGE;
}

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <h2 className="text-lg font-semibold text-card-foreground" data-testid="error-heading">
      Something went wrong
    </h2>
  );
}

/** The oracle's danger `BbAlert`. */
function ErrorMessageAlert({ reason }: { readonly reason?: string }) {
  return <ErrorBanner data-testid="error-message">{errorMessage(reason)}</ErrorBanner>;
}

/**
 * The oracle's `reason == "not_a_member"`-gated escape hatch.
 *
 * Gated because `not_a_member` is the only case whose fix is to sign out — the
 * user is authenticated as somebody without access, so a home link alone would
 * loop them straight back into this same error. Signing a user out of a working
 * session because a redirect_uri was malformed would be a hostile non-sequitur,
 * so every other reason gets the home link only.
 */
function SignOutLink() {
  return (
    <a
      href="/logout"
      data-testid="error-sign-out-link"
      className="text-sm font-medium text-primary hover:text-primary/80"
    >
      Sign out and try a different account
    </a>
  );
}

/** The oracle's `BbCardFooter`: always a way home, sometimes a way out. */
function ErrorFooter({ reason }: { readonly reason?: string }) {
  return (
    <div className="flex flex-col items-center gap-2 w-full">
      {reason === NOT_A_MEMBER ? <SignOutLink /> : null}
      <a
        href="/"
        data-testid="error-back-link"
        className="text-sm text-muted-foreground hover:text-foreground"
      >
        Back to home
      </a>
    </div>
  );
}

export interface ErrorPageProps {
  /** The `reason` query parameter — `undefined` when the caller omits it. */
  readonly reason?: string;
}

export function ErrorPage({ reason }: ErrorPageProps): ReactNode {
  return (
    <Card>
      <CardHeading />
      <ErrorMessageAlert reason={reason} />
      <ErrorFooter reason={reason} />
    </Card>
  );
}
