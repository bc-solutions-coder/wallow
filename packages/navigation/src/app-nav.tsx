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
 * The shell's primary navigation — the rail and the drawer. Not exported: it is
 * rendered by `AppShell`, which owns the controls that drive it.
 *
 * THREE MODES, TWO AXES. Nav state is read from `useNavStore`, never from props:
 * the controls that flip it live in `AppShell`'s main column, so the two share
 * only the store.
 *
 *   desktop expanded  (isNavCollapsed === false) — `w-64` rail, icon + label.
 *   desktop icon rail (isNavCollapsed === true)  — `w-16` rail, icon ONLY, with
 *                       the label moved to `aria-label`. It is NOT hidden text:
 *                       rendering the label and letting the rail clip it into
 *                       "Settin" / "Sign O" is the bug this component exists for.
 *   mobile drawer     (below `md`, isMobileNavOpen) — no rail exists at all; the
 *                       drawer is a temporary sheet over the page with the full
 *                       expanded content, dismissed by backdrop, nav link, or
 *                       Escape.
 *
 * Those three are the states a visitor can be IN. Before them sits one render in
 * which the width is not yet known — `useIsDesktop() === undefined` on the
 * server and through hydration — and it is resolved by CSS, not by picking a
 * mode; see `NavRail`'s `hideBelowMd`.
 *
 * `data-nav-open` (the inverse of `isNavCollapsed`) stays the attribute styling
 * and the specs key off. Collapsing stays presentational: the aside stays
 * mounted, so the links and the toggle's `aria-controls` target
 * `#{testIdPrefix}-nav` keep their identity across both desktop states.
 */

/*
 * The rail's palette is NAMED, not mixed. `bg-foreground text-background` used to
 * invert the two page colours, which only lands on a sidebar by coincidence — in
 * dark mode it painted a glaring light rail against a dark page.
 * `--color-sidebar` / `--color-sidebar-foreground` / `--color-sidebar-accent`
 * name the surface instead, so both modes are deliberate and a fork rebrands the
 * rail from `branding.json`.
 *
 * WHO OWNS A ROW'S COLOUR. Not this file, for the rows that are catalog
 * components. A destination is a `NavigationMenu.Link`, so what rendered was
 * `twMerge(navigationMenuLinkRecipe(), itemClass)` and the recipe painted from
 * the PAGE palette — which left this file naming one class per recipe colour
 * purely to out-rank it. twMerge only drops a class the caller CONFLICTS with,
 * variant included, so the day the list lost its `hover:text-sidebar-foreground`
 * entry the recipe's `hover:text-accent-foreground` stood back up and hovered
 * labels fell to 1.27:1 in light mode, with the suite still green. A list you can
 * silently drop an entry from is not a mechanism. The catalog now takes
 * `surface="sidebar"` and paints its own inverted rest/hover/active states, so
 * `navRowClass` is GEOMETRY ONLY and `shell-source.test.ts` holds it there.
 */
const navRowClass =
  "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium whitespace-nowrap no-underline";
const navRowIconOnlyClass = `${navRowClass} justify-center`;

/**
 * The geometry a footer control needs to sit flush with the destination rows.
 *
 * Exported because the footer is a caller's slot: its control is not a
 * `NavigationMenu.Link`, so it gets none of the catalog's row treatment and
 * would otherwise have to re-derive this. GEOMETRY ONLY — no colour. A footer
 * control on the inverted rail states its own rest/hover pair, because what it
 * is (a button, an anchor, a menu trigger) decides which palette applies.
 *
 * @param showLabel The mode the slot was handed: `false` is the collapsed icon
 *   rail, which centres its single glyph.
 */
export function navRowClassName(showLabel: boolean): string {
  return showLabel ? navRowClass : navRowIconOnlyClass;
}
/** Active-route styling handed to `Link`'s `activeProps`, so both modes share it. */
const activeItemClass = "bg-sidebar-accent text-sidebar-foreground";
const iconClass = "size-5 shrink-0";

