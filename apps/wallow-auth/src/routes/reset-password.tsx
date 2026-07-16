import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../components/auth-layout";
import { ResetPasswordForm } from "../features/reset-password/components/ResetPasswordForm";

/**
 * The `/reset-password` route (Wallow-vec7.3.2) — the React port of the Blazor
 * oracle `api/src/Wallow.Auth/Components/Pages/ResetPassword.razor`.
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * This route owns the query string — the oracle's two `[SupplyParameterFromQuery]`
 * properties — and passes both halves of the reset link down as props, keeping
 * the form a pure function of its inputs and testable without a router.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — the per-client
 * (`client_id`) branding overlay is not wired on this screen, and no acceptance
 * criterion asks for it. If a shared `use-client-branding` hook lands, this route
 * is a one-line retrofit.
 */
interface ResetPasswordSearch {
  /** The reset link's `email` — `undefined` when the link omits it. */
  readonly email?: string;
  /** The reset link's `token` — `undefined` when the link omits it. */
  readonly token?: string;
}

/**
 * BOTH params are optional, deliberately: a bare `/reset-password` must still
 * render its form and refuse on submit (the form carries the oracle's
 * `IsNullOrEmpty` guard), rather than throw a search-validation error at a user
 * who followed a mangled link. Anything non-string is treated as absent for the
 * same reason.
 */
function validateSearch(search: Record<string, unknown>): ResetPasswordSearch {
  return {
    email: typeof search.email === "string" ? search.email : undefined,
    token: typeof search.token === "string" ? search.token : undefined,
  };
}

function ResetPasswordRoute() {
  const { email, token } = Route.useSearch();

  return (
    <AuthLayout>
      <ResetPasswordForm email={email} token={token} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/reset-password")({
  validateSearch,
  component: ResetPasswordRoute,
});
