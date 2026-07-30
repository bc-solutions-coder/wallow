import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../components/auth-layout";
import { PrivacyPage } from "../features/privacy/components/PrivacyPage";

/**
 * The `/privacy` route (Wallow-vec7.3.3).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * The page reads no query string, so this route is a pure mount — no
 * `validateSearch`, matching the oracle's parameterless `@page "/privacy"`.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — the per-client
 * (`client_id`) branding overlay is not wired on this screen, and no acceptance
 * criterion asks for it. This mirrors the sibling `/reset-password` route.
 */
function PrivacyRoute() {
  return (
    <AuthLayout>
      <PrivacyPage />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/privacy")({
  component: PrivacyRoute,
});
