import { Outlet } from "@tanstack/react-router";

import { DashboardNav } from "./DashboardNav";
import { navIconLabels, navIcons } from "./nav-icons";
import { useIsDesktop } from "../lib/use-is-desktop";
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
 * The shell owns the nav's CONTROLS (Wallow-evd5.4.1) while `DashboardNav` owns
 * the rail and the drawer. They exchange no props — both read `useUiStore`,
 * which is what makes that state global rather than a `useState`. The controls
 * must stay in the main column: a toggle inside the collapsed rail would be the
 * thing it is meant to reveal.
 *
 * ONE CONTROL PER WIDTH (Wallow-0byr.2). The two controls are never on screen
 * together because they act on different axes and only one axis exists at a
 * given width: above `md` the rail is permanent furniture and the only question
 * is whether it carries labels (`dashboard-nav-toggle`); below `md` there is no
 * rail, so the only question is whether the overlay drawer is summoned
 * (`dashboard-nav-mobile-menu`).
 *
 * The backdrop belongs to the drawer, not to the rail: dimming the page behind a
 * persistent sidebar and dismissing it by clicking away is overlay behaviour,
 * and the overlay is the phone drawer. Nothing dims behind the desktop rail in
 * either of its states.
 *
 * The controls are separate components rather than inline JSX so the shell stays
 * inside oxlint's `react/jsx-max-depth` budget.
 */

/** Desktop: expand/collapse the persistent rail between labels and icons. */
function NavToggle() {
  const isNavCollapsed = useUiStore((state) => state.isNavCollapsed);
  const toggleNavCollapsed = useUiStore((state) => state.toggleNavCollapsed);
  const Icon = navIcons.navToggle;
  return (
    <button
      type="button"
      data-testid="dashboard-nav-toggle"
      aria-controls="dashboard-nav"
      aria-expanded={!isNavCollapsed}
      aria-label={navIconLabels.navToggle}
      onClick={toggleNavCollapsed}
      className="relative z-20 mb-4 px-3 py-2 rounded-md border border-foreground/20 text-sm font-medium text-foreground hover:bg-foreground/10"
    >
      <Icon aria-hidden="true" className="size-5" />
    </button>
  );
}

/** Mobile: summon (or dismiss) the overlay drawer. There is no rail to collapse. */
function MobileMenuButton() {
  const isMobileNavOpen = useUiStore((state) => state.isMobileNavOpen);
  const openMobileNav = useUiStore((state) => state.openMobileNav);
  const closeMobileNav = useUiStore((state) => state.closeMobileNav);
  const Icon = navIcons.mobileMenu;
  return (
    <button
      type="button"
      data-testid="dashboard-nav-mobile-menu"
      aria-controls="dashboard-nav-drawer"
      aria-expanded={isMobileNavOpen}
      aria-label={navIconLabels.mobileMenu}
      onClick={isMobileNavOpen ? closeMobileNav : openMobileNav}
      className="relative z-20 mb-4 px-3 py-2 rounded-md border border-foreground/20 text-sm font-medium text-foreground hover:bg-foreground/10"
    >
      <Icon aria-hidden="true" className="size-5" />
    </button>
  );
}

/**
 * Dismiss-by-clicking-outside for the mobile drawer. Covers the page beneath the
 * drawer, which stays interactive above it.
 */
function NavBackdrop() {
  const closeMobileNav = useUiStore((state) => state.closeMobileNav);
  return (
    <button
      type="button"
      data-testid="dashboard-nav-backdrop"
      aria-label={navIconLabels.close}
      onClick={closeMobileNav}
      className="fixed inset-0 z-20 bg-foreground/40"
    />
  );
}

export function DashboardLayout(props: { isAdmin?: boolean } = {}) {
  const isDesktop = useIsDesktop();
  const isMobileNavOpen = useUiStore((state) => state.isMobileNavOpen);
  const showBackdrop = !isDesktop && isMobileNavOpen;
  return (
    <div data-testid="dashboard-welcome" className="min-h-screen flex bg-background">
      <DashboardNav isAdmin={props.isAdmin} />
      {showBackdrop ? <NavBackdrop /> : null}
      <main className="flex-1 p-6 overflow-auto text-foreground">
        {isDesktop ? <NavToggle /> : <MobileMenuButton />}
        <Outlet />
      </main>
    </div>
  );
}
