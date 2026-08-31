import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { ErrorPage } from "@features/error";

/**
 * The `/error` route (Wallow-vec7.3.3).
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
 * given no `branding` prop, so it falls back to the fork's own — deliberately
 * (issue #142): the error screen is NEVER client-branded. Several of its reason
 * codes are refusals of the very request that would have identified a client,
 * and dressing the refusal in that client's chrome would lend it legitimacy.
 * The root loader's transaction gate excludes this path too, so no context is
 * even fetched here.
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
