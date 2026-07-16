import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../../components/auth-layout";
import { VerifyEmailConfirm } from "../../features/verify-email/components/VerifyEmailConfirm";

/**
 * The `/verify-email/confirm` route (Wallow-vec7.3.3) — the React port of the
 * Blazor oracle `api/src/Wallow.Auth/Components/Pages/VerifyEmailConfirm.razor`.
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * This route owns the query string — the oracle's three
 * `[SupplyParameterFromQuery]` properties — and hands them down as props,
 * keeping the screen a pure function of its inputs and testable without a
 * router. This is the seam `/reset-password` established.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — mirroring the
 * sibling `/reset-password` route.
 */
interface VerifyEmailConfirmSearch {
  /** The verification link's `token` — `undefined` when the link omits it. */
  readonly token?: string;
  /** The verification link's `email` — `undefined` when the link omits it. */
  readonly email?: string;
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
}

/**
 * ALL params are optional, deliberately: a mangled verification link must reach
 * the screen and hit the screen's own `IsNullOrEmpty` guard — which short-
 * circuits to the error state without going to the network — rather than throw a
 * search-validation error at a user who clicked a link in their email. Anything
 * non-string is treated as absent for the same reason.
 */
function validateSearch(search: Record<string, unknown>): VerifyEmailConfirmSearch {
  return {
    token: typeof search.token === "string" ? search.token : undefined,
    email: typeof search.email === "string" ? search.email : undefined,
    returnUrl: typeof search.returnUrl === "string" ? search.returnUrl : undefined,
  };
}

function VerifyEmailConfirmRoute() {
  const { token, email, returnUrl } = Route.useSearch();

  return (
    <AuthLayout>
      <VerifyEmailConfirm email={email} token={token} returnUrl={returnUrl} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/verify-email/confirm")({
  validateSearch,
  component: VerifyEmailConfirmRoute,
});
