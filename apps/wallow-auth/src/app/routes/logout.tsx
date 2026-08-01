import { asString, scalarToString } from "@bc-solutions-coder/utils/guards";
import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { LogoutScreen } from "@features/logout";

/**
 * The `/logout` route (Wallow-vec7.3.5).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * One path, two phases: the confirm step and the `signed_out=true` landing are
 * the SAME route driven off a query param (per the oracle), so both live behind
 * this one registration.
 *
 * This route owns the query string — the oracle's two
 * `[SupplyParameterFromQuery]` properties — and hands them down as props, keeping
 * the screen a pure function of its inputs and testable without a router. This is
 * the seam `/reset-password` established and `/consent` followed.
 */
interface LogoutSearch {
  /**
   * The `post_logout_redirect_uri` query parameter. The wire name is snake_case,
   * per the oracle's `[SupplyParameterFromQuery(Name = "post_logout_redirect_uri")]`
   * — it is OpenIddict's own parameter name and is not this screen's to rename,
   * even though the prop it feeds is `postLogoutRedirectUri`.
   */
  readonly post_logout_redirect_uri?: string;
  /**
   * The `signed_out` query parameter, kept as the raw STRING the oracle compares
   * (`SignedOut == "true"`) rather than parsed to a boolean here.
   *
   * `scalarToString` is what restores it: rejecting non-strings outright would
   * send every real OpenIddict post-logout callback — which spells the parameter
   * exactly `signed_out=true`, and so reaches `validateSearch` as the BOOLEAN
   * `true` — to the confirm step, telling a user who just signed out to sign out
   * again.
   */
  readonly signed_out?: string;
}

/**
 * Both params are optional. `post_logout_redirect_uri` is taken ONLY as a string —
 * unlike `signed_out` it is not compared to a literal but used as a URI, and a
 * number or boolean stringified into one is a malformed link, not a destination.
 * Anything else is treated as ABSENT rather than rejected.
 *
 * No open-redirect guard is applied here — see the note on `LogoutScreen`:
 * `post_logout_redirect_uri` is absolute by definition, and the defence on this
 * screen is the server's allow-list (`auth.validateRedirectUri`), not the
 * relative-path `isSafeReturnUrl` check.
 */
function validateSearch(search: Record<string, unknown>): LogoutSearch {
  return {
    post_logout_redirect_uri: asString(search.post_logout_redirect_uri),
    signed_out: scalarToString(search.signed_out),
  };
}

function LogoutRoute() {
  const { post_logout_redirect_uri: postLogoutRedirectUri, signed_out: signedOut } =
    Route.useSearch();

  return (
    <AuthLayout>
      <LogoutScreen postLogoutRedirectUri={postLogoutRedirectUri} signedOut={signedOut} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/logout")({
  validateSearch,
  component: LogoutRoute,
});
