import type { ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardLayout } from "./DashboardLayout";
import { useUiStore } from "../stores/ui-store";

// Stub the router primitives the shell composes (as in `DashboardLayout.test.tsx`).
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
 * DashboardLayout nav-toggle spec (Wallow-evd5.4.1) — the affordance that makes
 * the UI store real. The shell owns the CONTROLS (they must stay reachable while
 * the drawer is collapsed, so they cannot live inside the drawer); `DashboardNav`
 * owns the drawer. Neither passes the flag to the other — that separation is the
 * reason this state is in `useUiStore` rather than a `useState` in one component.
 *
 * Contract:
 *   - `dashboard-nav-toggle` — always-rendered button; calls `toggleNav()` and
 *     reports the drawer state as `aria-expanded`, with `aria-controls` naming
 *     the nav element's id (`dashboard-nav`),
 *   - `dashboard-nav-backdrop` — rendered ONLY while the drawer is open; calls
 *     `closeNav()` (dismiss-by-clicking-outside on small screens).
 */
describe("DashboardLayout nav toggle", () => {
  beforeEach(() => {
    useUiStore.setState({ isNavOpen: false });
  });

  it("renders a nav toggle control", async () => {
    await render(<DashboardLayout />);

    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeInTheDocument();
  });

  it("opens the drawer when the toggle is activated", async () => {
    await render(<DashboardLayout />);

    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));

    expect(useUiStore.getState().isNavOpen).toBe(true);
  });

  it("closes the drawer when the toggle is activated again", async () => {
    await render(<DashboardLayout />);

    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));
    await userEvent.click(page.getByTestId("dashboard-nav-toggle"));

    expect(useUiStore.getState().isNavOpen).toBe(false);
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

  it("renders no backdrop while the drawer is closed", async () => {
    await render(<DashboardLayout />);

    await expect.element(page.getByTestId("dashboard-nav-backdrop")).not.toBeInTheDocument();
  });

  it("renders a backdrop while the drawer is open", async () => {
    useUiStore.setState({ isNavOpen: true });

    await render(<DashboardLayout />);

    await expect.element(page.getByTestId("dashboard-nav-backdrop")).toBeInTheDocument();
  });

  it("closes the drawer when the backdrop is activated", async () => {
    useUiStore.setState({ isNavOpen: true });
    await render(<DashboardLayout />);

    await userEvent.click(page.getByTestId("dashboard-nav-backdrop"));

    expect(useUiStore.getState().isNavOpen).toBe(false);
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
