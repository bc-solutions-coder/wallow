import { useNavStore } from "@bc-solutions-coder/navigation";
import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardLayout } from "./DashboardLayout";

/**
 * What this app contributes to the shell: WHICH destinations exist, WHO may see
 * each one, and what the nav footer does. The rail, the drawer and the controls
 * belong to `@bc-solutions-coder/navigation` and are covered there.
 *
 * Every case runs in all three nav modes, because a gate or a footer that works
 * only in the expanded rail is the regression: the shell renders the manifest
 * three times.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// The nav's `Link`s need live router context; `Outlet` needs a route match.
vi.mock("@tanstack/react-router", () => ({
  Link: ({ to, children, activeProps: _activeProps, onClick, ...rest }: LinkStubProps) => (
    <a
      href={to}
      onClick={(event: MouseEvent<HTMLAnchorElement>) => {
        event.preventDefault();
        onClick?.(event);
      }}
      {...rest}
    >
      {children}
    </a>
  ),
  Outlet: () => <div data-testid="dashboard-outlet-stub" />,
}));

const logoutMock = vi.hoisted(() => vi.fn());

vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, logout: logoutMock };
});

const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

interface NavMode {
  readonly name: string;
  readonly viewport: readonly [number, number];
  readonly isNavCollapsed: boolean;
  readonly isMobileNavOpen: boolean;
}

const modes: readonly NavMode[] = [
  {
    name: "expanded rail",
    viewport: DESKTOP_VIEWPORT,
    isNavCollapsed: false,
    isMobileNavOpen: false,
  },
  {
    name: "collapsed icon rail",
    viewport: DESKTOP_VIEWPORT,
    isNavCollapsed: true,
    isMobileNavOpen: false,
  },
  {
    name: "mobile drawer",
    viewport: MOBILE_VIEWPORT,
    isNavCollapsed: false,
    isMobileNavOpen: true,
  },
];

/** Every destination the manifest declares, with the name and route it answers to. */
const destinations: ReadonlyArray<readonly [testid: string, label: string, href: string]> = [
  ["dashboard-nav-organizations", "Organizations", "/dashboard/organizations"],
  ["dashboard-nav-settings", "Settings", "/dashboard/settings"],
  ["dashboard-nav-inquiries", "Inquiries", "/dashboard/inquiries"],
];

