import { createFileRoute } from "@tanstack/react-router";

/**
 * Placeholder for the `/logout` route, pre-registered by Wallow-vec7.3.16.
 *
 * The route is already bound into the shared `src/router.tsx`, so
 * Wallow-vec7.3.5 (2.5 Logout) owns THIS file (plus its own
 * `src/features/logout/` siblings) and implements the screen here **without
 * editing the router** — that registration is already done. The path is the
 * contract, taken verbatim from the `@page` directive of the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Logout.razor`.
 *
 * One path, two phases: the confirm step and the `signed_out=true` landing are
 * the SAME route driven off a query param (per the oracle) — 2.5 implements
 * both in this one file, no second route registration needed.
 *
 * The marker below is deliberately not the screen: the `{page}-{element}`
 * testids from the oracle belong to the owning screen task, which replaces this
 * component wholesale.
 */
function LogoutPlaceholder() {
  return <div data-testid="route-placeholder" data-route="/logout" />;
}

export const Route = createFileRoute("/logout")({
  component: LogoutPlaceholder,
});
