import { ErrorCode, type FailureMessageRegistry } from "@bc-solutions-coder/api-errors";
import { Button, Card, MutedText, Text, useFailureMessage } from "@bc-solutions-coder/ui";
import { useQuery } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";

import { accountVerifyEmailOptions } from "../api";
import { signInHref } from "../sign-in-href";
import { decideReturnUrl } from "@shared/lib/return-url";

/**
 * The VerifyEmailConfirm screen (Wallow-vec7.3.3).
 *
 * `email`, `token`, and `returnUrl` arrive as props rather than being read from
 * the router inside the component: the route owns the query string (the oracle's
 * three `[SupplyParameterFromQuery]` properties) and hands them down, which
 * keeps this component a pure function of its inputs and testable without a
 * router. This is the seam `ResetPasswordForm` established.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `verify-email-confirm-loading`, `verify-email-confirm-success`,
 * `verify-email-confirm-continue`, `verify-email-confirm-error`,
 * `verify-email-confirm-signin-link`.
 *
 * Mutations call the GENERATED operations and reads use the generated
 * `{op}Options()` factories, both bound to the request-scoped SDK off the router
 * context (`useRouteContext({ from: "__root__" })`). The OIDC URL builders are
 * pure and imported directly. There is no app-level facade (Wallow-pu6a.5.5).
 *
 * ── HOW THE ORACLE'S ERROR SWITCH IS PORTED ───────────────────────────────────
 *
 * The oracle switches its message on `result.Error`:
 *
 *     "invalid_token" => "The verification link is invalid or has expired."
 *     _               => "Failed to verify email. Please try again."
 *
 * `AccountController.VerifyEmail` answers RFC 7807 problems: `Auth.TokenInvalid`
 * or `Auth.TokenExpired` for a link that cannot be redeemed (an unknown email
 * deliberately collapses into the same code, so the screen cannot be used to
 * enumerate users). The generated client THROWS on any non-2xx and the fetch
 * layer parses the body into an `ApiFailure`; the query's `error` is worded
 * through `useFailureMessage`, with the oracle's first branch as the call-site
 * `VERIFY_MESSAGES` and its tail as the `fallback`. A 5xx or a dead network
 * reads the model's own copy. The screen never narrows the rejection itself.
 *
 * ── SUCCESS IS "RESOLVED", NOT `succeeded === true` ──────────────────────────
 *
 * The oracle reads `result.Succeeded` off the body. Through this seam that read
 * is redundant and is deliberately not ported: every 200 from the endpoint is
 * `Ok(new { succeeded = true })` — there is no 200-with-false — and every falsy
 * case is a 400 that `unwrap()` has already turned into a throw. A resolved
 * promise IS success, so the untyped body is never inspected.
 */

/** The oracle's guard for a link missing either half of its identity. */
const INVALID_LINK_MESSAGE = "Invalid verification link. Missing required parameters.";

/** The oracle's `"invalid_token" =>` branch, reached here via the token problems. */
const EXPIRED_LINK_MESSAGE = "The verification link is invalid or has expired.";

/** The oracle's `catch` branch: any other failure, including a network-level one. */
const GENERIC_FAILURE_MESSAGE = "An error occurred while verifying your email. Please try again.";

/**
 * This screen's sentences, ahead of the app registry: a bad or expired token is
 * about the VERIFICATION link specifically, not the registry's generic "link".
 */
const VERIFY_MESSAGES: FailureMessageRegistry = {
  [ErrorCode.AUTH_TOKEN_INVALID]: () => EXPIRED_LINK_MESSAGE,
  [ErrorCode.AUTH_TOKEN_EXPIRED]: () => EXPIRED_LINK_MESSAGE,
};

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <Text as="h2" variant="subheading" color="onCard">
      Email Verification
    </Text>
  );
}

/** The oracle's `_loading` branch: a spinner and nothing else. */
function LoadingState() {
  return (
    <div
      className="flex items-center justify-center py-4"
      data-testid="verify-email-confirm-loading"
    >
      <Text as="span" variant="bodySm" color="muted">
        Verifying your email...
      </Text>
    </div>
  );
}

/** The oracle's success `BbAlert`. */
function SuccessAlert() {
  return (
    <div
      className="rounded-md border border-border bg-muted/40 p-3 space-y-1"
      data-testid="verify-email-confirm-success"
    >
      <Text as="p" variant="bodySm" weight="medium">
        Email verified!
      </Text>
      <MutedText>Your email has been verified. You can now sign in.</MutedText>
    </div>
  );
}

