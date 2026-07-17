/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardNav } from "./DashboardNav";

// No global `expect` (vitest `globals` is off); register jest-dom matchers.
expect.extend(matchers);

const logoutMock = vi.hoisted(() => vi.fn());

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

// Spy on the SDK's `logout` (a real browser nav to `/bff/logout` in prod).
vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, logout: logoutMock };
});

/**
 * DashboardNav admin-gate + logout spec (Wallow-ffpq.3.6) — restores the Blazor
 * `DashboardLayout.razor` behaviour the Wallow-8w1h.8.1 port deliberately
 * dropped: the Organizations nav item is gated to admins, and a "Sign Out" link
 * (`dashboard-logout-link`) calls the BFF logout. The gate is driven by the
 * `isAdmin` prop the shell derives from the current user's roles.
 */
describe("DashboardNav admin gate", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows the Organizations link for an admin", () => {
    render(<DashboardNav isAdmin />);
    expect(screen.getByTestId("dashboard-nav-organizations")).toBeInTheDocument();
  });

  it("hides the Organizations link for a non-admin", () => {
    render(<DashboardNav isAdmin={false} />);
    expect(screen.queryByTestId("dashboard-nav-organizations")).not.toBeInTheDocument();
  });

  it("keeps the non-gated links visible for a non-admin", () => {
    render(<DashboardNav isAdmin={false} />);
    expect(screen.getByTestId("dashboard-nav-apps")).toBeInTheDocument();
    expect(screen.getByTestId("dashboard-nav-settings")).toBeInTheDocument();
    expect(screen.getByTestId("dashboard-nav-inquiries")).toBeInTheDocument();
  });
});

describe("DashboardNav logout", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders a logout link", () => {
    render(<DashboardNav />);
    expect(screen.getByTestId("dashboard-logout-link")).toBeInTheDocument();
  });

  it("calls the BFF logout when the logout link is activated", async () => {
    const user = userEvent.setup();
    render(<DashboardNav />);

    await user.click(screen.getByTestId("dashboard-logout-link"));

    expect(logoutMock).toHaveBeenCalled();
  });
});
