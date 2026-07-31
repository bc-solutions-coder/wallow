import type { ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardLayout } from "./DashboardLayout";

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
 * The authenticated shell's composition: a `dashboard-welcome` root, the nav's
 * links, and a router `<Outlet/>` for the child routes.
 */
describe("DashboardLayout", () => {
  // Vitest browser mode defaults to a 414x896 viewport — a phone, below the `md`
  // breakpoint at which the shell renders the nav rail rather than a mobile
  // drawer. These cases assert the desktop composition.
  beforeEach(async () => {
    await page.viewport(1280, 800);
  });

  it("renders a shell root carrying data-testid=dashboard-welcome", async () => {
    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-welcome")).toBeInTheDocument();
  });

  it("renders the dashboard nav (all four vertical links)", async () => {
    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-nav-organizations")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-settings")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-inquiries")).toBeInTheDocument();
  });

  it("renders an Outlet for child routes", async () => {
    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-outlet-stub")).toBeInTheDocument();
  });
});
