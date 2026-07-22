import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../../components/auth-layout";
import { MfaChallengeForm } from "../../features/mfa-challenge/components/MfaChallengeForm";

/**
 * The `/mfa/challenge` route (Wallow-vec7.3.6).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched. Wallow-vec7.3.15
 * (2.8e) navigates the login screen HERE on `mfaRequired`.
 *
 * This route owns the query string — the oracle's single
 * `[SupplyParameterFromQuery]` property — and hands `returnUrl` down as a prop,
 * keeping the form a pure function of its inputs and testable without a router.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — the per-client
 * branding overlay is not wired on this screen, consistent with the sibling
 * ports, and no acceptance criterion asks for it.
 */
interface MfaChallengeSearch {
  /** The `returnUrl` query parameter — `undefined` on a direct (non-OIDC) sign-in. */
  readonly returnUrl?: string;
}

/**
 * `returnUrl` is OPTIONAL, deliberately: a bare `/mfa/challenge` is the direct
 * (non-OIDC) sign-in path and must still render its form, rather than throw a
 * search-validation error at a user mid-login. Anything non-string is treated as
 * absent for the same reason.
 *
 * Note that absent and unsafe are NOT the same thing downstream: the form
 * refuses a present-but-unsafe value (including `?returnUrl=`) rather than
 * sanitizing it, and treats an absent one as an ordinary direct sign-in.
 */
function validateSearch(search: Record<string, unknown>): MfaChallengeSearch {
  return {
    returnUrl: typeof search.returnUrl === "string" ? search.returnUrl : undefined,
  };
}

function MfaChallengeRoute() {
  const { returnUrl } = Route.useSearch();

  return (
    <AuthLayout>
      <MfaChallengeForm returnUrl={returnUrl} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/mfa/challenge")({
  validateSearch,
  component: MfaChallengeRoute,
});
