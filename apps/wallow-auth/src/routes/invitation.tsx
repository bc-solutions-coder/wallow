import { createFileRoute } from "@tanstack/react-router";

/**
 * Placeholder for the `/invitation` route, pre-registered by Wallow-vec7.3.16.
 *
 * The route is already bound into the shared `src/router.tsx`, so
 * Wallow-vec7.3.9 (2.10 InvitationLanding) owns THIS file (plus its own
 * `src/features/invitation/` siblings) and implements the screen here
 * **without editing the router** — that registration is already done. The path
 * is the contract, taken verbatim from the `@page` directive of the Blazor
 * oracle `api/src/Wallow.Auth/Components/Pages/InvitationLanding.razor` — note
 * it is `/invitation`, singular, NOT `/invitations`.
 *
 * The marker below is deliberately not the screen: the `{page}-{element}`
 * testids from the oracle belong to the owning screen task, which replaces this
 * component wholesale.
 */
function InvitationLandingPlaceholder() {
  return <div data-testid="route-placeholder" data-route="/invitation" />;
}

export const Route = createFileRoute("/invitation")({
  component: InvitationLandingPlaceholder,
});
