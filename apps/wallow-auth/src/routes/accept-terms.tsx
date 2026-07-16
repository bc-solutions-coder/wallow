import { createFileRoute } from "@tanstack/react-router";

/**
 * Placeholder for the `/accept-terms` route, pre-registered by
 * Wallow-vec7.3.16.
 *
 * The route is already bound into the shared `src/router.tsx`, so
 * Wallow-vec7.3.10 (2.11 AcceptTerms) owns THIS file (plus its own
 * `src/features/accept-terms/` siblings) and implements the screen here
 * **without editing the router** — that registration is already done. The path
 * is the contract, taken verbatim from the `@page` directive of the Blazor
 * oracle `api/src/Wallow.Auth/Components/Pages/AcceptTerms.razor`.
 *
 * This is the ToS/Privacy *gate* in the external-login flow; the static terms
 * document is the separate `/terms` route (owned by Wallow-vec7.3.3).
 *
 * The marker below is deliberately not the screen: the `{page}-{element}`
 * testids from the oracle belong to the owning screen task, which replaces this
 * component wholesale.
 */
function AcceptTermsPlaceholder() {
  return <div data-testid="route-placeholder" data-route="/accept-terms" />;
}

export const Route = createFileRoute("/accept-terms")({
  component: AcceptTermsPlaceholder,
});
