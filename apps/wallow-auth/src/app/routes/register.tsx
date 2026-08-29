import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { RegisterForm } from "@features/register";

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
   * The `client_id` query parameter. The wire name is snake_case, per the oracle's
   * `[SupplyParameterFromQuery(Name = "client_id")]` — it is OpenIddict's parameter
   * name and is not this screen's to rename: the schema is what the router
   * serializes back into a URL, so it keeps the wire spelling and the rename to
   * the `clientId` prop happens at the destructure (the `/login` convention).
   */
  readonly client_id?: string;
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
    client_id: typeof search.client_id === "string" ? search.client_id : undefined,
    returnUrl: typeof search.returnUrl === "string" ? search.returnUrl : undefined,
  };
}

function RegisterRoute() {
  const { client_id: clientId, returnUrl } = Route.useSearch();

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
