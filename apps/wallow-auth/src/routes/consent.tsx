import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../components/auth-layout";
import { ConsentScreen } from "../features/consent/components/ConsentScreen";

/**
 * The `/consent` route (Wallow-vec7.3.4).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * This route owns the query string — the oracle's two
 * `[SupplyParameterFromQuery]` properties — and hands them down as props,
 * keeping the screen a pure function of its inputs and testable without a
 * router. This is the seam `/reset-password` established.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — the per-client
 * (`client_id`) branding overlay is not wired on this screen, and no acceptance
 * criterion asks for it, even though this route does carry a `client_id`.
 */
interface ConsentSearch {
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
  /**
   * The `client_id` query parameter. The wire name is snake_case, per the
   * oracle's `[SupplyParameterFromQuery(Name = "client_id")]` — it is
   * OpenIddict's parameter name and is not this screen's to rename, even though
   * the prop it feeds is `clientId`.
   */
  readonly client_id?: string;
}

/**
 * Both params are optional and an unsafe `returnUrl` is NOT rejected here,
 * deliberately: refusing at the search-validation layer would throw before the
 * screen mounts, and the open-redirect refusal is specified to land the user on
 * `/error?reason=invalid_redirect_uri` (bd memory
 * `returnurl-guard-refuse-dont-sanitize`). Handing the raw value to the
 * component, where it can render nothing and route to `/error`, is what makes
 * that possible. Anything non-string is treated as absent.
 */
function validateSearch(search: Record<string, unknown>): ConsentSearch {
  return {
    returnUrl: typeof search.returnUrl === "string" ? search.returnUrl : undefined,
    client_id: typeof search.client_id === "string" ? search.client_id : undefined,
  };
}

function ConsentRoute() {
  const { returnUrl, client_id: clientId } = Route.useSearch();

  return (
    <AuthLayout>
      <ConsentScreen clientId={clientId} returnUrl={returnUrl} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/consent")({
  validateSearch,
  component: ConsentRoute,
});
