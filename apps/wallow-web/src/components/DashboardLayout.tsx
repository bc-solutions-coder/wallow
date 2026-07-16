import { Outlet } from "@tanstack/react-router";

import { DashboardNav } from "./DashboardNav";

/**
 * DashboardLayout (Wallow-8w1h.8.1) — the authenticated dashboard shell that the
 * `/dashboard` layout route renders. Wraps the `DashboardNav` and a router
 * `<Outlet/>` (into which the reparented organizations/apps/settings/inquiries
 * child routes render) under a `data-testid="dashboard-welcome"` root.
 */
export function DashboardLayout() {
  return (
    <div data-testid="dashboard-welcome">
      <DashboardNav />
      <Outlet />
    </div>
  );
}
