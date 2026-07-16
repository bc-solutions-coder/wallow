/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

import { DashboardNav } from "./DashboardNav";

// No global `expect` (vitest `globals` is off); register jest-dom matchers.
expect.extend(matchers);

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
 * DashboardNav spec (Wallow-8w1h.8.1). The dashboard shell's nav must link to
 * ALL FOUR verticals unconditionally (a deliberate divergence from the Blazor
 * oracle's admin-gated Organizations link), each carrying a
 * `data-testid="dashboard-nav-<feature>"` testid pointing at its route.
 */
describe("DashboardNav", () => {
  const links: ReadonlyArray<readonly [testid: string, href: string]> = [
    ["dashboard-nav-organizations", "/dashboard/organizations"],
    ["dashboard-nav-apps", "/dashboard/apps"],
    ["dashboard-nav-settings", "/dashboard/settings"],
    ["dashboard-nav-inquiries", "/dashboard/inquiries"],
  ];

  for (const [testid, href] of links) {
    it(`renders a nav link ${testid} -> ${href}`, () => {
      render(<DashboardNav />);
      const link = screen.getByTestId(testid);
      expect(link).toBeInTheDocument();
      expect(link).toHaveAttribute("href", href);
    });
  }
});
