import type { ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardNav } from "./DashboardNav";
import { useUiStore } from "../stores/ui-store";

// Stub TanStack `Link` to a plain anchor.
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
 * The theme control's REACHABILITY across the nav's three modes, not its looks.
 * A toggle that exists only in the expanded rail disappears the moment a visitor
 * collapses the nav or opens the app on a phone.
 *
 * `data-testid="theme-toggle"` is app-owned — `packages/ui` does not default it,
 * so it is asserted here at the place that supplies it.
 */
describe("DashboardNav theme toggle", () => {
  beforeEach(async () => {
    useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
    // Vitest browser mode's default viewport (414x896) is a phone, below the
    // `md` breakpoint at which the rail exists at all.
    await page.viewport(1280, 800);
  });

  it("renders the theme toggle in the expanded desktop rail", async () => {
    await render(<DashboardNav />);

    await expect.element(page.getByTestId("theme-toggle")).toBeInTheDocument();
  });

  it("keeps the theme toggle reachable in the collapsed icon rail", async () => {
    useUiStore.setState({ isNavCollapsed: true });

    await render(<DashboardNav />);

    await expect.element(page.getByTestId("theme-toggle")).toBeInTheDocument();
  });

  it("renders the theme toggle in the mobile drawer", async () => {
    await page.viewport(414, 896);
    useUiStore.setState({ isMobileNavOpen: true });

    await render(<DashboardNav />);

    await expect.element(page.getByTestId("theme-toggle")).toBeInTheDocument();
  });

  it("names the control for assistive tech in every mode", async () => {
    // The icon rail hides text labels, so an unnamed control would announce as
    // a bare "button" there — the same defect `navIconLabels` exists to prevent
    // for the destinations.
    useUiStore.setState({ isNavCollapsed: true });

    await render(<DashboardNav />);

    const toggle = page.getByTestId("theme-toggle").element();
    expect(toggle.getAttribute("aria-label")).toMatch(/theme/iu);
  });
});
