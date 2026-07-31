import type { ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardLayout } from "./DashboardLayout";
import { useUiStore } from "../stores/ui-store";

// Stub the router primitives the shell composes.
vi.mock("@tanstack/react-router", () => ({
  Link: ({
    to,
    children,
    ...rest
  }: { to: string; children?: ReactNode } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
  Outlet: () => <div data-testid="dashboard-outlet-stub" />,
}));

/**
 * The desktop rail toggle. The shell owns the CONTROL — it stays reachable while
 * the rail is collapsed, so it cannot live inside the rail — and `DashboardNav`
 * owns the rail. Neither passes the flag to the other, which is why the state
 * lives in `useUiStore`.
 */
describe("DashboardLayout nav toggle", () => {
  // Vitest browser mode's default viewport (414x896) is a phone, below the `md`
  // breakpoint at which the shell renders this toggle at all.
  beforeEach(async () => {
    useUiStore.setState({ isNavCollapsed: true, isMobileNavOpen: false });
    await page.viewport(1280, 800);
  });

  it("renders a nav toggle control", async () => {
    await render(<DashboardLayout />);

    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeInTheDocument();
  });

  it("opens the drawer when the toggle is activated", async () => {
    await render(<DashboardLayout />);

    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));

    expect(useUiStore.getState().isNavCollapsed).toBe(false);
  });

  it("closes the drawer when the toggle is activated again", async () => {
    await render(<DashboardLayout />);

    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));
    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));

    expect(useUiStore.getState().isNavCollapsed).toBe(true);
  });

  it("reports the drawer state on the toggle via aria-expanded", async () => {
    await render(<DashboardLayout />);
    const toggle = page.getByTestId("dashboard-nav-toggle");
    await expect.element(toggle).toHaveAttribute("aria-expanded", "false");

    await userEvent.click(toggle);

    await expect.element(toggle).toHaveAttribute("aria-expanded", "true");
  });

  it("points the toggle at the nav it controls", async () => {
    await render(<DashboardLayout />);

    await expect
      .element(page.getByTestId("dashboard-nav-toggle"))
      .toHaveAttribute("aria-controls", "dashboard-nav");
    await expect.element(page.getByTestId("dashboard-nav")).toHaveAttribute("id", "dashboard-nav");
  });

  it("renders no backdrop while the rail is collapsed", async () => {
    await render(<DashboardLayout />);

    await expect.element(page.getByTestId("dashboard-nav-backdrop")).not.toBeInTheDocument();
  });

  it("renders no backdrop while the rail is expanded either", async () => {
    // Dimming the main column and dismissing by clicking away is overlay
    // behaviour, and the only overlay is the phone drawer.
    useUiStore.setState({ isNavCollapsed: false });

    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-backdrop")).not.toBeInTheDocument();
  });

  it("renders no mobile menu button at desktop width", async () => {
    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).not.toBeInTheDocument();
  });

  it("flips the nav's own open state from the shell's toggle", async () => {
    // The cross-component proof: the control and the drawer share no props, only
    // the store.
    await render(<DashboardLayout />);
    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "false");

    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "true");
  });
});
