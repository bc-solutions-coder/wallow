import { AppShell, type NavRequirement } from "@bc-solutions-coder/navigation";
import { Outlet } from "@tanstack/react-router";

import { ADMIN_ROLE, dashboardDestinations } from "./dashboard-destinations";
import { SignOut } from "./SignOut";

/**
 * DashboardLayout — the authenticated dashboard shell the `/dashboard` layout
 * route renders.
 *
 * Everything about the rail, the drawer and the controls that drive them belongs
 * to `@bc-solutions-coder/navigation`. What stays here is the three things only
 * this app can answer: WHICH destinations exist, WHO may see each one, and what
 * the nav footer does — all of which the shell takes as props.
 *
 * `isAdmin` is derived from the current user's roles in the `/dashboard` route's
 * `beforeLoad`. The gate hides an admin-gated destination only when
 * `isAdmin === false`; leaving it unspecified — which is what rendering the
 * layout in isolation does — keeps every destination visible.
 *
 * The shell's testids derive from its `testIdPrefix`, defaulting to
 * `"dashboard"`, which is what `dashboard-nav-organizations`, `dashboard-shell`
 * and the rest of the E2E contract rest on.
 */
/**
 * Module scope, not an inline arrow: the shell CALLS this slot
 * (`renderFooter?.(showLabels)`), so the element it returns is ordinary markup
 * and nothing remounts — but a function-returning-JSX written inside a component
 * body reads as a nested component definition to both `react/no-unstable-nested-
 * components` and to the next person. Hoisting says which of the two it is.
 */
const renderSignOut = (showLabel: boolean) => <SignOut showLabel={showLabel} />;

export function DashboardLayout(props: { isAdmin?: boolean } = {}) {
  return (
    <AppShell
      destinations={dashboardDestinations}
      can={(requirement: NavRequirement) =>
        requirement.role !== ADMIN_ROLE || props.isAdmin !== false
      }
      footer={renderSignOut}
    >
      <Outlet />
    </AppShell>
  );
}
