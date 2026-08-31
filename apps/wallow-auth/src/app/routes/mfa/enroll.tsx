import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { useTransactionBranding } from "@shared/hooks/use-transaction-branding";
import { MfaEnrollForm } from "@features/mfa-enroll";

/**
 * The `/mfa/enroll` route (Wallow-vec7.3.7).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched. Wallow-vec7.3.15
 * (2.8e) navigates the login screen HERE on `mfaEnrollmentRequired`.
 *
 * This route owns the query string — the oracle's two `[SupplyParameterFromQuery]`
 * properties — and hands both down as props, keeping the form a pure function of
 * its inputs and testable without a router.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside,
 * wearing the requesting client's branding when this enrollment sits inside an
 * authorize transaction (issue #142) — a direct enrollment keeps the fork's own.
 */
interface MfaEnrollSearch {
  /** The `returnUrl` query parameter — `undefined` on a direct (non-OIDC) enrollment. */
  readonly returnUrl?: string;
  /** The `enrollToken` query parameter — present only on the settings-triggered flow. */
  readonly enrollToken?: string;
}

/**
 * BOTH params are optional, deliberately: a user sent here mid-login carries
 * neither, and a bare `/mfa/enroll` must still enroll rather than throw a
 * search-validation error at them. Anything non-string is treated as absent for
 * the same reason.
 *
 * Note that absent and unsafe are NOT the same thing downstream: the form refuses
 * a present-but-unsafe `returnUrl` (including `?returnUrl=`) rather than
 * sanitizing it, and treats an absent one as an ordinary direct enrollment that
 * falls back to "/" on Done.
 */
function validateSearch(search: Record<string, unknown>): MfaEnrollSearch {
  return {
    returnUrl: typeof search.returnUrl === "string" ? search.returnUrl : undefined,
    enrollToken: typeof search.enrollToken === "string" ? search.enrollToken : undefined,
  };
}

function MfaEnrollRoute() {
  const { returnUrl, enrollToken } = Route.useSearch();
  const transaction = useTransactionBranding();

  return (
    <AuthLayout branding={transaction?.branding} organizationName={transaction?.organizationName}>
      <MfaEnrollForm returnUrl={returnUrl} enrollToken={enrollToken} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/mfa/enroll")({
  validateSearch,
  component: MfaEnrollRoute,
});
