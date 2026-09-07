// The per-component subpath, NOT the root barrel: the barrel also pulls in
// `FocusOnNavigate`, which imports `useRouterState`, and the specs around this
// component stub `@tanstack/react-router` down to `Link` alone. Bundlers
// tree-shake that away; a dev/test module graph does not, so the barrel would
// fail to link here without changing a single rendered class.
import { NavigationMenu } from "@bc-solutions-coder/ui/navigation-menu";
import { ThemeToggle } from "@bc-solutions-coder/ui/theme-toggle";
import { Link } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { useEffect } from "react";

import type { NavDestination, NavRequirement } from "./destinations";
import { useNavStore } from "./nav-store";
import { useIsDesktop } from "./use-is-desktop";

/**
 * Render the shared destination list as a desktop rail or mobile drawer. The desktop rail
 * preserves its element identity when collapsed. Before hydration determines the viewport,
 * responsive CSS controls rail visibility.
 */

/**
 * NavigationMenu.Link owns sidebar colors through surface="sidebar". Row classes here supply
 * layout only, so hover and active colors remain consistent with the component recipe.
 */
const navRowClass =
  "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium whitespace-nowrap no-underline";
const navRowIconOnlyClass = `${navRowClass} justify-center`;

/**
 * Return layout classes matching destination rows, centered when showLabel is false. This helper
 * supplies no colors; footer controls must choose their own sidebar surface styles.
 */
export function navRowClassName(showLabel: boolean): string {
  return showLabel ? navRowClass : navRowIconOnlyClass;
}
/** Active-route styling handed to `Link`'s `activeProps`, so both modes share it. */
const activeItemClass = "bg-sidebar-accent text-sidebar-foreground";
const iconClass = "size-5 shrink-0";

/**
 * Compose a sidebar NavigationMenu link with TanStack Router for navigation and active-route
 * styling. The destination label supplies the accessible name even when only the decorative icon
 * is visible.
 */
function NavItem(props: {
  destination: NavDestination;
  testId: string;
  showLabel: boolean;
  onNavigate?: () => void;
}) {
  const Icon = props.destination.icon;
  return (
    <NavigationMenu.Item>
      <NavigationMenu.Link
        render={<Link to={props.destination.to} activeProps={{ className: activeItemClass }} />}
        data-testid={props.testId}
        aria-label={props.destination.label}
        surface="sidebar"
        className={props.showLabel ? navRowClass : navRowIconOnlyClass}
        onClick={props.onNavigate}
      >
        <Icon aria-hidden="true" className={iconClass} />
        {props.showLabel ? props.destination.label : null}
      </NavigationMenu.Link>
    </NavigationMenu.Item>
  );
}

/**
 * Render one navigation list shared by the rail and drawer. A hidden destination removes its
 * entire list item.
 */
function NavDestinationList(props: {
  destinations: readonly NavDestination[];
  can: (requirement: NavRequirement) => boolean;
  testIdPrefix: string;
  showLabels: boolean;
  onNavigate?: () => void;
}) {
  return (
    <NavigationMenu.Root className={`flex-1 flex-col py-4 ${props.showLabels ? "px-4" : "px-2"}`}>
      <NavigationMenu.List className="flex-col">
        {props.destinations.map((destination: NavDestination) =>
          destination.requires !== undefined && !props.can(destination.requires) ? null : (
            <NavItem
              key={destination.id}
              destination={destination}
              testId={`${props.testIdPrefix}-${destination.id}`}
              showLabel={props.showLabels}
              onNavigate={props.onNavigate}
            />
          ),
        )}
      </NavigationMenu.List>
    </NavigationMenu.Root>
  );
}

/** The theme preference stays reachable in every navigation mode. */
function NavThemeToggle(props: { showLabel: boolean }) {
  return (
    <div className={`flex py-4 ${props.showLabel ? "px-4" : "justify-center px-2"}`}>
      <ThemeToggle data-testid="theme-toggle" surface="sidebar" presentation="icon" />
    </div>
  );
}

/**
 * Footer content is supplied by the app so sign-out and other actions can use the app
 * authentication and error handling. The shell supplies spacing; showLabel lets the content adapt
 * to the icon rail.
 */
