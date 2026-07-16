import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";
import { signInHref } from "../sign-in-href";

/**
 * The VerifyEmailConfirm screen (Wallow-vec7.3.3), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/VerifyEmailConfirm.razor`.
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
 * The API is reached through `getWallowAuthSdk()`, never `@bc-solutions-coder/sdk`
 * directly — that facade is this app's only permitted importer of the SDK.
 *
 * ── WHY THE ORACLE'S ERROR SWITCH IS NOT PORTED LITERALLY ─────────────────────
 *
 * The oracle switches its message on `result.Error`:
 *
 *     "invalid_token" => "The verification link is invalid or has expired."
 *     _               => "Failed to verify email. Please try again."
 *
 * That string does not survive the TS seam (bd memory `wallow-auth-screens-must-
 * map-sdk-errors-by-http-status`). `AccountController.VerifyEmail`
 * (api/src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs
 * :796-822) returns its failures as `BadRequest(new { succeeded = false,
 * error = "invalid_token" })` — a 400 whose body is a bare anon object, NOT
 * RFC 7807 problem details. `unwrap()` THROWS on any non-2xx, and
 * `toWallowError()` (packages/sdk/src/auth-client.ts:257-280) builds its `code`
 * from `extensions.code` ?? `code` only — it never reads a top-level `error`. So
 * the screen receives `WallowError{ status: 400, code: "UNKNOWN" }` and the
 * reason string is LOST.
 *
 * What survives is the HTTP status, and here that is enough: this endpoint has
 * exactly TWO failure returns (an unknown email, and a rejected
 * `ConfirmEmailAsync`) and BOTH are `400 + error: "invalid_token"`. A 400 from
 * this endpoint therefore *means* invalid_token, and the oracle's `_` arm is
 * unreachable through it. Non-400 rejections land on the oracle's `catch`
 * message rather than that unreachable arm — which is what the Blazor original
 * does for a 500 too (its `AuthApiClient` parses the error body, which throws on
 * a non-JSON 500 body and falls into `catch`).
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

/** The oracle's `"invalid_token" =>` branch, reached here via HTTP 400. */
const EXPIRED_LINK_MESSAGE = "The verification link is invalid or has expired.";

/** The oracle's `catch` branch: any other failure, including a network-level one. */
const GENERIC_FAILURE_MESSAGE = "An error occurred while verifying your email. Please try again.";

/**
 * The only failure status this endpoint distinguishes. Both of its failure
 * returns are `400 + error: "invalid_token"`, so a 400 from it *means* the link
 * is bad — see the seam note above.
 */
const INVALID_TOKEN_STATUS = 400;

/**
 * Map a rejection onto one of the oracle's two messages by HTTP status — see the
 * seam note above for why the reason string cannot be read instead.
 *
 * Narrowed structurally rather than with `instanceof WallowError`: that class is
 * exported from the SDK's `./server` entry, and screens may not import the SDK at
 * all. Defensive for the same reason — a network-level rejection carries no
 * `status`, and must fall through to the generic message rather than throw inside
 * the error branch or claim the link expired when it did not.
 */
function verifyFailureMessage(cause: unknown): string {
  if (typeof cause === "object" && cause !== null && "status" in cause) {
    const status: unknown = (cause as { readonly status: unknown }).status;

    if (status === INVALID_TOKEN_STATUS) {
      return EXPIRED_LINK_MESSAGE;
    }
  }

  return GENERIC_FAILURE_MESSAGE;
}

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return <h2 className="text-lg font-semibold text-card-foreground">Email Verification</h2>;
}

/** The oracle's `_loading` branch: a spinner and nothing else. */
function LoadingState() {
  return (
    <div
      className="flex items-center justify-center py-4"
      data-testid="verify-email-confirm-loading"
    >
      <span className="text-sm text-muted-foreground">Verifying your email...</span>
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
      <p className="text-sm font-medium text-foreground">Email verified!</p>
      <p className="text-sm text-muted-foreground">
        Your email has been verified. You can now sign in.
      </p>
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
    <a
      href={returnUrl}
      data-testid="verify-email-confirm-continue"
      className="block w-full rounded-md bg-primary px-3 py-2 text-center text-sm font-medium text-primary-foreground"
    >
      Continue
    </a>
  );
}

/** The oracle's success branch: the alert, plus a Continue when there is one. */
function SuccessState({ returnUrl }: { readonly returnUrl?: string }) {
  const canContinue: boolean =
    returnUrl !== undefined && getWallowAuthSdk().oidc.isSafeReturnUrl(returnUrl);

  return (
    <div className="space-y-4">
      <SuccessAlert />
      {canContinue && returnUrl !== undefined ? <ContinueButton returnUrl={returnUrl} /> : null}
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
      <p className="text-sm font-medium text-destructive">Verification failed</p>
      <p className="text-sm text-destructive">{message}</p>
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

  // The oracle's `IsNullOrEmpty(Token) || IsNullOrEmpty(Email)`, which runs
  // BEFORE its try block: an empty string is a missing one, so `?token=` never
  // reaches the endpoint.
  const linkIsComplete: boolean =
    email !== undefined && email !== "" && token !== undefined && token !== "";

  const query = useQuery({
    queryKey: ["verify-email", email, token],
    queryFn: async (): Promise<null> => {
      if (email === undefined || token === undefined) {
        // Unreachable: `enabled` gates this on `linkIsComplete`. Present only to
        // narrow the props to the `string`s the call takes, without a cast.
        return null;
      }

      await getWallowAuthSdk().auth.verifyEmail({ email, token });

      // The untyped body is deliberately discarded — see the note on success
      // above. "Resolved" is the whole success signal.
      return null;
    },
    // Carries the oracle's guard to React Query: a malformed link short-circuits
    // to the error state without ever going to the network. A screen that
    // "helpfully" sent `token: undefined` would 400 and blame the user's link
    // for its own bug.
    enabled: linkIsComplete,
    // A single-use token is not a retryable request: a 400 will never become a
    // 200, and retrying only delays telling the user their link is bad.
    retry: false,
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
    return <ErrorState message={verifyFailureMessage(query.error)} />;
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
    <div className="rounded-lg border border-border bg-card p-6 space-y-6">
      <CardHeading />
      <VerificationState email={email} token={token} returnUrl={returnUrl} />
      <SignInLink returnUrl={returnUrl} />
    </div>
  );
}
