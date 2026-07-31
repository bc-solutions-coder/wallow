import type { MouseEvent, ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  byTestId,
  expectClasses,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
} from "@shared/testing/style-contract";
import { DashboardNav } from "./DashboardNav";
import { useUiStore } from "../stores/ui-store";

/**
 * Which token paints which node of the dashboard sidebar.
 *
 * `bg-foreground text-background` names no colour — it swaps the two page
 * colours, which in dark mode is a glaring light rail on a dark page. The
 * `sidebar` tokens name the surface instead, so a fork rebrands the rail from
 * `branding.json`. `sidebar-accent` serves hover AND active.
 */

// Which route the stubbed router considers active, so a case can read the
// classes `activeProps` merged in.
const routerState = vi.hoisted(() => ({ activePath: "" }));

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  className?: string;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub TanStack `Link` to a plain anchor that applies `activeProps.className` on
// the active route — the only way that treatment reaches the DOM.
vi.mock("@tanstack/react-router", () => ({
  Link: ({ to, children, className, activeProps, onClick, ...rest }: LinkStubProps) => (
    <a
      href={to}
      className={[className, to === routerState.activePath ? activeProps?.className : undefined]
        .filter(Boolean)
        .join(" ")}
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

const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

/** The token pair that MEANS "the inverted nav surface". */
const SIDEBAR_SURFACE = "bg-sidebar text-sidebar-foreground";

/** The hand-rolled inversion, asserted absent wherever the pair belongs. */
const RETIRED_SURFACE: readonly string[] = ["bg-foreground", "text-background"];

/** A nav row at rest: full-strength label, no surface until hover. */
const ROW_AT_REST = "text-sidebar-foreground hover:bg-sidebar-accent";

/** Hand-mixed row utilities — a `/80` tint and two page-colour overlays. */
const RETIRED_ROW: readonly string[] = [
  "text-background/80",
  "hover:bg-background/10",
  "hover:text-background",
  "text-background",
];

/**
 * The catalog hover colour a row must never end up wearing. A row is a
 * `NavigationMenu.Link`, so the DOM gets `twMerge(recipe, itemClass)`, and
 * twMerge drops a recipe class only where the caller conflicts AT THE SAME
 * VARIANT — a bare `text-sidebar-foreground` does not, leaving the label
 * painting L 0.22 on L 0.30 under the cursor.
 */
const RECIPE_HOVER_COLOR = "hover:text-accent-foreground";

/** `element` carries none of `classes`. */
function expectRetired(element: Element, classes: readonly string[]): void {
  const survivors = classes.filter((cls) => element.classList.contains(cls));

  expect(survivors, `<${element.tagName.toLowerCase()}> still inverts by hand`).toEqual([]);
}

beforeEach(() => {
  routerState.activePath = "";
  useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
});

describe("DashboardNav rail surface", () => {
  beforeEach(async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
  });

  it("paints the rail with the sidebar token pair", async () => {
    await render(<DashboardNav />);
    const rail = await waitForTestId("dashboard-nav");

    expectClasses(rail, SIDEBAR_SURFACE);
    expectRetired(rail, RETIRED_SURFACE);
    expectTokenColorsOnly(rail);
  });

  it("paints the collapsed icon rail identically", async () => {
    useUiStore.setState({ isNavCollapsed: true });

    await render(<DashboardNav />);
    const rail = await waitForTestId("dashboard-nav");

    // Collapsing is a WIDTH change and nothing else.
    expectClasses(rail, SIDEBAR_SURFACE);
    expectRetired(rail, RETIRED_SURFACE);
  });
});

describe("DashboardNav drawer surface", () => {
  beforeEach(async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    useUiStore.setState({ isMobileNavOpen: true });
  });

  it("paints the mobile drawer with the same pair as the rail", async () => {
    await render(<DashboardNav />);
    const drawer = await waitForTestId("dashboard-nav-drawer");

    expectClasses(drawer, SIDEBAR_SURFACE);
    expectRetired(drawer, RETIRED_SURFACE);
    expectTokenColorsOnly(drawer);
  });
});

describe("DashboardNav rows", () => {
  beforeEach(async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
  });

  it("gives every destination the same token row treatment", async () => {
    await render(<DashboardNav />);
    await waitForTestId("dashboard-nav-apps");

    for (const testId of [
      "dashboard-nav-organizations",
      "dashboard-nav-apps",
      "dashboard-nav-settings",
      "dashboard-nav-inquiries",
    ]) {
      const row = byTestId(testId);
      expectClasses(row, ROW_AT_REST);
      expectRetired(row, RETIRED_ROW);

      // The merged output, not the handed-in string.
      expectClasses(row, "hover:text-sidebar-foreground");
      expect(
        row.classList.contains(RECIPE_HOVER_COLOR),
        `${testId} lets the catalog's ${RECIPE_HOVER_COLOR} survive the merge`,
      ).toBe(false);
    }
  });

  it("gives Sign Out the destinations' row treatment", async () => {
    await render(<DashboardNav />);
    const logout = await waitForTestId("dashboard-logout-link");

    // Sign Out is a `<button>` rather than a `Link`, but the same row.
    expectClasses(logout, ROW_AT_REST);
    expectRetired(logout, RETIRED_ROW);
  });

  it("marks the active route with the sidebar accent surface", async () => {
    routerState.activePath = "/dashboard/settings";

    await render(<DashboardNav />);
    const active = await waitForTestId("dashboard-nav-settings");

    expectClasses(active, "bg-sidebar-accent text-sidebar-foreground");
    expectRetired(active, ["bg-background/15", "text-background"]);

    // `sidebar-accent` serves hover AND active, so the inactive row is the half
    // of this that a rail painting every row would fail.
    expect(byTestId("dashboard-nav-apps").classList.contains("bg-sidebar-accent")).toBe(false);
  });

  it("divides Sign Out from the destinations with the accent border", async () => {
    await render(<DashboardNav />);
    const logout = await waitForTestId("dashboard-logout-link");
    const footer = parentOf(logout);

    expectClasses(footer, "border-t border-sidebar-accent");
    expectRetired(footer, ["border-background/10"]);
  });
});
