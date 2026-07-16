import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../components/auth-layout";
import { TermsPage } from "../features/terms/components/TermsPage";

/**
 * The `/terms` route (Wallow-vec7.3.3) — the React port of the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Terms.razor`.
 *
 * Not to be confused with `/accept-terms` (the ToS gate, owned by
 * Wallow-vec7.3.10) — this is the static terms document that gate links to.
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * The page reads no query string, so this route is a pure mount — no
 * `validateSearch`, matching the oracle's parameterless `@page "/terms"`.
 */
function TermsRoute() {
  return (
    <AuthLayout>
      <TermsPage />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/terms")({
  component: TermsRoute,
});
