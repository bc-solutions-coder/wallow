import { createFileRoute } from "@tanstack/react-router";

/**
 * Placeholder for the `/register` route, pre-registered by Wallow-vec7.3.16.
 *
 * The route is already bound into the shared `src/router.tsx`, so
 * Wallow-vec7.3.8 (2.9 Register) owns THIS file (plus its own
 * `src/features/register/` siblings) and implements the screen here **without
 * editing the router** — that registration is already done. The path is the
 * contract, taken verbatim from the `@page` directive of the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Register.razor`.
 *
 * The marker below is deliberately not the screen: the `{page}-{element}`
 * testids from the oracle belong to the owning screen task, which replaces this
 * component wholesale.
 */
function RegisterPlaceholder() {
  return <div data-testid="route-placeholder" data-route="/register" />;
}

export const Route = createFileRoute("/register")({
  component: RegisterPlaceholder,
});