describe.each(modes)("DashboardLayout — $name", (mode: NavMode) => {
  beforeEach(async () => {
    logoutMock.mockReset();
    useNavStore.setState({
      isNavCollapsed: mode.isNavCollapsed,
      isMobileNavOpen: mode.isMobileNavOpen,
    });
    await page.viewport(...mode.viewport);
  });

  it("renders the sidebar at its intended width", async () => {
    await render(<DashboardLayout />);
    const nav = page.getByTestId(mode.isMobileNavOpen ? "dashboard-nav-drawer" : "dashboard-nav");
    await expect
      .poll(() => nav.element().getBoundingClientRect().width)
      .toBe(mode.isNavCollapsed ? 64 : 256);
  });

  it("keeps bottom controls inside the sidebar with space around the separator", async () => {
    await render(<DashboardLayout />);
    const nav = page
      .getByTestId(mode.isMobileNavOpen ? "dashboard-nav-drawer" : "dashboard-nav")
      .element()
      .getBoundingClientRect();
    const theme = page.getByTestId("theme-toggle").element().getBoundingClientRect();
    const logout = page.getByRole("button", { name: "Sign Out" }).element();
    const footer = logout.parentElement?.parentElement;
    if (footer === null || footer === undefined) {
      throw new Error("Missing footer");
    }
    const separator = footer.getBoundingClientRect();
    const action = logout.getBoundingClientRect();
    expect(theme.left).toBeGreaterThanOrEqual(nav.left);
    expect(theme.right).toBeLessThanOrEqual(nav.right);
    expect(action.right).toBeLessThanOrEqual(nav.right);
    expect(separator.left).toBe(nav.left);
    expect(separator.width).toBe(nav.width);
    expect(separator.top - theme.bottom).toBe(16);
    expect(action.top - separator.top).toBe(17);
    if (mode.isNavCollapsed) {
      expect(theme.left + theme.width / 2).toBe(nav.left + nav.width / 2);
      expect(action.left + action.width / 2).toBe(nav.left + nav.width / 2);
    }
  });

  it("reaches every destination by its accessible name", async () => {
    await render(<DashboardLayout />);

    // By ROLE + NAME, not by testid: in the collapsed rail the name is the only
    // thing there is. `exact: true` because role-name matching is substring by
    // default, and "My Organizations" would otherwise also satisfy "Organizations".
    for (const [, label] of destinations) {
      await expect
        .element(page.getByRole("link", { name: label, exact: true }))
        .toBeInTheDocument();
    }
    await expect.element(page.getByRole("button", { name: "Sign Out" })).toBeInTheDocument();
  });

  it("points each destination at the route the manifest names", async () => {
    await render(<DashboardLayout />);

    for (const [testid, , href] of destinations) {
      await expect.element(page.getByTestId(testid)).toHaveAttribute("href", href);
    }
  });

  it("hides Organizations from a non-admin without touching the other destinations", async () => {
    await render(<DashboardLayout isAdmin={false} />);
    await expect.element(page.getByTestId("dashboard-nav-my-organizations")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-organizations")).not.toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Settings" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Inquiries" })).toBeInTheDocument();
    await expect.element(page.getByRole("button", { name: "Sign Out" })).toBeInTheDocument();
  });

  it("shows Organizations to an admin", async () => {
    await render(<DashboardLayout isAdmin />);

    await expect.element(page.getByTestId("dashboard-nav-organizations")).toBeInTheDocument();
  });

  it("shows Organizations when the layout is rendered without a gate", async () => {
    // The isolated render every spec here does by default. An unspecified
    // `isAdmin` is not a denial.
    await render(<DashboardLayout />);

    await expect.element(page.getByTestId("dashboard-nav-organizations")).toBeInTheDocument();
  });

  it("calls the BFF logout from the Sign Out control", async () => {
    await render(<DashboardLayout />);

    await userEvent.click(page.getByTestId("dashboard-logout-link"));

    expect(logoutMock).toHaveBeenCalled();
  });
});

describe("DashboardLayout composition", () => {
  beforeEach(async () => {
    useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
    await page.viewport(...DESKTOP_VIEWPORT);
  });

  it("renders a shell root carrying data-testid=dashboard-shell", async () => {
    // The shell's default `testIdPrefix`, which the E2E contract rests on.
    await render(<DashboardLayout />);

    await expect.element(page.getByTestId("dashboard-shell")).toBeInTheDocument();
  });

  it("routes the matched child into the shell's main column", async () => {
    await render(<DashboardLayout />);
    await expect.element(page.getByTestId("dashboard-outlet-stub")).toBeInTheDocument();

    const outlet: Element = page.getByTestId("dashboard-outlet-stub").element();

    expect(
      outlet.closest("main"),
      "the routed content must sit in the main landmark",
    ).not.toBeNull();
    expect(
      outlet.closest('[data-testid="dashboard-nav"]'),
      "the routed content must not be inside the rail",
    ).toBeNull();
  });
});

describe("Dashboard sidebar resizing", () => {
  it("expands again after collapsing without moving over the content", async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
    useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
    await render(<DashboardLayout />);
    const nav = page.getByTestId("dashboard-nav");
    const toggle = page.getByTestId("dashboard-nav-toggle");
    await expect.poll(() => nav.element().getBoundingClientRect().width).toBe(256);
    await toggle.click();
    await expect.poll(() => nav.element().getBoundingClientRect().width).toBe(64);
    await toggle.click();
    await expect.poll(() => nav.element().getBoundingClientRect().width).toBe(256);
    expect(page.getByRole("main").element().getBoundingClientRect().left).toBe(
      nav.element().getBoundingClientRect().right,
    );
  });
});