function NavFooter(props: { children?: ReactNode; showLabel: boolean }) {
  if (props.children === undefined) {
    return null;
  }
  return (
    <div className={`py-4 border-t border-sidebar-accent ${props.showLabel ? "px-4" : "px-2"}`}>
      {props.children}
    </div>
  );
}

/** The caller-supplied band above the destinations — an org switcher, a brand mark. */
function NavHeader(props: { children?: ReactNode }) {
  if (props.children === undefined) {
    return null;
  }
  return <div className="px-4 pt-4">{props.children}</div>;
}

const railClass =
  "relative z-30 w-16 data-[nav-open=true]:w-64 bg-sidebar text-sidebar-foreground flex-col shrink-0 transition-[width] duration-200";

/** What both the rail and the drawer need to render their contents. */
interface NavContentProps {
  destinations: readonly NavDestination[];
  can: (requirement: NavRequirement) => boolean;
  testIdPrefix: string;
  renderHeader?: (showLabel: boolean) => ReactNode;
  renderFooter?: (showLabel: boolean) => ReactNode;
}

/**
 * Keep the desktop rail mounted through collapse changes. Before the viewport is observed,
 * hideBelowMd delegates visibility to CSS; display:none also removes it from keyboard navigation
 * and the accessibility tree.
 */
function NavRail(props: NavContentProps & { hideBelowMd?: boolean }) {
  const isNavCollapsed = useNavStore((state) => state.isNavCollapsed);
  const showLabels = !isNavCollapsed;
  return (
    <aside
      id={`${props.testIdPrefix}-nav`}
      data-testid={`${props.testIdPrefix}-nav`}
      data-nav-open={isNavCollapsed ? "false" : "true"}
      className={`${props.hideBelowMd ? "hidden md:flex" : "flex"} ${railClass}`}
    >
      <NavHeader>{props.renderHeader?.(showLabels)}</NavHeader>
      <NavDestinationList
        destinations={props.destinations}
        can={props.can}
        testIdPrefix={props.testIdPrefix}
        showLabels={showLabels}
      />
      <NavThemeToggle showLabel={showLabels} />
      <NavFooter showLabel={showLabels}>{props.renderFooter?.(showLabels)}</NavFooter>
    </aside>
  );
}

/**
 * The mobile overlay drawer — expanded content, because a sheet over the page has
 * the room a rail does not. Navigating dismisses it, so it never covers the page
 * it just navigated to.
 */
function NavDrawer(props: NavContentProps) {
  const closeMobileNav = useNavStore((state) => state.closeMobileNav);
  return (
    <div
      id={`${props.testIdPrefix}-nav-drawer`}
      data-testid={`${props.testIdPrefix}-nav-drawer`}
      className="fixed inset-y-0 left-0 z-30 w-64 bg-sidebar text-sidebar-foreground flex flex-col"
    >
      <NavHeader>{props.renderHeader?.(true)}</NavHeader>
      <NavDestinationList
        destinations={props.destinations}
        can={props.can}
        testIdPrefix={props.testIdPrefix}
        showLabels
        onNavigate={closeMobileNav}
      />
      <NavThemeToggle showLabel />
      <NavFooter showLabel>{props.renderFooter?.(true)}</NavFooter>
    </div>
  );
}

export function AppNav(props: NavContentProps) {
  const isDesktop = useIsDesktop();
  const isMobileNavOpen = useNavStore((state) => state.isMobileNavOpen);
  const closeMobileNav = useNavStore((state) => state.closeMobileNav);

  // Escape dismisses the drawer. The listener is re-registered per open state
  // rather than gated with an early return so the effect has one exit path.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      if (isMobileNavOpen && event.key === "Escape") {
        closeMobileNav();
      }
    };
    globalThis.addEventListener("keydown", onKeyDown);
    return () => {
      globalThis.removeEventListener("keydown", onKeyDown);
    };
  }, [isMobileNavOpen, closeMobileNav]);

  // No viewport observed yet (server render, first hydration render): hand the
  // breakpoint to CSS instead of guessing one. The drawer is not a candidate
  // here — it opens only from a control the visitor has not been able to press.
  if (isDesktop === undefined) {
    return <NavRail {...props} hideBelowMd />;
  }
  if (!isDesktop) {
    return isMobileNavOpen ? <NavDrawer {...props} /> : null;
  }
  return <NavRail {...props} />;
}
