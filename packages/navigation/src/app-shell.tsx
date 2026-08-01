// The per-component subpath, NOT the root barrel — the same constraint
// `app-nav.tsx` documents at its own import: the barrel also pulls in
// `FocusOnNavigate`, which imports `useRouterState`, and the specs around this
// component stub `@tanstack/react-router` down to `Link`. Bundlers tree-shake
// that away; a dev/test module graph does not, so the barrel would fail to link
// here without changing a single rendered class.
import { Button } from "@bc-solutions-coder/ui/button";
import type { ReactNode } from "react";

import { AppNav } from "./app-nav";
import type { NavDestination, NavRequirement } from "./destinations";
import { defaultNavControlIcons, defaultNavControlLabels, type NavControlIcons } from "./nav-icons";
import { useNavStore } from "./nav-store";
import { useIsDesktop } from "./use-is-desktop";

/**
 * `AppShell` — the application frame: a collapsible desktop rail, a mobile
 * overlay drawer, the controls that drive them, and a main column holding
 * whatever the app routes into it.
 *
 * The routed content is `children`, not an `Outlet` this package renders. A
 * router's `Outlet` is bound to the app's own route tree, so taking it as
 * children is what keeps this package usable by a fork that mounts the shell
 * somewhere else in its tree — and it is why `@tanstack/react-router` is needed
 * here only for `Link`.
 *
 * The shell owns the nav's CONTROLS while `AppNav` owns the rail and the drawer.
 * They exchange no props — both read `useNavStore`, which is what makes that
 * state global rather than a `useState`. The controls must stay in the main
 * column: a toggle inside the collapsed rail would be the thing it is meant to
 * reveal.
 *
 * ONE CONTROL PER WIDTH. The two controls are never on screen together because
 * they act on different axes and only one axis exists at a given width: above
 * `md` the rail is permanent furniture and the only question is whether it
 * carries labels (`{prefix}-nav-toggle`); below `md` there is no rail, so the
 * only question is whether the overlay drawer is summoned
 * (`{prefix}-nav-mobile-menu`).
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
 * The two controls are ONE outline button declared once, and that button is the
 * CATALOG'S rather than a string this shell keeps. The catalog already ships
 * exactly this control: `outline` is the border-with-no-surface treatment, and
 * `icon` is the square target a glyph-only button actually wants.
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
 * The pre-hydration display utilities. While `useIsDesktop` answers `undefined`
 * both controls are emitted and the `md` media query picks which one paints — at
 * first paint, which is the whole point: a control chosen in JavaScript cannot be
 * chosen until JavaScript has run, and by then the wrong one has already been on
 * screen. `md:inline-flex` restores the display the catalog `Button` already has,
 * so the visible control lays out identically to the unconditional one.
 *
 * It has to RESTORE it, and it has to restore THAT value. `hidden` and the
 * recipe's `inline-flex` are the same tailwind-merge group and the caller's
 * `className` merges last, so `hidden` deletes `inline-flex` outright — leaving
 * nothing to re-enable at `md` unless this says so. `md:inline-block` would be
 * correct for a bare `<button>`; on a `Button` it strands a `size-9 p-0` box with
 * no flex centring, dropping the glyph onto the text baseline for the length of
 * the pre-hydration window. `mobileOnlyClass` needs no counterpart: `md:hidden`
 * is a different modifier scope, so the base `inline-flex` survives beside it.
 */
const desktopOnlyClass = "hidden md:inline-flex";
const mobileOnlyClass = "md:hidden";

/** What the controls need from the shell: their icons, names and testid stem. */
interface ControlProps {
  testIdPrefix: string;
  icons: NavControlIcons;
  labels: Readonly<Record<keyof NavControlIcons, string>>;
}

/** Desktop: expand/collapse the persistent rail between labels and icons. */
function NavToggle(props: ControlProps & { hideBelowMd?: boolean }) {
  const isNavCollapsed = useNavStore((state) => state.isNavCollapsed);
  const toggleNavCollapsed = useNavStore((state) => state.toggleNavCollapsed);
  const Icon = props.icons.navToggle;
  return (
    <Button
      variant="outline"
      size="icon"
      width="auto"
      type="button"
      data-testid={`${props.testIdPrefix}-nav-toggle`}
      aria-controls={`${props.testIdPrefix}-nav`}
      aria-expanded={!isNavCollapsed}
      aria-label={props.labels.navToggle}
      onClick={toggleNavCollapsed}
      className={props.hideBelowMd ? `${navControlLayout} ${desktopOnlyClass}` : navControlLayout}
    >
      <Icon aria-hidden="true" className="size-5" />
    </Button>
  );
}

