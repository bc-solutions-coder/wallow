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
 * Application frame with a desktop rail, mobile drawer, and main content. The app supplies routed
 * children and evaluates destination requirements; the shell and navigation share presentation
 * state through useNavStore.
 */

/**
 * Navigation controls use the page-surface outline icon button. width="auto" prevents the button
 * default from stretching across the content column; layout remains the shell responsibility.
 */
const navControlLayout = "relative z-20 mb-4";

/**
 * Before the viewport is known, render both controls and let responsive CSS show one. The desktop
 * class must restore inline-flex because hidden replaces the button base display class.
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
 * Translucent backdrop color for the mobile drawer. Keep this literal separate because the
 * sidebar palette lint rule permits transparency here.
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
   * Return whether to show a destination with requirements. Called only when requires is present;
   * omission shows every destination. This predicate does not protect routes or API calls.
   */
  readonly can?: (requirement: NavRequirement) => boolean;
  /** Rendered at the top of the rail and the drawer; receives false for the icon-only rail and true otherwise. */
  readonly header?: (showLabel: boolean) => ReactNode;
  /** Rendered below the theme toggle, in a separated band; receives false for the icon-only rail and true otherwise. */
  readonly footer?: (showLabel: boolean) => ReactNode;
  /** Override rail and mobile-menu icons. The close slot is accepted but currently not rendered. */
  readonly icons?: Partial<NavControlIcons>;
  /** Overrides for any of the three control accessible names. */
  readonly labels?: Partial<Record<keyof NavControlIcons, string>>;
  /**
   * The stem for shell and destination test IDs; the theme toggle keeps theme-toggle: `{prefix}-shell`,
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

/**
 * Render application navigation and main content under TanStack Router. At 48rem and above, show
 * the collapsible rail; below it, show a menu button and optional drawer. Destination
 * requirements only affect visibility through can; the app owns route and API authorization.
 */
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
