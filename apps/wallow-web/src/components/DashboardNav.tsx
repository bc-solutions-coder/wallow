import { logout } from "@bc-solutions-coder/sdk";
import { Link } from "@tanstack/react-router";

import { useUiStore } from "../stores/ui-store";

/**
 * DashboardNav (Wallow-8w1h.8.1) — the dashboard shell's primary navigation.
 *
 * Renders `Link`s to the dashboard verticals, each carrying a
 * `data-testid="dashboard-nav-<feature>"` testid:
 *   organizations -> /dashboard/organizations (admin-gated, Wallow-ffpq.3.6)
 *   apps          -> /dashboard/apps
 *   settings      -> /dashboard/settings
 *   inquiries     -> /dashboard/inquiries
 *
 * `isAdmin` gates the Organizations link to admins. The gate hides
 * the link only when `isAdmin === false`; an unspecified `isAdmin` (the shell
 * renders `<DashboardNav />` in isolation, e.g. `DashboardLayout.test.tsx`)
 * leaves the link visible. A `dashboard-logout-link` calls the BFF logout,
 * providing a Sign Out control (net-new for Wallow-ffpq.3.6).
 *
 * Expanded/collapsed is read from `useUiStore` (Wallow-evd5.4.1), NOT from a
 * prop: the control that flips it lives in the sibling `DashboardLayout`, so the
 * two components share only the store. Collapsing is presentational — the aside
 * stays mounted and narrows to an icon rail, so the links (and the `aria-controls`
 * target `#dashboard-nav`) keep their identity in both states.
 */
const linkClass =
  "flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium whitespace-nowrap text-background/80 hover:text-background hover:bg-background/10 no-underline";

export function DashboardNav(props: { isAdmin?: boolean } = {}) {
  const showOrganizations = props.isAdmin !== false;
  const isNavOpen = useUiStore((state) => state.isNavOpen);
  return (
    <aside
      id="dashboard-nav"
      data-testid="dashboard-nav"
      data-nav-open={isNavOpen ? "true" : "false"}
      className="relative z-30 w-16 data-[nav-open=true]:w-64 overflow-x-hidden bg-foreground text-background flex flex-col shrink-0 transition-[width] duration-200"
    >
      <nav className="flex-1 px-4 py-4 flex flex-col gap-1">
        {showOrganizations ? (
          <Link
            to="/dashboard/organizations"
            data-testid="dashboard-nav-organizations"
            className={linkClass}
          >
            Organizations
          </Link>
        ) : null}
        <Link to="/dashboard/apps" data-testid="dashboard-nav-apps" className={linkClass}>
          Apps
        </Link>
        <Link to="/dashboard/settings" data-testid="dashboard-nav-settings" className={linkClass}>
          Settings
        </Link>
        <Link to="/dashboard/inquiries" data-testid="dashboard-nav-inquiries" className={linkClass}>
          Inquiries
        </Link>
      </nav>
      <div className="px-4 py-4 border-t border-background/10">
        <button
          type="button"
          data-testid="dashboard-logout-link"
          className={`${linkClass} w-full text-left`}
          onClick={() => {
            logout();
          }}
        >
          Sign Out
        </button>
      </div>
    </aside>
  );
}
