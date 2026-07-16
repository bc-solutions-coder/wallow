import { createFileRoute } from "@tanstack/react-router";

/**
 * Placeholder for the `/mfa/challenge` route, pre-registered by
 * Wallow-vec7.3.16.
 *
 * The route is already bound into the shared `src/router.tsx`, so
 * Wallow-vec7.3.6 (2.6 MfaChallenge) owns THIS file (plus its own
 * `src/features/mfa-challenge/` siblings) and implements the screen here
 * **without editing the router** — that registration is already done. The path
 * is the contract, taken verbatim from the `@page` directive of the Blazor
 * oracle `api/src/Wallow.Auth/Components/Pages/MfaChallenge.razor`.
 *
 * Wallow-vec7.3.15 (2.8e) navigates the login screen HERE on `mfaRequired`, so
 * this path existing is what unblocks that hand-off.
 *
 * The marker below is deliberately not the screen: the `{page}-{element}`
 * testids from the oracle belong to the owning screen task, which replaces this
 * component wholesale.
 */
function MfaChallengePlaceholder() {
  return <div data-testid="route-placeholder" data-route="/mfa/challenge" />;
}

export const Route = createFileRoute("/mfa/challenge")({
  component: MfaChallengePlaceholder,
});
