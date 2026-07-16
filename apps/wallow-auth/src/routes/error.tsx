import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../components/auth-layout";
import { ErrorPage } from "../features/error/components/ErrorPage";

/**
 * The `/error` route (Wallow-vec7.3.3) — the React port of the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Error.razor`.
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * This route carries load beyond its own bead: per bd memory
 * `returnurl-guard-refuse-dont-sanitize`, every screen that fails the
 * `isSafeReturnUrl` open-redirect guard lands HERE via
 * `/error?reason=invalid_redirect_uri`, and the OIDC flows route here with
 * `not_a_member` / `access_denied` / `invalid_request`.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — the per-client
 * (`client_id`) branding overlay is not wired on this screen, and no acceptance
 * criterion asks for it. This mirrors the sibling `/reset-password` route.
 */
interface ErrorSearch {
  /** The `reason` query parameter — `undefined` when the link omits it. */
  readonly reason?: string;
}

/**
 * `reason` is optional and is never rejected, deliberately: `/error?reason=` is
 * attacker-constructible and a bare `/error` must still render the generic
 * message, rather than throw a search-validation error at a user who arrived
 * here from a failed flow. Anything non-string is treated as absent for the same
 * reason. The screen maps the value through a `ReadonlyMap` and never echoes it
 * to the DOM.
 */
function validateSearch(search: Record<string, unknown>): ErrorSearch {
  return {
    reason: typeof search.reason === "string" ? search.reason : undefined,
  };
}

function ErrorRoute() {
  const { reason } = Route.useSearch();

  return (
    <AuthLayout>
      <ErrorPage reason={reason} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/error")({
  validateSearch,
  component: ErrorRoute,
});
