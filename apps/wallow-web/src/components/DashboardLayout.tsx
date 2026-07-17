import { Outlet } from "@tanstack/react-router";

import { DashboardNav } from "./DashboardNav";

/**
 * DashboardLayout (Wallow-8w1h.8.1) — the authenticated dashboard shell that the
 * `/dashboard` layout route renders. Wraps the `DashboardNav` and a router
 * `<Outlet/>` (into which the reparented organizations/apps/settings/inquiries
 * child routes render) under a `data-testid="dashboard-welcome"` root.
 *
 * `isAdmin` (derived from the current user's roles in the `/dashboard` route's
 * `beforeLoad`, Wallow-ffpq.3.6) is forwarded to `DashboardNav` to gate the
 * Organizations link. It is left unspecified when the layout is rendered in
 * isolation, which keeps the link visible (see `DashboardNav`).
 */
export function DashboardLayout(props: { isAdmin?: boolean } = {}) {
  return (
    <div data-testid="dashboard-welcome" className="min-h-screen flex bg-background">
      <DashboardNav isAdmin={props.isAdmin} />
      <main className="flex-1 p-6 overflow-auto text-foreground">
        <Outlet />
      </main>
    </div>
  );
}
