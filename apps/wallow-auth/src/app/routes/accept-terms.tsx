import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { useTransactionBranding } from "@shared/hooks/use-transaction-branding";
import { AcceptTermsScreen } from "@features/accept-terms";

/**
 * The `/accept-terms` route (Wallow-vec7.3.10).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * This is the ToS/Privacy *gate* in the external-login flow; the static terms
 * document is the separate `/terms` route (Wallow-vec7.3.3).
 *
 * This route owns the query string — the oracle's four
 * `[SupplyParameterFromQuery]` properties, plus the `client_id` the flow gained
 * in Wallow-53kr — and hands them down as props, keeping the screen a pure
 * function of its inputs and testable without a router. This is the seam
 * `/reset-password` established and `/consent` followed.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside,
 * wearing the requesting client's branding when this gate sits inside an
 * authorize transaction (issue #142). The `client_id` here stays relay cargo
 * for the endpoint — the branding comes from the root loader's
 * transaction-scoped context, keyed by `returnUrl`. (This flow's returnUrl is
 * usually ABSOLUTE and server-allow-listed, which the transaction gate
 * refuses locally — such a visit simply keeps the fork's chrome.)
 */
interface AcceptTermsSearch {
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
  /**
   * The `client_id` query parameter — the client that started the external-login
   * flow (Wallow-53kr). Snake_case, because that is the spelling
   * `external-login-callback` redirects here with, and the schema keeps the wire
   * spelling it serializes back into a URL; the rename to the `clientId` prop
   * (the name the endpoint binds) happens at the destructure, the `/login`
   * convention.
   */
  readonly client_id?: string;
  /** The `email` query parameter — the address the external provider vouched for. */
  readonly email?: string;
  /** The `name` query parameter — the provider's display name for the user. */
  readonly name?: string;
  /** The `error` query parameter — a bounce-back reason code. */
  readonly error?: string;
}

/**
 * All four params are optional: a bare `/accept-terms` must still render its
 * gate rather than throw. (Without an ExternalLoginState cookie the API will
 * bounce that user to `/login?error=session_expired` — the API's call to make,
 * not a reason for this route to explode.)
 *
 * `returnUrl` is handed on RAW: it is not this route's to reject. Every value
 * this flow produces is an absolute, server-allow-listed URL, and
 * `complete-external-registration` re-validates it before honouring it — see the
 * guard note on `AcceptTermsScreen`.
 *
 * Anything non-string is treated as absent, the narrowing `/consent` set:
 * TanStack Router JSON-parses scalar search values, so `?name=42` arrives as the
 * NUMBER 42, and handing that to a `string | undefined` prop is how a port ships
 * `.trim is not a function`.
 */
function validateSearch(search: Record<string, unknown>): AcceptTermsSearch {
  return {
    returnUrl: typeof search.returnUrl === "string" ? search.returnUrl : undefined,
    client_id: typeof search.client_id === "string" ? search.client_id : undefined,
    email: typeof search.email === "string" ? search.email : undefined,
    name: typeof search.name === "string" ? search.name : undefined,
    error: typeof search.error === "string" ? search.error : undefined,
  };
}

function AcceptTermsRoute() {
  const { returnUrl, client_id: clientId, email, name, error } = Route.useSearch();
  const transaction = useTransactionBranding();

  return (
    <AuthLayout branding={transaction?.branding} organizationName={transaction?.organizationName}>
      <AcceptTermsScreen
        returnUrl={returnUrl}
        clientId={clientId}
        email={email}
        name={name}
        error={error}
      />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/accept-terms")({
  validateSearch,
  component: AcceptTermsRoute,
});
