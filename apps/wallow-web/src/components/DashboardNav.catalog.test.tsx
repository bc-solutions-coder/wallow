import type { MouseEvent, ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardNav } from "./DashboardNav";
import { useUiStore } from "../stores/ui-store";

/**
 * Catalog-migration spec for the dashboard sidebar (Wallow-m5aq.5.3) — the
 * hand-rolled nav items (`DashboardNav`'s `NavItem` / `NavDestinationList`)
 * become the catalog `NavigationMenu`.
 *
 * WHAT THE MIGRATION HAS TO BUY. The rail already got the accessible NAME of
 * each destination right (`aria-label` from `navIconLabels`, which is why the
 * icon rail is legible at all) — `DashboardNav.modes.test.tsx` pins that, and it
 * must not regress. What it never had is the STRUCTURE: four anchors dropped
 * straight into a `<nav>` announce as four loose links, with no count and no
 * "3 of 4" position. A navigation menu announces one list of four items. That
 * list, in all three modes, is what this file pins.
 *
 * THREE MODES, ONE CONTRACT (the modes themselves are `DashboardNav.modes` /
 * `DashboardLayout.mobile`'s subject, not this file's): the desktop expanded
 * rail, the desktop icon rail, and the mobile overlay drawer each render the
 * destinations through the SAME extracted list, so the list semantics must hold
 * in all three or the extraction has drifted.
 *
 * Structure is asserted by ROLE (`list` / `listitem`), accepting either the
 * native `ul`/`li` or an explicit role, because what a screen reader is handed
 * is the contract — not which element the catalog happened to render.
 *
 * VIEWPORT: Vitest browser mode's 414x896 default is below Tailwind's `md`
 * breakpoint, i.e. a phone. Every case sets the width it means, since the rail
 * only exists at desktop widths.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  className?: string;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub TanStack `Link` to a plain anchor, as every other spec in this directory
// does. The sidebar hands its destinations to the router `Link`; whether it does
// so directly or through the catalog's `render` prop is the migration's business,
// and either way the stub is what ends up in the DOM.
vi.mock("@tanstack/react-router", () => ({
  // `activeProps` is pulled out and dropped: the real Link merges it in only for
  // the active route, and letting it fall into `...rest` would put an object on
  // a DOM attribute.
  Link: ({
    to,
    children,
    className,
    activeProps: _activeProps,
    onClick,
    ...rest
  }: LinkStubProps) => (
    <a
      href={to}
      className={className}
      onClick={(event: MouseEvent<HTMLAnchorElement>) => {
        event.preventDefault();
        onClick?.(event);
      }}
      {...rest}
    >
      {children}
    </a>
  ),
}));

/** The four destinations the rail and the drawer both render. */
const destinationTestIds: readonly string[] = [
  "dashboard-nav-organizations",
  "dashboard-nav-apps",
  "dashboard-nav-settings",
  "dashboard-nav-inquiries",
];

const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

/** The single element carrying `testId`; fails loudly when absent or duplicated. */
function byTestId(testId: string): HTMLElement {
  const elements = page.getByTestId(testId).elements();
  expect(elements, `expected exactly one [data-testid="${testId}"]`).toHaveLength(1);
  return elements[0] as HTMLElement;
}

/** The list item `testId` sits in — native `<li>` or an explicit role. */
function listItemFor(testId: string): HTMLElement | null {
  return byTestId(testId).closest<HTMLElement>('li, [role="listitem"]');
}

/** The list `testId` belongs to — native `<ul>`/`<ol>` or an explicit role. */
function listFor(testId: string): HTMLElement | null {
  return byTestId(testId).closest<HTMLElement>('ul, ol, [role="list"]');
}

/**
 * Every destination is a listitem, and all four belong to the SAME list — the
 * whole assertion the migration exists for, applied identically to whichever
 * mode the caller has set up.
 */
async function expectOneNavigationList(): Promise<void> {
  for (const testId of destinationTestIds) {
    await expect.element(page.getByTestId(testId)).toBeInTheDocument();
  }

  const firstList: HTMLElement | null = listFor(destinationTestIds[0] as string);
  expect(firstList, "the nav destinations must be gathered into a list").not.toBeNull();

  for (const testId of destinationTestIds) {
    expect(listItemFor(testId), `${testId} must be a list item`).not.toBeNull();
    expect(listFor(testId), `${testId} must share the other destinations' list`).toBe(firstList);
  }

  // One item per destination, so the list announces "4 items" rather than
  // counting furniture that happens to sit beside them.
  const items = (firstList as HTMLElement).querySelectorAll(
    ':scope > li, :scope > [role="listitem"]',
  );
  expect(items).toHaveLength(destinationTestIds.length);
}

/** The navigation landmark the list must stay inside, in every mode. */
function expectListInNavigationLandmark(): void {
  const list: HTMLElement | null = listFor(destinationTestIds[0] as string);
  expect(list, "the nav destinations must be gathered into a list").not.toBeNull();
  expect(
    (list as HTMLElement).closest('nav, [role="navigation"]'),
    "the list must sit inside a navigation landmark",
  ).not.toBeNull();
}

beforeEach(() => {
  useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
});

describe("DashboardNav expanded rail (catalog NavigationMenu)", () => {
  beforeEach(async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
  });

  it("gathers every destination into one navigation list", async () => {
    await render(<DashboardNav />);

    await expectOneNavigationList();
  });

  it("keeps that list inside a navigation landmark", async () => {
    await render(<DashboardNav />);
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();

    expectListInNavigationLandmark();
  });

  it("drops the admin-gated destination from the list rather than emptying its item", async () => {
    await render(<DashboardNav isAdmin={false} />);
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();

    // Gating removes a destination; the list must shrink with it, or a screen
    // reader is told there are four items and handed three.
    await expect.element(page.getByTestId("dashboard-nav-organizations")).not.toBeInTheDocument();

    const list: HTMLElement | null = listFor("dashboard-nav-apps");
    expect(list).not.toBeNull();
    const items = (list as HTMLElement).querySelectorAll(':scope > li, :scope > [role="listitem"]');
    expect(items).toHaveLength(destinationTestIds.length - 1);
  });
});

describe("DashboardNav collapsed icon rail (catalog NavigationMenu)", () => {
  beforeEach(async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
    useUiStore.setState({ isNavCollapsed: true });
  });

  it("gathers every destination into one navigation list", async () => {
    await render(<DashboardNav />);

    await expectOneNavigationList();
  });

  it("keeps the list semantics while the labels are gone", async () => {
    await render(<DashboardNav />);
    await expectOneNavigationList();

    // The icon rail's whole premise: no rendered text, name from `aria-label`.
    // Collapsing must not take the list structure down with the labels.
    for (const testId of destinationTestIds) {
      expect(byTestId(testId).textContent?.trim()).toBe("");
      expect(byTestId(testId).getAttribute("aria-label")).not.toBeNull();
    }
  });
});

describe("DashboardNav mobile drawer (catalog NavigationMenu)", () => {
  beforeEach(async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    useUiStore.setState({ isMobileNavOpen: true });
  });

  it("gathers every destination into one navigation list", async () => {
    await render(<DashboardNav />);
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

    await expectOneNavigationList();
  });

  it("keeps that list inside a navigation landmark", async () => {
    await render(<DashboardNav />);
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

    expectListInNavigationLandmark();
  });
});
