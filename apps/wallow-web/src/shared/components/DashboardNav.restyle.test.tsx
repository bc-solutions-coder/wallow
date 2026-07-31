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
 * Token spec for the dashboard sidebar (Wallow-lrlm.5.4).
 *
 * The rail and the drawer used to invert themselves with `bg-foreground
 * text-background` and then reach for FOUR different opacity modifiers on top of
 * it (`text-background/80`, `hover:bg-background/10`, `bg-background/15`,
 * `border-background/10`). That pair is not a colour this theme names, it is an
 * instruction to swap the two page colours — which in LIGHT mode happens to land
 * on the sidebar palette and in DARK mode produces a glaring light rail against
 * a dark page. `--color-sidebar` / `--color-sidebar-foreground` /
 * `--color-sidebar-accent` (Wallow-lrlm.1.1) name the surface instead, so both
 * modes paint deliberately and a fork rebrands the rail from `branding.json`.
 *
 * WHAT `sidebar-accent` BUYS. The two hand-mixed overlays were the same idea at
 * two strengths: 10% of the page background over the rail for hover, 15% for the
 * active route. In light mode `oklch(0.97 0.008 70)` at 10% over
 * `oklch(0.22 0.035 45)` is `oklch(0.30 …)` — which is `sidebarAccent`'s value
 * EXACTLY (`api/branding.json`). The token was cut for this job; both states now
 * take it, and the active row stays distinguishable from an idle one because it
 * alone carries a surface at all.
 *
 * BEHAVIOUR STAYS PINNED ELSEWHERE. The three modes, the list semantics, the
 * accessible names and the "active differs from inactive" contract belong to
 * `DashboardNav.modes` / `.catalog` / `.destinations`, which this spec does not
 * touch. Here the subject is only which token paints which node.
 *
 * VIEWPORT: Vitest browser mode's 414x896 default is a phone, below Tailwind's
 * `md` breakpoint, so every case states the width whose mode it means.
 */

// Which route the stubbed router considers active, so a case can point it at one
// destination and read the classes `activeProps` merged in.
const routerState = vi.hoisted(() => ({ activePath: "" }));

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  className?: string;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub TanStack `Link` to a plain anchor that applies `activeProps.className` on
// the active route, as `DashboardNav.modes.test.tsx` does — the active-row
// treatment is handed to the router, so it only reaches the DOM through the stub.
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

/** The token pair that MEANS "the inverted nav surface", replacing the swap. */
const SIDEBAR_SURFACE = "bg-sidebar text-sidebar-foreground";

/** The retired inversion, asserted absent wherever the pair used to be. */
const RETIRED_SURFACE: readonly string[] = ["bg-foreground", "text-background"];

/** A nav row at rest: full-strength label, no surface until hover. */
const ROW_AT_REST = "text-sidebar-foreground hover:bg-sidebar-accent";

/** The retired row utilities — the `/80` tint and the two hand-mixed overlays. */
const RETIRED_ROW: readonly string[] = [
  "text-background/80",
  "hover:bg-background/10",
  "hover:text-background",
  "text-background",
];

/**
 * The catalog hover colour a row must never end up wearing.
 *
 * A destination row is a `NavigationMenu.Link`, so the class attribute that
 * reaches the DOM is `twMerge(navigationMenuLinkRecipe(), itemClass)` — the
 * recipe contributes `hover:text-accent-foreground`, which twMerge only drops
 * when the caller supplies a class conflicting with it AT THE SAME VARIANT. A
 * bare `text-sidebar-foreground` does not conflict with a `hover:` class, so the
 * row keeps painting `accent-foreground` (light mode: L 0.22) on
 * `sidebar-accent` (L 0.30) and the label vanishes under the cursor. Only
 * `hover:text-sidebar-foreground` suppresses it.
 *
 * Every other assertion here reads the classes this file HANDS to the catalog;
 * this one reads what the merge PRODUCED, which is the only place the defect was
 * ever visible.
 */
const RECIPE_HOVER_COLOR = "hover:text-accent-foreground";

/** `element` carries none of `classes` (the assertion the migration is for). */
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

    // Collapsing is a WIDTH change and nothing else. A rail that only took the
    // token in one of its two desktop states would be two rails.
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

      // The merged output, not the handed-in string: the row must suppress the
      // link recipe's own hover colour, or hovering it drops the label to ~1.3:1
      // against `sidebar-accent` in light mode.
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

    // Sign Out is a `<button>` rather than a `Link`, but it is the same ROW: it
    // shares `itemClass`, so a migration that missed it would leave one row on
    // the retired tint next to four that moved.
    expectClasses(logout, ROW_AT_REST);
    expectRetired(logout, RETIRED_ROW);
  });

  it("marks the active route with the sidebar accent surface", async () => {
    routerState.activePath = "/dashboard/settings";

    await render(<DashboardNav />);
    const active = await waitForTestId("dashboard-nav-settings");

    expectClasses(active, "bg-sidebar-accent text-sidebar-foreground");
    expectRetired(active, ["bg-background/15", "text-background"]);

    // The idle sibling must NOT take the accent surface: `sidebar-accent` now
    // serves hover AND active, and asserting only that the active row has it
    // would pass a rail that painted every row.
    expect(byTestId("dashboard-nav-apps").classList.contains("bg-sidebar-accent")).toBe(false);
  });

  it("divides Sign Out from the destinations with the accent border", async () => {
    await render(<DashboardNav />);
    const logout = await waitForTestId("dashboard-logout-link");
    const footer = parentOf(logout);

    // `border-background/10` mixed the same overlay the row states did, so the
    // rule takes the same token rather than a fifth hand-mixed value.
    expectClasses(footer, "border-t border-sidebar-accent");
    expectRetired(footer, ["border-background/10"]);
  });
});
