import { Outlet } from "@tanstack/react-router";

import { DashboardNav } from "./DashboardNav";
import { useUiStore } from "../stores/ui-store";

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
 *
 * The shell owns the nav drawer's CONTROLS (Wallow-evd5.4.1) while `DashboardNav`
 * owns the drawer itself. They exchange no props — both read `useUiStore`, which
 * is what makes that state global rather than a `useState`. The controls must
 * stay in the main column: a toggle inside the collapsed rail would be the thing
 * it is meant to reveal.
 *
 * `NavToggle`/`NavBackdrop` are separate components rather than inline JSX so the
 * shell stays inside oxlint's `react/jsx-max-depth` budget.
 */

/** Expand/collapse control for the nav drawer; sits above the backdrop so it stays clickable. */
function NavToggle() {
  const isNavOpen = useUiStore((state) => state.isNavOpen);
  const toggleNav = useUiStore((state) => state.toggleNav);
  return (
    <button
      type="button"
      data-testid="dashboard-nav-toggle"
      aria-controls="dashboard-nav"
      aria-expanded={isNavOpen}
      onClick={toggleNav}
      className="relative z-20 mb-4 px-3 py-2 rounded-md border border-foreground/20 text-sm font-medium text-foreground hover:bg-foreground/10"
    >
      Menu
    </button>
  );
}

/**
 * Dismiss-by-clicking-outside for the open drawer. Covers the main column only
 * (the drawer itself stays interactive), and is the sole consumer of `closeNav`.
 */
function NavBackdrop() {
  const closeNav = useUiStore((state) => state.closeNav);
  return (
    <button
      type="button"
      data-testid="dashboard-nav-backdrop"
      aria-label="Close navigation"
      onClick={closeNav}
      className="fixed top-0 right-0 bottom-0 left-64 z-10 bg-foreground/40"
    />
  );
}

export function DashboardLayout(props: { isAdmin?: boolean } = {}) {
  const isNavOpen = useUiStore((state) => state.isNavOpen);
  return (
    <div data-testid="dashboard-welcome" className="min-h-screen flex bg-background">
      <DashboardNav isAdmin={props.isAdmin} />
      {isNavOpen ? <NavBackdrop /> : null}
      <main className="flex-1 p-6 overflow-auto text-foreground">
        <NavToggle />
        <Outlet />
      </main>
    </div>
  );
}
