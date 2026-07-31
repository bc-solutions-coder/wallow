// The per-component subpath, NOT the root barrel — the same constraint
// `DashboardNav.tsx` documents at its own import: the barrel also pulls in
// `FocusOnNavigate`, which imports `useRouterState`, and every spec in this
// directory stubs `@tanstack/react-router` down to `Link` plus `Outlet`.
// Bundlers tree-shake that away; a dev/test module graph does not, so the
// barrel would fail to link here without changing a single rendered class.
import { Button } from "@bc-solutions-coder/ui/button";
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
 * The two controls are ONE outline button declared once (Wallow-lrlm.5.4), and
 * since Wallow-lrlm.6.5 that button is the CATALOG'S rather than a string this
 * shell keeps. The catalog already shipped exactly this control: `outline` is
 * the same border-with-no-surface treatment the shell used to spell out, and
 * `icon` is the square target a glyph-only button actually wants. Adopting the
 * variant also deletes the one place the hand-rolled copy had drifted — it
 * hovered to `bg-muted` where the catalog hovers to `bg-accent`.
 *
 * `width="auto"` is not decoration: the recipe defaults `width` to `full` for
 * the sake of the call sites that predate that axis, which would stretch a
 * square icon box across the column.
 *
 * The recipe owns colour, border, radius, padding and type scale. POSITION it
 * does not own, so the layout below stays the caller's to pass; it merges last
 * through `cn()`/tailwind-merge, so it survives.
 *
 * The sidebar palette stays deliberately absent — `surface` keeps its `page`
 * default. These sit in the main column on `bg-background`, not on the rail:
 * painting a control on a light page with the rail's colours would make it a
 * black box.
 */
const navControlLayout = "relative z-20 mb-4";

/**
 * The pre-hydration display utilities (Wallow-lrlm.6.3). While `useIsDesktop`
 * answers `undefined` both controls are emitted and the `md` media query picks
 * which one paints — at first paint, which is the whole point: a control chosen
 * in JavaScript cannot be chosen until JavaScript has run, and by then the wrong
 * one has already been on screen. `md:inline-flex` restores the display the
 * catalog `Button` already has, so the visible control lays out identically to
 * the unconditional one.
 *
 * It has to RESTORE it, and it has to restore THAT value (Wallow-lrlm.6.5).
 * `hidden` and the recipe's `inline-flex` are the same tailwind-merge group and
 * the caller's `className` merges last, so `hidden` deletes `inline-flex`
 * outright — leaving nothing to re-enable at `md` unless this says so. The old
 * `md:inline-block` was correct for a bare `<button>`; on a `Button` it would
 * strand a `size-9 p-0` box with no flex centring, dropping the glyph onto the
 * text baseline for the length of the pre-hydration window. `mobileOnlyClass`
 * needs no counterpart: `md:hidden` is a different modifier scope, so the base
 * `inline-flex` survives beside it.
 */
const desktopOnlyClass = "hidden md:inline-flex";
const mobileOnlyClass = "md:hidden";

/** Desktop: expand/collapse the persistent rail between labels and icons. */
function NavToggle(props: { hideBelowMd?: boolean } = {}) {
  const isNavCollapsed = useUiStore((state) => state.isNavCollapsed);
  const toggleNavCollapsed = useUiStore((state) => state.toggleNavCollapsed);
  const Icon = navIcons.navToggle;
  return (
    <Button
      variant="outline"
      size="icon"
      width="auto"
      type="button"
      data-testid="dashboard-nav-toggle"
      aria-controls="dashboard-nav"
      aria-expanded={!isNavCollapsed}
      aria-label={navIconLabels.navToggle}
      onClick={toggleNavCollapsed}
      className={props.hideBelowMd ? `${navControlLayout} ${desktopOnlyClass}` : navControlLayout}
    >
      <Icon aria-hidden="true" className="size-5" />
    </Button>
  );
}

/** Mobile: summon (or dismiss) the overlay drawer. There is no rail to collapse. */
function MobileMenuButton(props: { hideAtMd?: boolean } = {}) {
  const isMobileNavOpen = useUiStore((state) => state.isMobileNavOpen);
  const openMobileNav = useUiStore((state) => state.openMobileNav);
  const closeMobileNav = useUiStore((state) => state.closeMobileNav);
  const Icon = navIcons.mobileMenu;
  return (
    <Button
      variant="outline"
      size="icon"
      width="auto"
      type="button"
      data-testid="dashboard-nav-mobile-menu"
      aria-controls="dashboard-nav-drawer"
      aria-expanded={isMobileNavOpen}
      aria-label={navIconLabels.mobileMenu}
      onClick={isMobileNavOpen ? closeMobileNav : openMobileNav}
      className={props.hideAtMd ? `${navControlLayout} ${mobileOnlyClass}` : navControlLayout}
    >
      <Icon aria-hidden="true" className="size-5" />
    </Button>
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

/**
 * Which control the main column carries. Extracted so the unresolved-viewport
 * case is a branch rather than a nested ternary: before the width is known BOTH
 * are emitted and CSS shows one, after it is known exactly one is mounted — so
 * the "one control per width" rule above holds at every moment, including the
 * first paint it previously did not cover.
 */
function NavControls(props: { isDesktop: boolean | undefined }) {
  if (props.isDesktop === undefined) {
    return (
      <>
        <NavToggle hideBelowMd />
        <MobileMenuButton hideAtMd />
      </>
    );
  }
  return props.isDesktop ? <NavToggle /> : <MobileMenuButton />;
}

export function DashboardLayout(props: { isAdmin?: boolean } = {}) {
  const isDesktop: boolean | undefined = useIsDesktop();
  const isMobileNavOpen = useUiStore((state) => state.isMobileNavOpen);
  // `isDesktop === false`, not `!isDesktop`: an unresolved viewport is not a
  // mobile one. The drawer cannot be open in that window anyway — nothing has
  // been clickable yet — so a scrim there would be a scrim over nothing.
  const showBackdrop = isDesktop === false && isMobileNavOpen;
  return (
    <div data-testid="dashboard-welcome" className="min-h-screen flex bg-background">
      <DashboardNav isAdmin={props.isAdmin} />
      {showBackdrop ? <NavBackdrop /> : null}
      <main className="flex-1 p-6 overflow-auto text-foreground">
        <NavControls isDesktop={isDesktop} />
        <Outlet />
      </main>
    </div>
  );
}
