import type { ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it, vi } from "vitest";

import { DashboardNav } from "./DashboardNav";

// `DashboardNav` renders TanStack `Link`s. Outside a `RouterProvider` those
// hooks throw, so stub `Link` to a plain anchor that passes through `to`
// (as `href`) and any `data-testid`. This lets the nav render in isolation
// while still asserting each link's target + testid.
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
 * DashboardNav spec (Wallow-8w1h.8.1). The dashboard shell's nav links to the
 * verticals, each carrying a `data-testid="dashboard-nav-<feature>"` testid
 * pointing at its route. The Organizations link is admin-gated as of
 * Wallow-ffpq.3.6 (restoring the Blazor oracle), so it is covered by
 * `DashboardNav.gate.test.tsx` rather than the unconditional loop here.
 */
describe("DashboardNav", () => {
  const links: ReadonlyArray<readonly [testid: string, href: string]> = [
    ["dashboard-nav-apps", "/dashboard/apps"],
    ["dashboard-nav-settings", "/dashboard/settings"],
    ["dashboard-nav-inquiries", "/dashboard/inquiries"],
  ];

  for (const [testid, href] of links) {
    it(`renders a nav link ${testid} -> ${href}`, async () => {
      await render(<DashboardNav />);
      const link = page.getByTestId(testid);
      await expect.element(link).toBeInTheDocument();
      await expect.element(link).toHaveAttribute("href", href);
    });
  }
});
