import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useNavStore } from "./nav-store";
import { ShellFixture } from "./shell.fixtures";

/**
 * The nav across a resize, and from the keyboard.
 *
 * `useIsDesktop` subscribes to `matchMedia`, so a resize swaps which mode is
 * mounted while the store keeps its two flags — the rail's and the drawer's are
 * independent. `page.viewport` drives real `matchMedia`, so this is the event a
 * dragged window produces.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub the router primitive the nav composes, suppressing the anchor's default
// navigation so no click moves the test iframe.
vi.mock("@tanstack/react-router", () => ({
  Link: ({ to, children, activeProps: _activeProps, onClick, ...rest }: LinkStubProps) => (
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
}));

/** Widths on either side of the `md` (48rem) breakpoint the nav switches on. */
const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

beforeEach(async () => {
  useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
  await page.viewport(...DESKTOP_VIEWPORT);
});

describe("the nav across the breakpoint", () => {
  it("keeps the rail collapsed across a trip to a phone and back", async () => {
    useNavStore.setState({ isNavCollapsed: true });
    await render(<ShellFixture />);
    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "false");

    await page.viewport(...MOBILE_VIEWPORT);

    await expect.element(page.getByTestId("dashboard-nav")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).toBeInTheDocument();

    await page.viewport(...DESKTOP_VIEWPORT);

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "false");
    expect(useNavStore.getState().isNavCollapsed).toBe(true);
  });

  it("withdraws the drawer and its backdrop once the viewport reaches desktop", async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    await render(<ShellFixture />);
    await userEvent.click(page.getByTestId("dashboard-nav-mobile-menu"));
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

    await page.viewport(...DESKTOP_VIEWPORT);

    // The control that dismisses them — the menu button — is gone at this width,
    // so a surviving sheet and backdrop would be a trap.
    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-backdrop")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeInTheDocument();
  });

  it("swaps the desktop toggle for the menu button as the viewport narrows", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeInTheDocument();

    await page.viewport(...MOBILE_VIEWPORT);

    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-toggle")).not.toBeInTheDocument();
  });

  it("gives the drawer full labels even while the desktop rail is collapsed", async () => {
    useNavStore.setState({ isNavCollapsed: true, isMobileNavOpen: true });
    await page.viewport(...MOBILE_VIEWPORT);

    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-settings")).toHaveTextContent("Settings");
    await expect.element(page.getByTestId("dashboard-logout-link")).toHaveTextContent("Sign Out");
  });
});

describe("nav keyboard activation", () => {
  it("collapses and expands the rail from the keyboard", async () => {
    await render(<ShellFixture />);
    const toggle = page.getByTestId("dashboard-nav-toggle");
    await expect.element(toggle).toBeInTheDocument();

    toggle.element().focus();
    await userEvent.keyboard("{Enter}");

    await expect.element(toggle).toHaveAttribute("aria-expanded", "false");
    expect(useNavStore.getState().isNavCollapsed).toBe(true);

    await userEvent.keyboard(" ");

    await expect.element(toggle).toHaveAttribute("aria-expanded", "true");
    expect(useNavStore.getState().isNavCollapsed).toBe(false);
  });

  it("opens the drawer from the keyboard", async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    await render(<ShellFixture />);
    const menu = page.getByTestId("dashboard-nav-mobile-menu");
    await expect.element(menu).toBeInTheDocument();

    menu.element().focus();
    await userEvent.keyboard("{Enter}");

    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();
    await expect.element(menu).toHaveAttribute("aria-expanded", "true");
  });

  it("leaves everything alone when Escape is pressed with no drawer open", async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).toBeInTheDocument();

    // The Escape listener is registered whether or not the drawer is showing.
    await userEvent.keyboard("{Escape}");

    await expect.element(page.getByTestId("dashboard-nav-drawer")).not.toBeInTheDocument();
    expect(useNavStore.getState().isMobileNavOpen).toBe(false);
    expect(useNavStore.getState().isNavCollapsed).toBe(false);
  });
});
