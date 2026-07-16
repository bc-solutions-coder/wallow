import { Link } from "@tanstack/react-router";

/**
 * DashboardNav (Wallow-8w1h.8.1) — the dashboard shell's primary navigation.
 *
 * Renders four `Link`s to the dashboard verticals, each carrying a
 * `data-testid="dashboard-nav-<feature>"` testid. All four links are
 * unconditional — a deliberate divergence from the Blazor oracle's admin-gated
 * Organizations link, since the SDK's `WallowUser` has no typed roles array:
 *   organizations -> /dashboard/organizations
 *   apps          -> /dashboard/apps
 *   settings      -> /dashboard/settings
 *   inquiries     -> /dashboard/inquiries
 */
export function DashboardNav() {
  return (
    <nav>
      <Link to="/dashboard/organizations" data-testid="dashboard-nav-organizations">
        Organizations
      </Link>
      <Link to="/dashboard/apps" data-testid="dashboard-nav-apps">
        Apps
      </Link>
      <Link to="/dashboard/settings" data-testid="dashboard-nav-settings">
        Settings
      </Link>
      <Link to="/dashboard/inquiries" data-testid="dashboard-nav-inquiries">
        Inquiries
      </Link>
    </nav>
  );
}