/**
 * The oracle's `IsSafe(ReturnUrl)`-gated Continue button.
 *
 * Unlike the footer's sign-in link (which merely FORWARDS returnUrl as a query
 * parameter), this one navigates straight to it — which is precisely what the
 * open-redirect guard exists to stop. An unsafe or absent returnUrl means there
 * is nowhere legitimate to continue to, so the button is simply not rendered and
 * the footer link is the way on.
 */
function ContinueButton({ returnUrl }: { readonly returnUrl: string }) {
  return (
    <Button
      render={<a href={returnUrl} />}
      nativeButton={false}
      data-testid="verify-email-confirm-continue"
    >
      Continue
    </Button>
  );
}

/** The oracle's success branch: the alert, plus a Continue when there is one. */
function SuccessState({ returnUrl }: { readonly returnUrl?: string }) {
  const destination = decideReturnUrl(returnUrl, "empty-ok");

  return (
    <div className="space-y-4">
      <SuccessAlert />
      {destination.verdict === "accept" ? (
        <ContinueButton returnUrl={destination.returnUrl} />
      ) : null}
    </div>
  );
}

/** The oracle's danger `BbAlert`, carrying one of the curated messages above. */
function ErrorState({ message }: { readonly message: string }) {
  return (
    <div
      className="rounded-md border border-destructive bg-destructive/10 p-3 space-y-1"
      data-testid="verify-email-confirm-error"
    >
      <Text as="p" variant="bodySm" color="destructive" weight="medium">
        Verification failed
      </Text>
      <Text as="p" variant="bodySm" color="destructive">
        {message}
      </Text>
    </div>
  );
}

/**
 * The oracle's `BbCardFooter` — outside its if/else, so it survives every state.
 * It is the one way out of the error state.
 */
function SignInLink({ returnUrl }: { readonly returnUrl?: string }) {
  return (
    <div className="text-center w-full">
      <a
        href={signInHref(returnUrl)}
        data-testid="verify-email-confirm-signin-link"
        className="text-sm text-muted-foreground hover:text-foreground"
      >
        Go to sign in
      </a>
    </div>
  );
}

/**
 * The oracle's three mutually-exclusive states, chosen the way its
 * if/else-if/else chain does. Split out so the card below stays flat.
 */
function VerificationState(props: {
  readonly email?: string;
  readonly token?: string;
  readonly returnUrl?: string;
}) {
  const { email, token, returnUrl } = props;
  const { sdk } = useRouteContext({ from: "__root__" });

  // The oracle's `IsNullOrEmpty(Token) || IsNullOrEmpty(Email)`, which runs
  // BEFORE its try block: an empty string is a missing one, so `?token=` never
  // reaches the endpoint.
  const linkIsComplete: boolean =
    email !== undefined && email !== "" && token !== undefined && token !== "";

  // The generated query type takes both parameters as optional, so the props
  // pass straight through: `enabled` below is what keeps an incomplete link off
  // the wire, not a `?? ""` placeholder.
  const query = useQuery({
    ...accountVerifyEmailOptions({ client: sdk.client, query: { email, token } }),
    // The untyped body is deliberately discarded — see the note on success
    // above. "Resolved" is the whole success signal.
    select: (): null => null,
    // Carries the oracle's guard to React Query: a malformed link short-circuits
    // to the error state without ever going to the network. A screen that
    // "helpfully" sent `token: undefined` would 400 and blame the user's link
    // for its own bug.
    enabled: linkIsComplete,
  });

  // Resolved unconditionally (hooks may not sit behind the early returns below)
  // and read only in the error branch; `null` in every other state.
  const failureMessage: string | null = useFailureMessage(query.isError ? query.error : null, {
    messages: VERIFY_MESSAGES,
    fallback: GENERIC_FAILURE_MESSAGE,
  });

  // Checked before `isPending`, which is also true for a disabled query: the
  // missing-parameter path has no request to wait on, so the user must never be
  // told we are "verifying your email".
  if (!linkIsComplete) {
    return <ErrorState message={INVALID_LINK_MESSAGE} />;
  }

  if (query.isPending) {
    return <LoadingState />;
  }

  if (query.isError) {
    return <ErrorState message={failureMessage ?? GENERIC_FAILURE_MESSAGE} />;
  }

  return <SuccessState returnUrl={returnUrl} />;
}

export interface VerifyEmailConfirmProps {
  /** The `email` query parameter — `undefined` when the link omits it. */
  readonly email?: string;
  /** The `token` query parameter — `undefined` when the link omits it. */
  readonly token?: string;
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
}

export function VerifyEmailConfirm({
  email,
  token,
  returnUrl,
}: VerifyEmailConfirmProps): ReactNode {
  return (
    <Card>
      <CardHeading />
      <VerificationState email={email} token={token} returnUrl={returnUrl} />
      <SignInLink returnUrl={returnUrl} />
    </Card>
  );
}
