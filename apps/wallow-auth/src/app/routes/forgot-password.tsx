import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { useTransactionBranding } from "@shared/hooks/use-transaction-branding";
import { ForgotPasswordForm } from "@features/forgot-password";

/**
 * The `/forgot-password` route (Wallow-vec7.3.1).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside,
 * wearing the requesting client's branding when the visit sits inside an
 * authorize transaction (issue #142). The route declares no search schema of
 * its own — the transaction `returnUrl` is read by the ROOT loader off the raw
 * location, so nothing here needs to consume it — and a bare `/forgot-password`
 * keeps the fork's own chrome.
 */
function ForgotPasswordRoute() {
  const transaction = useTransactionBranding();

  return (
    <AuthLayout branding={transaction?.branding} organizationName={transaction?.organizationName}>
      <ForgotPasswordForm />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/forgot-password")({
  component: ForgotPasswordRoute,
});
