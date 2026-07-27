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
 *   - `dashboard-nav-backdrop` — NOT a desktop affordance (Wallow-0byr.2): the
 *     rail is persistent furniture, so nothing dims behind it in either state.
 *     The backdrop belongs to the mobile overlay drawer, covered in
 *     `DashboardLayout.mobile.test.tsx`.
 *
 * The store axis is `isNavCollapsed`, the inverse of the `isNavOpen` this spec
 * was written against (Wallow-0byr.1); every case below drives the same
 * behaviour through the inverted flag. This file stays the DESKTOP toggle spec;
 * the collapsed rail's rendering is `DashboardNav.modes.test.tsx` and the phone
 * drawer is `DashboardLayout.mobile.test.tsx`.
 */
describe("DashboardLayout nav toggle", () => {
  // Vitest browser mode's default viewport (414x896) is a phone, below the `md`
  // breakpoint at which the shell renders this toggle at all (Wallow-0byr.2) —
  // pin a desktop width so these cases keep exercising the control they name.
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
    // The backdrop moved to the MOBILE axis in Wallow-0byr.2. A desktop rail is
    // persistent furniture, not an overlay: dimming the whole main column behind
    // it and dismissing it by clicking away is overlay behaviour, and the
    // overlay is now the phone drawer. Backdrop coverage lives in
    // `DashboardLayout.mobile.test.tsx`, wired to `closeMobileNav`.
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
