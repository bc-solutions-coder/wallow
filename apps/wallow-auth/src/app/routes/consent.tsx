import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { useTransactionBranding } from "@shared/hooks/use-transaction-branding";
import { ConsentScreen } from "@features/consent";

/**
 * The `/consent` route (Wallow-vec7.3.4).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * This route owns the query string — the oracle's two
 * `[SupplyParameterFromQuery]` properties, plus the `scope` and the single-use
 * `consent_token` the authorize endpoint sends — and hands them down as props, keeping the
 * screen a pure function of its inputs and testable without a router. This is
 * the seam `/reset-password` established.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside,
 * wearing the requesting client's branding from the root loader's transaction
 * context (issue #142) — consent is the flagship branded screen: the user is
 * deciding whether to grant THIS client access, so the header names it and
 * attributes its organization.
 */
interface ConsentSearch {
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
  /**
   * The `client_id` query parameter — cargo the authorize redirect includes.
   * The screen no longer consumes it (who is asking comes from the
   * transaction-scoped context lookup, keyed by `returnUrl`), but the schema
   * keeps the wire spelling so the router serializes the URL faithfully.
   */
  readonly client_id?: string;
  /**
   * The `scope` query parameter — ONE space-delimited string, OAuth's own
   * delimiter and what `AuthorizationController` builds its consent redirect
   * with. Kept raw here and split by the screen; `undefined` when the link omits
   * it.
   */
  readonly scope?: string;
  /**
   * The `consent_token` query parameter — the single-use token the answer must
   * be posted back with. Opaque here; `undefined` when the link omits it.
   */
  readonly consent_token?: string;
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
    scope: typeof search.scope === "string" ? search.scope : undefined,
    consent_token: typeof search.consent_token === "string" ? search.consent_token : undefined,
  };
}

function ConsentRoute() {
  const { returnUrl, scope, consent_token: consentToken } = Route.useSearch();
  const transaction = useTransactionBranding();

  return (
    <AuthLayout branding={transaction?.branding} organizationName={transaction?.organizationName}>
      <ConsentScreen returnUrl={returnUrl} scope={scope} consentToken={consentToken} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/consent")({
  validateSearch,
  component: ConsentRoute,
});
