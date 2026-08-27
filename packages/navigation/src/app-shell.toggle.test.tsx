import { assertRouterStubApplied } from "@bc-solutions-coder/testing/router-stub";
import { Link } from "@tanstack/react-router";
import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useNavStore } from "./nav-store";
import { ShellFixture } from "./shell.fixtures";

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub the router primitive the nav composes, suppressing the anchor's default
// navigation so no stray click can move the test iframe. `onClick` is pulled out
// of `rest` so a spread handler cannot land after — and thus replace — the one
// that calls `preventDefault`.
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

/**
 * The desktop rail toggle. The shell owns the CONTROL — it stays reachable while
 * the rail is collapsed, so it cannot live inside the rail — and `AppNav` owns
 * the rail. Neither passes the flag to the other, which is why the state lives
 * in `useNavStore`.
 */
describe("AppShell nav toggle", () => {
  // Vitest browser mode's default viewport (414x896) is a phone, below the `md`
  // breakpoint at which the shell renders this toggle at all.
  beforeEach(async () => {
    useNavStore.setState({ isNavCollapsed: true, isMobileNavOpen: false });
    await page.viewport(1280, 800);
  });

  it("renders a nav toggle control", async () => {
    await render(<ShellFixture />);

    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeInTheDocument();
  });

  it("expands the rail when the toggle is activated", async () => {
    await render(<ShellFixture />);

    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));

    expect(useNavStore.getState().isNavCollapsed).toBe(false);
  });

  it("collapses it again when the toggle is activated twice", async () => {
    await render(<ShellFixture />);

    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));
    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));

    expect(useNavStore.getState().isNavCollapsed).toBe(true);
  });

  it("reports the rail state on the toggle via aria-expanded", async () => {
    await render(<ShellFixture />);
    const toggle = page.getByTestId("dashboard-nav-toggle");
    await expect.element(toggle).toHaveAttribute("aria-expanded", "false");

    await userEvent.click(toggle);

    await expect.element(toggle).toHaveAttribute("aria-expanded", "true");
  });

  it("points the toggle at the nav it controls", async () => {
    await render(<ShellFixture />);

    await expect
      .element(page.getByTestId("dashboard-nav-toggle"))
      .toHaveAttribute("aria-controls", "dashboard-nav");
    await expect.element(page.getByTestId("dashboard-nav")).toHaveAttribute("id", "dashboard-nav");
  });

  it("derives both the control's testid and its target from the shell's prefix", async () => {
    await render(<ShellFixture testIdPrefix="console" />);

    await expect
      .element(page.getByTestId("console-nav-toggle"))
      .toHaveAttribute("aria-controls", "console-nav");
    await expect.element(page.getByTestId("console-nav")).toHaveAttribute("id", "console-nav");
  });

  it("renders no backdrop while the rail is collapsed", async () => {
    await render(<ShellFixture />);

    await expect.element(page.getByTestId("dashboard-nav-backdrop")).not.toBeInTheDocument();
  });

  it("renders no backdrop while the rail is expanded either", async () => {
    // Dimming the main column and dismissing by clicking away is overlay
    // behaviour, and the only overlay is the phone drawer.
    useNavStore.setState({ isNavCollapsed: false });

    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-backdrop")).not.toBeInTheDocument();
  });

  it("renders no mobile menu button at desktop width", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).not.toBeInTheDocument();
  });

  it("flips the nav's own open state from the shell's toggle", async () => {
    // The cross-component proof: the control and the rail share no props, only
    // the store.
    await render(<ShellFixture />);
    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "false");

    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "true");
  });
});
