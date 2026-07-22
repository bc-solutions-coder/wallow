import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../components/auth-layout";
import { RegisterForm } from "../features/register/components/RegisterForm";

/**
 * The `/register` route (Wallow-vec7.3.8).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * This route owns the query string — the oracle's two `[SupplyParameterFromQuery]`
 * properties — and passes both down as props, keeping the form a pure function of
 * its inputs and testable without a router (the seam `ResetPasswordForm`
 * established and `MfaChallengeForm` followed).
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — the per-client
 * (`client_id`) branding overlay is not wired on this screen, and no acceptance
 * criterion asks for it, though this route is where it would land.
 */
interface RegisterSearch {
  /**
   * The `client_id` query parameter — snake_case ON THE WIRE (the oracle's
   * `[SupplyParameterFromQuery(Name = "client_id")]`), camelCase in the app.
   */
  readonly clientId?: string;
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
}

/**
 * BOTH params are optional, deliberately: a bare `/register` is the ordinary
 * direct-signup entry point and must render its form rather than throw a
 * search-validation error at the user. Anything non-string is treated as absent
 * for the same reason.
 */
function validateSearch(search: Record<string, unknown>): RegisterSearch {
  return {
    clientId: typeof search.client_id === "string" ? search.client_id : undefined,
    returnUrl: typeof search.returnUrl === "string" ? search.returnUrl : undefined,
  };
}

function RegisterRoute() {
  const { clientId, returnUrl } = Route.useSearch();

  return (
    <AuthLayout>
      <RegisterForm clientId={clientId} returnUrl={returnUrl} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/register")({
  validateSearch,
  component: RegisterRoute,
});
