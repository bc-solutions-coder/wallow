import { assertRouterStubApplied } from "@bc-solutions-coder/testing/router-stub";
import { Link } from "@tanstack/react-router";
import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useNavStore } from "./nav-store";
import { ShellFixture } from "./shell.fixtures";

/**
 * The mobile overlay drawer. Below Tailwind's `md` breakpoint there is no rail
 * at all: a menu button summons a sheet over the page, dismissible the three
 * ways an overlay must be (backdrop, nav link, Escape). The whole shell renders
 * because the drawer, its backdrop and the button that opens them are one
 * behaviour split across the shell and the nav.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub the router primitive the nav composes, suppressing the anchor's default
// navigation so a click cannot move the test iframe. The click still bubbles, so
// the close may be wired on the link or on a container above it.
vi.mock("@tanstack/react-router", () => ({
  Link: Object.assign(
    ({ to, children, activeProps: _activeProps, onClick, ...rest }: LinkStubProps) => (
      <a
        href={to}
        data-router-stub="true"
        onClick={(event: MouseEvent<HTMLAnchorElement>) => {
          event.preventDefault();
          onClick?.(event);
        }}
        {...rest}
      >
        {children}
      </a>
    ),
    { wallowRouterStub: true },
  ),
}));

beforeEach(() => {
  assertRouterStubApplied(Link);
});

/** A phone-width viewport, comfortably below the 48rem `md` breakpoint. */
const MOBILE_VIEWPORT = [390, 844] as const;

beforeEach(async () => {
  useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
  await page.viewport(...MOBILE_VIEWPORT);
});

describe("AppNav below the breakpoint", () => {
  it("renders no desktop icon rail", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-shell")).toBeInTheDocument();

    // Absent, not narrowed or visually hidden.
    await expect.element(page.getByTestId("dashboard-nav")).not.toBeInTheDocument();
  });

  it("renders no desktop collapse toggle", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-shell")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-toggle")).not.toBeInTheDocument();
  });

  it("renders a menu button carrying an accessible name", async () => {
    await render(<ShellFixture />);

    const menu = page.getByTestId("dashboard-nav-mobile-menu");
    await expect.element(menu).toBeInTheDocument();
    await expect.element(menu).toHaveAttribute("aria-label", "Open navigation");
    await expect.element(menu).toHaveAttribute("aria-expanded", "false");
  });

  it("renders neither the drawer nor a backdrop while the drawer is closed", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-backdrop")).not.toBeInTheDocument();
  });
});

describe("AppShell mobile drawer", () => {
  it("opens a drawer with a backdrop when the menu button is activated", async () => {
    await render(<ShellFixture />);

    await userEvent.click(page.getByTestId("dashboard-nav-mobile-menu"));

    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-backdrop")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("dashboard-nav-mobile-menu"))
      .toHaveAttribute("aria-expanded", "true");
    expect(useNavStore.getState().isMobileNavOpen).toBe(true);
  });

  it("shows every nav destination with its full label in the drawer", async () => {
    useNavStore.setState({ isMobileNavOpen: true });

    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

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
    useNavStore.setState({ isMobileNavOpen: true });

    await render(<ShellFixture />);

    await expect
      .element(page.getByTestId("dashboard-nav-backdrop"))
      .toHaveAttribute("aria-label", "Close navigation");
  });

  it("closes the drawer when the backdrop is activated", async () => {
    useNavStore.setState({ isMobileNavOpen: true });
    await render(<ShellFixture />);

    await userEvent.click(page.getByTestId("dashboard-nav-backdrop"));

    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    expect(useNavStore.getState().isMobileNavOpen).toBe(false);
  });

  it("closes the drawer when a nav link is activated", async () => {
    useNavStore.setState({ isMobileNavOpen: true });
    await render(<ShellFixture />);

    await userEvent.click(page.getByTestId("dashboard-nav-settings"));

    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    expect(useNavStore.getState().isMobileNavOpen).toBe(false);
  });

  it("closes the drawer when Escape is pressed", async () => {
    useNavStore.setState({ isMobileNavOpen: true });
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

    await userEvent.keyboard("{Escape}");

    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    expect(useNavStore.getState().isMobileNavOpen).toBe(false);
  });

  it("leaves the desktop rail expanded when the drawer is dismissed", async () => {
    await render(<ShellFixture />);

    await userEvent.click(page.getByTestId("dashboard-nav-mobile-menu"));
    await userEvent.keyboard("{Escape}");

    expect(useNavStore.getState().isNavCollapsed).toBe(false);
  });

  it("derives the drawer's testid and the menu button's target from the shell's prefix", async () => {
    useNavStore.setState({ isMobileNavOpen: true });

    await render(<ShellFixture testIdPrefix="console" />);

    await expect.element(page.getByTestId("console-nav-drawer")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("console-nav-mobile-menu"))
      .toHaveAttribute("aria-controls", "console-nav-drawer");
    await expect.element(page.getByTestId("console-nav-backdrop")).toBeInTheDocument();
  });
});
