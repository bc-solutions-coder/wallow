import type { MouseEvent, ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardNav } from "./DashboardNav";
import { useUiStore } from "../stores/ui-store";

/**
 * DashboardNav desktop-mode spec (Wallow-0byr.2) — the two, and only two,
 * DESKTOP states: the expanded rail (icon + label) and the collapsed icon rail
 * (icon only). The mobile overlay drawer is a different axis entirely and lives
 * in `DashboardLayout.mobile.test.tsx`.
 *
 * WHY EVERY CASE SETS A VIEWPORT: Vitest browser mode defaults to a 414x896
 * viewport, which is BELOW Tailwind's `md` breakpoint (48rem/768px) — i.e. the
 * default is a phone. Since the nav now renders a rail only at desktop widths,
 * a desktop spec that does not say so is silently a mobile spec. `page.viewport`
 * drives `matchMedia` in the real Chromium these specs run in (verified), so it
 * is the mobile/desktop switch for the whole suite.
 *
 * WHY THE COLLAPSED RAIL MUST CARRY NO TEXT NODE: no stylesheet is loaded in
 * browser mode, so a visually-hidden label and the clipped label this epic is
 * about are indistinguishable from the DOM. The only assertion that can tell
 * "deliberate icon rail" from "broken clipped text" is that the collapsed item
 * renders NO text at all and takes its accessible name from `aria-label`.
 */

// Which route the stubbed router considers active. Mutable so a case can point
// it at one item and assert only that item is highlighted.
const routerState = vi.hoisted(() => ({ activePath: "" }));

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  className?: string;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub TanStack `Link` to a plain anchor (as in `DashboardNav.test.tsx`), with
// two additions this spec needs: it applies `activeProps.className` when `to`
// matches `routerState.activePath` (that is how TanStack styles the active
// link, and the nav must supply it in BOTH desktop modes), and it suppresses
// the anchor's default navigation so a click cannot move the test iframe.
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

/** Every nav item the rail renders, with the label it must be reachable by. */
const navItems: ReadonlyArray<readonly [testid: string, label: string]> = [
  ["dashboard-nav-organizations", "Organizations"],
  ["dashboard-nav-apps", "Apps"],
  ["dashboard-nav-settings", "Settings"],
  ["dashboard-nav-inquiries", "Inquiries"],
  ["dashboard-logout-link", "Sign Out"],
];

/** Widths on either side of the `md` breakpoint the nav switches on. */
const DESKTOP_VIEWPORT = [1280, 800] as const;

beforeEach(async () => {
  routerState.activePath = "";
  useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
  await page.viewport(...DESKTOP_VIEWPORT);
});

describe("DashboardNav expanded rail (desktop)", () => {
  it("reports itself expanded", async () => {
    await render(<DashboardNav />);

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "true");
  });

  it("renders an icon for every nav item", async () => {
    await render(<DashboardNav />);

    for (const [testid] of navItems) {
      await expect.element(page.getByTestId(testid)).toBeInTheDocument();
      expect(page.getByTestId(testid).element().querySelector("svg")).not.toBeNull();
    }
  });

  it("renders the visible label alongside every icon", async () => {
    await render(<DashboardNav />);

    for (const [testid, label] of navItems) {
      await expect.element(page.getByTestId(testid)).toHaveTextContent(label);
    }
  });

  it("highlights the active route", async () => {
    routerState.activePath = "/dashboard/settings";

    await render(<DashboardNav />);
    await expect.element(page.getByTestId("dashboard-nav-settings")).toBeInTheDocument();

    // The nav must hand `Link` active-link styling, so the active item's classes
    // differ from an inactive sibling's. Asserting "differs" rather than an exact
    // Tailwind string keeps this about the behaviour, not the palette.
    const active: string = page.getByTestId("dashboard-nav-settings").element().className;
    const inactive: string = page.getByTestId("dashboard-nav-apps").element().className;
    expect(active).not.toBe(inactive);
  });
});

describe("DashboardNav collapsed icon rail (desktop)", () => {
  beforeEach(() => {
    useUiStore.setState({ isNavCollapsed: true });
  });

  it("reports itself collapsed", async () => {
    await render(<DashboardNav />);

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "false");
  });

  it("renders an icon for every nav item", async () => {
    await render(<DashboardNav />);

    for (const [testid] of navItems) {
      await expect.element(page.getByTestId(testid)).toBeInTheDocument();
      expect(page.getByTestId(testid).element().querySelector("svg")).not.toBeNull();
    }
  });

  it("renders no text node for any nav item", async () => {
    await render(<DashboardNav />);

    // The epic's actual bug: the collapsed rail rendered full-size labels and let
    // the rail clip them into "Settin" / "Inquir" / "Sign O".
    for (const [testid] of navItems) {
      await expect.element(page.getByTestId(testid)).toBeInTheDocument();
      expect(page.getByTestId(testid).element().textContent?.trim()).toBe("");
    }
  });

  it("names every icon-only nav item with an aria-label", async () => {
    await render(<DashboardNav />);

    for (const [testid, label] of navItems) {
      await expect.element(page.getByTestId(testid)).toHaveAttribute("aria-label", label);
    }
  });

  it("keeps every nav destination reachable by its accessible name", async () => {
    await render(<DashboardNav />);

    await expect.element(page.getByRole("link", { name: "Organizations" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Apps" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Settings" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Inquiries" })).toBeInTheDocument();
    await expect.element(page.getByRole("button", { name: "Sign Out" })).toBeInTheDocument();
  });

  it("highlights the active route", async () => {
    routerState.activePath = "/dashboard/settings";

    await render(<DashboardNav />);
    await expect.element(page.getByTestId("dashboard-nav-settings")).toBeInTheDocument();

    const active: string = page.getByTestId("dashboard-nav-settings").element().className;
    const inactive: string = page.getByTestId("dashboard-nav-apps").element().className;
    expect(active).not.toBe(inactive);
  });
});

describe("DashboardNav desktop toggle", () => {
  it("drops the labels when the rail collapses and restores them when it expands", async () => {
    await render(<DashboardNav />);
    await expect.element(page.getByTestId("dashboard-nav-settings")).toHaveTextContent("Settings");

    useUiStore.getState().toggleNavCollapsed();

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "false");
    expect(page.getByTestId("dashboard-nav-settings").element().textContent?.trim()).toBe("");

    useUiStore.getState().toggleNavCollapsed();

    await expect.element(page.getByTestId("dashboard-nav-settings")).toHaveTextContent("Settings");
  });

  it("keeps the rail mounted in both states so the toggle's aria-controls target survives", async () => {
    await render(<DashboardNav />);
    await expect.element(page.getByTestId("dashboard-nav")).toHaveAttribute("id", "dashboard-nav");

    useUiStore.getState().toggleNavCollapsed();

    await expect.element(page.getByTestId("dashboard-nav")).toHaveAttribute("id", "dashboard-nav");
  });
});
