import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardLayout } from "./DashboardLayout";
import { useUiStore } from "../stores/ui-store";

/**
 * Dashboard mobile overlay-drawer spec (Wallow-0byr.2) — the third nav mode.
 * Below Tailwind's `md` breakpoint there is no rail at all: a menu button
 * summons a temporary sheet over the page, and that sheet is dismissible the
 * three ways an overlay must be (backdrop, nav link, Escape).
 *
 * These cases render the whole `DashboardLayout` rather than `DashboardNav`
 * because the drawer, its backdrop, and the button that opens them are one
 * behaviour split across the shell and the nav — the spec should not care which
 * component owns which node, only that the shell produces them together.
 *
 * VIEWPORT: Vitest browser mode's 414x896 default is already below `md`, but
 * every case sets the width explicitly anyway — an implicit default is not a
 * stated intent, and the desktop specs in this directory move the viewport.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub the router primitives the shell composes (as in `DashboardLayout.test.tsx`).
// The anchor's default navigation is suppressed so that "clicking a nav link
// closes the drawer" can be asserted without the test iframe navigating away;
// the click still bubbles, so it does not matter whether the close is wired on
// the link itself or on a container above it.
vi.mock("@tanstack/react-router", () => ({
  Link: ({ to, children, onClick, ...rest }: LinkStubProps) => (
    <a
      href={to}
      onClick={(event: MouseEvent<HTMLAnchorElement>) => {
        event.preventDefault();
        onClick?.(event);
      }}
      {...rest}
    >
      {children}
    </a>
  ),
  Outlet: () => <div data-testid="dashboard-outlet-stub" />,
}));

/** A phone-width viewport, comfortably below the 48rem `md` breakpoint. */
const MOBILE_VIEWPORT = [390, 844] as const;

beforeEach(async () => {
  useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
  await page.viewport(...MOBILE_VIEWPORT);
});

describe("dashboard nav below the breakpoint", () => {
  it("renders no desktop icon rail", async () => {
    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-welcome")).toBeInTheDocument();

    // Absent, not merely narrowed or visually hidden: on a phone the rail is not
    // a thing that exists in a collapsed form, it is a thing that is not there.
    await expect.element(page.getByTestId("dashboard-nav")).not.toBeInTheDocument();
  });

  it("renders no desktop collapse toggle", async () => {
    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-welcome")).toBeInTheDocument();

    // There is no rail to collapse, so the expand/collapse control is meaningless
    // here; the menu button replaces it.
    await expect.element(page.getByTestId("dashboard-nav-toggle")).not.toBeInTheDocument();
  });

  it("renders a menu button carrying an accessible name", async () => {
    await render(<DashboardLayout />);

    const menu = page.getByTestId("dashboard-nav-mobile-menu");
    await expect.element(menu).toBeInTheDocument();
    await expect.element(menu).toHaveAttribute("aria-label", "Open navigation");
    await expect.element(menu).toHaveAttribute("aria-expanded", "false");
  });

  it("renders neither the drawer nor a backdrop while the drawer is closed", async () => {
    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-backdrop")).not.toBeInTheDocument();
  });
});

describe("dashboard mobile drawer", () => {
  it("opens a drawer with a backdrop when the menu button is activated", async () => {
    await render(<DashboardLayout />);

    await userEvent.click(page.getByTestId("dashboard-nav-mobile-menu"));

    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-backdrop")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("dashboard-nav-mobile-menu"))
      .toHaveAttribute("aria-expanded", "true");
    expect(useUiStore.getState().isMobileNavOpen).toBe(true);
  });

  it("shows every nav destination with its full label in the drawer", async () => {
    useUiStore.setState({ isMobileNavOpen: true });

    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

    // The drawer is the expanded content, not an icon rail — it has the room.
    await expect
      .element(page.getByTestId("dashboard-nav-organizations"))
      .toHaveTextContent("Organizations");
    await expect.element(page.getByTestId("dashboard-nav-apps")).toHaveTextContent("Apps");
    await expect.element(page.getByTestId("dashboard-nav-settings")).toHaveTextContent("Settings");
    await expect
      .element(page.getByTestId("dashboard-nav-inquiries"))
      .toHaveTextContent("Inquiries");
    await expect.element(page.getByTestId("dashboard-logout-link")).toHaveTextContent("Sign Out");
  });

  it("names the backdrop so it is not an unlabelled button", async () => {
    useUiStore.setState({ isMobileNavOpen: true });

    await render(<DashboardLayout />);

    await expect
      .element(page.getByTestId("dashboard-nav-backdrop"))
      .toHaveAttribute("aria-label", "Close navigation");
  });

  it("closes the drawer when the backdrop is activated", async () => {
    useUiStore.setState({ isMobileNavOpen: true });
    await render(<DashboardLayout />);

    await userEvent.click(page.getByTestId("dashboard-nav-backdrop"));

    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    expect(useUiStore.getState().isMobileNavOpen).toBe(false);
  });

  it("closes the drawer when a nav link is activated", async () => {
    useUiStore.setState({ isMobileNavOpen: true });
    await render(<DashboardLayout />);

    // Navigating away must not leave the sheet covering the page it navigated to.
    await userEvent.click(page.getByTestId("dashboard-nav-settings"));

    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    expect(useUiStore.getState().isMobileNavOpen).toBe(false);
  });

  it("closes the drawer when Escape is pressed", async () => {
    useUiStore.setState({ isMobileNavOpen: true });
    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

    await userEvent.keyboard("{Escape}");

    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    expect(useUiStore.getState().isMobileNavOpen).toBe(false);
  });

  it("leaves the desktop rail expanded when the drawer is dismissed", async () => {
    // Cross-axis guard at the RENDER level (the store's own guard is unit-tested):
    // dismissing a phone sheet must not decide how the rail looks on a laptop.
    await render(<DashboardLayout />);

    await userEvent.click(page.getByTestId("dashboard-nav-mobile-menu"));
    await userEvent.keyboard("{Escape}");

    expect(useUiStore.getState().isNavCollapsed).toBe(false);
  });
});