/** Mobile: summon (or dismiss) the overlay drawer. There is no rail to collapse. */
function MobileMenuButton(props: ControlProps & { hideAtMd?: boolean }) {
  const isMobileNavOpen = useNavStore((state) => state.isMobileNavOpen);
  const openMobileNav = useNavStore((state) => state.openMobileNav);
  const closeMobileNav = useNavStore((state) => state.closeMobileNav);
  const Icon = props.icons.mobileMenu;
  return (
    <Button
      variant="outline"
      size="icon"
      width="auto"
      type="button"
      data-testid={`${props.testIdPrefix}-nav-mobile-menu`}
      aria-controls={`${props.testIdPrefix}-nav-drawer`}
      aria-expanded={isMobileNavOpen}
      aria-label={props.labels.mobileMenu}
      onClick={isMobileNavOpen ? closeMobileNav : openMobileNav}
      className={props.hideAtMd ? `${navControlLayout} ${mobileOnlyClass}` : navControlLayout}
    >
      <Icon aria-hidden="true" className="size-5" />
    </Button>
  );
}

/**
 * The scrim's tint — the ONE `foreground` colour the shell keeps, hoisted so the
 * carve-out is exactly one literal on exactly one line for the lint gate to
 * exempt.
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
function NavBackdrop(props: ControlProps) {
  const closeMobileNav = useNavStore((state) => state.closeMobileNav);
  return (
    <button
      type="button"
      data-testid={`${props.testIdPrefix}-nav-backdrop`}
      aria-label={props.labels.close}
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
 * first paint it otherwise would not cover.
 */
function NavControls(props: ControlProps & { isDesktop: boolean | undefined }) {
  if (props.isDesktop === undefined) {
    return (
      <>
        <NavToggle {...props} hideBelowMd />
        <MobileMenuButton {...props} hideAtMd />
      </>
    );
  }
  return props.isDesktop ? <NavToggle {...props} /> : <MobileMenuButton {...props} />;
}

/** Everything visible: destinations, gate, slots, icons and the testid stem. */
export interface AppShellProps {
  /** The nav manifest, in render order. */
  readonly destinations: readonly NavDestination[];
  /**
   * Whether a destination's `requires` is satisfied. Omit and every destination
   * is visible — which is what a shell rendered in isolation wants, and what
   * keeps this package free of an auth dependency.
   */
  readonly can?: (requirement: NavRequirement) => boolean;
  /** Rendered at the top of the rail and the drawer; takes the current mode. */
  readonly header?: (showLabel: boolean) => ReactNode;
  /** Rendered below the theme toggle, in a separated band; takes the current mode. */
  readonly footer?: (showLabel: boolean) => ReactNode;
  /** Overrides for any of the three control icons. */
  readonly icons?: Partial<NavControlIcons>;
  /** Overrides for any of the three control accessible names. */
  readonly labels?: Partial<Record<keyof NavControlIcons, string>>;
  /**
   * The stem every testid in the shell derives from: `{prefix}-shell`,
   * `{prefix}-nav`, `{prefix}-nav-drawer`, `{prefix}-nav-toggle`,
   * `{prefix}-nav-mobile-menu`, `{prefix}-nav-backdrop`, and `{prefix}-{id}` per
   * destination. Defaults to `"dashboard"`.
   */
  readonly testIdPrefix?: string;
  /** The routed content — an app passes its router's `<Outlet />`. */
  readonly children?: ReactNode;
}

/** Everything is visible when the app supplies no gate. */
const allowAll = (): boolean => true;

export function AppShell(props: AppShellProps) {
  const testIdPrefix: string = props.testIdPrefix ?? "dashboard";
  const icons: NavControlIcons = { ...defaultNavControlIcons, ...props.icons };
  const labels: Readonly<Record<keyof NavControlIcons, string>> = {
    ...defaultNavControlLabels,
    ...props.labels,
  };
  const controls: ControlProps = { testIdPrefix, icons, labels };

  const isDesktop: boolean | undefined = useIsDesktop();
  const isMobileNavOpen = useNavStore((state) => state.isMobileNavOpen);
  // `isDesktop === false`, not `!isDesktop`: an unresolved viewport is not a
  // mobile one. The drawer cannot be open in that window anyway — nothing has
  // been clickable yet — so a scrim there would be a scrim over nothing.
  const showBackdrop = isDesktop === false && isMobileNavOpen;

  return (
    <div data-testid={`${testIdPrefix}-shell`} className="min-h-screen flex bg-background">
      <AppNav
        destinations={props.destinations}
        can={props.can ?? allowAll}
        testIdPrefix={testIdPrefix}
        renderHeader={props.header}
        renderFooter={props.footer}
      />
      {showBackdrop ? <NavBackdrop {...controls} /> : null}
      <main className="flex-1 p-6 overflow-auto text-foreground">
        <NavControls {...controls} isDesktop={isDesktop} />
        {props.children}
      </main>
    </div>
  );
}
