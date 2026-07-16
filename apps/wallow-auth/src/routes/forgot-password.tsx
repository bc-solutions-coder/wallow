import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../components/auth-layout";
import { ForgotPasswordForm } from "../features/forgot-password/components/ForgotPasswordForm";

/**
 * The `/forgot-password` route (Wallow-vec7.3.1) — the React port of the Blazor
 * oracle `api/src/Wallow.Auth/Components/Pages/ForgotPassword.razor`.
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — the per-client
 * (`client_id`) branding overlay is not wired on this screen, and no acceptance
 * criterion asks for it. If a shared `use-client-branding` hook lands, this route
 * is a one-line retrofit.
 */
function ForgotPasswordRoute() {
  return (
    <AuthLayout>
      <ForgotPasswordForm />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/forgot-password")({
  component: ForgotPasswordRoute,
});
