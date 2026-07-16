/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

import { DashboardLayout } from "./DashboardLayout";

// No global `expect` (vitest `globals` is off); register jest-dom matchers.
expect.extend(matchers);

// `DashboardLayout` composes `DashboardNav` (TanStack `Link`s) and a router
// `<Outlet/>`. Both require live router context, so stub them: `Link` becomes a
// plain anchor (passing `to` -> `href` + testids), and `Outlet` becomes a marked
// placeholder we can assert on to prove the layout hosts an outlet for children.
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
 * DashboardLayout spec (Wallow-8w1h.8.1). The authenticated shell must render:
 *   - a root carrying `data-testid="dashboard-welcome"`,
 *   - the nav shell (its `dashboard-nav-*` links), and
 *   - a router `<Outlet/>` (the mount point for the reparented child routes).
 */
describe("DashboardLayout", () => {
  it("renders a shell root carrying data-testid=dashboard-welcome", () => {
    render(<DashboardLayout />);
    expect(screen.getByTestId("dashboard-welcome")).toBeInTheDocument();
  });

  it("renders the dashboard nav (all four vertical links)", () => {
    render(<DashboardLayout />);
    expect(screen.getByTestId("dashboard-nav-organizations")).toBeInTheDocument();
    expect(screen.getByTestId("dashboard-nav-apps")).toBeInTheDocument();
    expect(screen.getByTestId("dashboard-nav-settings")).toBeInTheDocument();
    expect(screen.getByTestId("dashboard-nav-inquiries")).toBeInTheDocument();
  });

  it("renders an Outlet for child routes", () => {
    render(<DashboardLayout />);
    expect(screen.getByTestId("dashboard-outlet-stub")).toBeInTheDocument();
  });
});
