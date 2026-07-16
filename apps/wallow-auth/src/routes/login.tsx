import { createFileRoute } from "@tanstack/react-router";

/**
 * Placeholder for the `/login` route, pre-registered by Wallow-vec7.3.16.
 *
 * The route is already bound into the shared `src/router.tsx`, so the Login
 * sub-tasks own THIS file (plus their own `src/features/login/` siblings) and
 * implement the screen here **without editing the router** — that registration
 * is already done. The path is the contract, taken verbatim from the `@page`
 * directive of the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Login.razor`.
 *
 * Unlike every other screen file, this one has FIVE owners in sequence and is
 * NOT parallel-safe: Wallow-vec7.3.11 (2.8a, password tab) creates the real
 * component and tab shell, then .3.12 (magic-link), .3.13 (OTP), .3.14
 * (external providers) and .3.15 (MFA hand-off) each extend the same file. Run
 * them serially, per the dep edges already on those beads.
 *
 * `/` (`routes/index.tsx`) already redirects here, so this placeholder is what
 * the app's entry point currently lands on.
 *
 * The marker below is deliberately not the screen: the `{page}-{element}`
 * testids from the oracle belong to the owning screen tasks, which replace this
 * component wholesale.
 */
function LoginPlaceholder() {
  return <div data-testid="route-placeholder" data-route="/login" />;
}

export const Route = createFileRoute("/login")({
  component: LoginPlaceholder,
});