/**
 * One nav destination — a `NavigationMenu.Item` (`<li>`) holding a
 * `NavigationMenu.Link` (`<a>`), which is the flat "row that navigates" shape
 * Base UI supports directly inside an item.
 *
 * The routing still belongs to TanStack: `render` hands the anchor over to the
 * router `Link`, so `to` resolves and `activeProps` supplies active-route
 * styling, while the catalog contributes the list semantics and the row's own
 * inverted palette.
 *
 * The accessible name always comes from the destination's `label`, whether or
 * not it is also rendered, which is what makes "same icon, same name in all three
 * modes" structural rather than hand-maintained. The icon is decorative
 * (`aria-hidden`) precisely because the name is on the item.
 *
 * `surface="sidebar"` is what tells the catalog it is being composed onto the
 * inverted rail. Nothing in the DOM could say so on its own, which is why it is
 * a prop and not something the recipe sniffs.
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
 * The destination list. Extracted from both the rail and the drawer so the two
 * cannot drift, and so neither exceeds oxlint's `react/jsx-max-depth` budget.
 *
 * `NavigationMenu.Root` is the `<nav>` landmark and `NavigationMenu.List` the
 * `<ul>` inside it, so the destinations announce as ONE list of N items rather
 * than N loose links — and a gated-away destination shrinks the count instead of
 * leaving an empty row behind, because the gate drops the whole `Item`.
 */
function NavDestinationList(props: {
  destinations: readonly NavDestination[];
  can: (requirement: NavRequirement) => boolean;
  testIdPrefix: string;
  showLabels: boolean;
  onNavigate?: () => void;
}) {
  return (
    <NavigationMenu.Root className="flex-1 flex-col px-4 py-4">
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

/**
 * The theme control, in the band above the footer so it is reachable in all
 * THREE nav modes — a toggle that existed only in the expanded rail would vanish
 * the moment a visitor collapsed the nav or opened the app on a phone.
 *
 * The catalog control always renders its state as text ("Light"/"Dark"/
 * "System"), so the icon rail gets a smaller box rather than the label-stripping
 * treatment the destinations get: there is no icon to fall back to.
 *
 * `surface="sidebar"` reaches `buttonRecipe` through `ThemeToggle`'s passthrough.
 * Without it the toggle wears the button's hard-coded `variant="secondary"`,
 * which in light mode is an L 0.92 chip glued to an L 0.22 rail.
 *
 * It is built in rather than left to a slot because it needs exactly what only
 * the rail knows — which mode is rendering, and that the surface is inverted —
 * and because it reaches no further than `@bc-solutions-coder/ui`, which this
 * package already depends on. The footer slot exists for the opposite case.
 */
function NavThemeToggle(props: { showLabel: boolean }) {
  return (
    <div className="px-4 pt-4">
      <ThemeToggle
        data-testid="theme-toggle"
        surface="sidebar"
        className={props.showLabel ? "w-full" : "w-full px-1 text-xs"}
      />
    </div>
  );
}

/**
 * The caller-supplied band below the theme toggle.
 *
 * `footer` is a SLOT rather than a built-in sign-out row for one concrete
 * reason: the only sign-out this repo has POSTs to the BFF through
 * `@bc-solutions-coder/sdk`, and building it in would give this package an `sdk`
 * edge for one button. The app owns the button, its error handling and its
 * testid; the package owns the band it sits in — the separator and the padding
 * that make it sit flush with the rows above — and hands down `showLabel` so the
 * control can match the destination rows in both desktop modes.
 */
function NavFooter(props: { children?: ReactNode }) {
  if (props.children === undefined) {
    return null;
  }
  return <div className="px-4 py-4 border-t border-sidebar-accent">{props.children}</div>;
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
 * The persistent desktop rail — expanded or narrowed to icons, never absent.
 *
 * `hideBelowMd` is the pre-hydration treatment: while `useIsDesktop` still
 * answers `undefined` the rail is emitted but left to the `md` media query, so a
 * phone's FIRST PAINT already omits it. `display: none` is not a half measure —
 * it takes the rail out of the layout, the tab order and the accessibility tree
 * exactly as not rendering it would, for the one render before the real viewport
 * is known. Once it is known the rail is either mounted unconditionally visible
 * or not mounted at all, which is why this stays a pre-hydration escape hatch
 * rather than the steady state.
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
      <NavFooter>{props.renderFooter?.(showLabels)}</NavFooter>
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
      <NavFooter>{props.renderFooter?.(true)}</NavFooter>
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
