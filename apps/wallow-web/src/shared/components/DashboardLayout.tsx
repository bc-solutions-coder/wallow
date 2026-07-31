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

/*
 * The two controls are ONE outline button declared once (Wallow-lrlm.5.4). They
 * sit in the main column on `bg-background`, not on the rail, so they take the
 * PAGE's own named tokens — `border-border` for the outline and `bg-muted` for
 * the recessed hover, the substitution Wallow-lrlm.3.5 settled for `ListRow` —
 * rather than the hand-mixed `foreground/20` and `foreground/10` they used to
 * carry. The sidebar palette is deliberately absent: painting a control on a
 * light page with the rail's colours would make it a black box.
 */
const navControlClass =
  "relative z-20 mb-4 px-3 py-2 rounded-md border border-border text-sm font-medium text-foreground hover:bg-muted";

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
      className={navControlClass}
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
      className={navControlClass}
    >
      <Icon aria-hidden="true" className="size-5" />
    </button>
  );
}

/**
 * The scrim's tint — the ONE `foreground` colour the dashboard chrome keeps
 * (Wallow-lrlm.5.4), hoisted so the carve-out is exactly one literal on exactly
 * one line for Wallow-lrlm.5.6's lint gate to exempt.
 *
 * A scrim is not an inversion. Translucency IS the control: a backdrop that is
 * not see-through is a blank page, so no opaque token can express it, and the
 * catalog reaches for the same idiom (`drawerBackdropRecipe` and
 * `alertDialogBackdropRecipe` are `bg-foreground/50`, `popoverBackdropRecipe`
 * `/20`). `bg-sidebar` here would hide the page it exists to dim.
 */
const BACKDROP_SCRIM = "bg-foreground/40";

/**
 * Dismiss-by-clicking-outside for the mobile drawer. Covers the page beside the
 * drawer, which stays interactive above it.
 *
 * It starts at the drawer's trailing edge (`left-64`) rather than spanning the
 * viewport (`inset-0`). The two look identical — the drawer is opaque and sits a
 * layer above — but only this one is honest about where the backdrop can
 * actually be clicked. Under `inset-0` at phone widths the element's own centre
 * falls BEHIND the drawer (`w-64` = 256px of a 390px viewport), so anything
 * targeting the backdrop's centre hits the drawer instead.
 */
function NavBackdrop() {
  const closeMobileNav = useUiStore((state) => state.closeMobileNav);
  return (
    <button
      type="button"
      data-testid="dashboard-nav-backdrop"
      aria-label={navIconLabels.close}
      onClick={closeMobileNav}
      className={`fixed inset-y-0 right-0 left-64 z-20 ${BACKDROP_SCRIM}`}
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
