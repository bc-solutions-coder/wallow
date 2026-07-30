import type { ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardNav } from "./DashboardNav";
import { useUiStore } from "../stores/ui-store";

// Stub TanStack `Link` to a plain anchor (as in `DashboardNav.test.tsx`).
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
}));

/**
 * DashboardNav drawer spec (Wallow-evd5.4.1) — the nav reads its open/closed
 * state from the `useUiStore` UI store instead of a prop, because the control
 * that flips it lives in a different component (`DashboardLayout`'s nav toggle).
 *
 * The store axis is now `isNavCollapsed`, the inverse of the `isNavOpen` this
 * spec was written against (Wallow-0byr.1); every case below drives the same
 * behaviour through the inverted flag. Wallow-0byr.2 rewrites this spec for the
 * three real nav modes.
 *
 * Contract:
 *   - the nav root carries `data-testid="dashboard-nav"` and reflects the store
 *     as `data-nav-open="true" | "false"` (styling keys off that attribute /
 *     the same boolean; the spec asserts the attribute, not computed CSS),
 *   - the nav stays MOUNTED when closed — collapsing is presentational, so the
 *     links keep their identity and the toggle's `aria-controls` target exists.
 */
describe("DashboardNav drawer state", () => {
  // Vitest browser mode's default viewport (414x896) is a phone, below the `md`
  // breakpoint at which the rail exists at all (Wallow-0byr.2) — pin a desktop
  // width so these cases keep exercising the rail they were written for.
  beforeEach(async () => {
    useUiStore.setState({ isNavCollapsed: true, isMobileNavOpen: false });
    await page.viewport(1280, 800);
  });

  it("marks the nav closed by default", async () => {
    await render(<DashboardNav />);

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "false");
  });

  it("marks the nav open when the store says it is open", async () => {
    useUiStore.setState({ isNavCollapsed: false });

    await render(<DashboardNav />);

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "true");
  });

  it("follows store changes made after it mounted", async () => {
    await render(<DashboardNav />);

    useUiStore.getState().toggleNavCollapsed();

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "true");
  });

  it("keeps the nav links mounted while the drawer is closed", async () => {
    await render(<DashboardNav />);

    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-settings")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-inquiries")).toBeInTheDocument();
  });
});
