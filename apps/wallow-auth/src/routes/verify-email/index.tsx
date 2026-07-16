import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../../components/auth-layout";
import { VerifyEmailNotice } from "../../features/verify-email/components/VerifyEmailNotice";

/**
 * The `/verify-email` route (Wallow-vec7.3.3) — the React port of the Blazor
 * oracle `api/src/Wallow.Auth/Components/Pages/VerifyEmail.razor`.
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * This is the static "check your inbox" page; the token-consuming confirm step
 * is its sibling `confirm.tsx` (`/verify-email/confirm`). Both are owned by 2.3.
 *
 * This route owns the query string (the oracle's `[SupplyParameterFromQuery]`
 * `ReturnUrl`) and hands it down as a prop, keeping the screen a pure function
 * of its inputs and testable without a router.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — mirroring the
 * sibling `/reset-password` route.
 */
interface VerifyEmailSearch {
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
}

/**
 * `returnUrl` is optional and never rejected, deliberately: a bare
 * `/verify-email` must render the notice with a plain `/login` back-link rather
 * than throw a search-validation error at a user who clicked a link in their
 * email. Anything non-string is treated as absent for the same reason. The
 * screen — not this function — applies the `isSafeReturnUrl` guard before the
 * value reaches the back-link.
 */
function validateSearch(search: Record<string, unknown>): VerifyEmailSearch {
  return {
    returnUrl: typeof search.returnUrl === "string" ? search.returnUrl : undefined,
  };
}

function VerifyEmailRoute() {
  const { returnUrl } = Route.useSearch();

  return (
    <AuthLayout>
      <VerifyEmailNotice returnUrl={returnUrl} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/verify-email")({
  validateSearch,
  component: VerifyEmailRoute,
});
