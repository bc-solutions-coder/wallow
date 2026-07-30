import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardNav } from "./DashboardNav";
import { useUiStore } from "../stores/ui-store";

/**
 * DashboardNav destination-parity spec (Wallow-0byr.3) — the acceptance line the
 * per-mode specs each cover from one side only: EVERY nav destination stays
 * reachable, stays named, stays admin-gated, and still signs out, in ALL THREE
 * modes.
 *
 * `DashboardNav.modes.test.tsx` proves the two desktop modes render what they
 * should; `DashboardLayout.mobile.test.tsx` proves the drawer opens and closes;
 * `DashboardNav.gate.test.tsx` proves the admin gate — but only on the desktop
 * rail. The rail and the drawer share `NavDestinationList`/`NavLogout` today, so
 * the gate and the logout are structurally the same in both; these cases are
 * what keeps that true if the two renderings are ever pulled apart.
 *
 * Every case runs the SAME assertions against a mode fixture rather than being
 * written per mode: a destination that is only reachable in the mode whose spec
 * remembered to check it is exactly the regression this file exists to catch.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub TanStack `Link` to a plain anchor (as in `DashboardNav.test.tsx`), with the
// anchor's default navigation suppressed so a click cannot move the test iframe.
// `activeProps` is destructured away rather than spread: no case here asserts the
// active highlight (that is `DashboardNav.modes.test.tsx`), and passing a
// router-only prop through to an `<a>` only earns a React unknown-prop warning.
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
}));

const logoutMock = vi.hoisted(() => vi.fn());

// Spy on the SDK's `logout` (a real browser nav to `/bff/logout` in prod).
vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, logout: logoutMock };
});

/** Widths on either side of the `md` (48rem) breakpoint the nav switches on. */
const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

/** A nav mode: the viewport and store state that together produce one rendering. */
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

/** Every nav item, with the accessible name it must answer to in every mode. */
const navItems: ReadonlyArray<readonly [testid: string, label: string]> = [
  ["dashboard-nav-organizations", "Organizations"],
  ["dashboard-nav-apps", "Apps"],
  ["dashboard-nav-settings", "Settings"],
  ["dashboard-nav-inquiries", "Inquiries"],
  ["dashboard-logout-link", "Sign Out"],
];

describe.each(modes)("DashboardNav destinations — $name", (mode: NavMode) => {
  beforeEach(async () => {
    vi.clearAllMocks();
    useUiStore.setState({
      isNavCollapsed: mode.isNavCollapsed,
      isMobileNavOpen: mode.isMobileNavOpen,
    });
    await page.viewport(...mode.viewport);
  });

  it("reaches every destination by its accessible name", async () => {
    await render(<DashboardNav />);

    // By ROLE + NAME, not by testid: this is the assertion a screen-reader user's
    // experience actually rests on, and in the collapsed rail the name is the
    // only thing there is.
    await expect.element(page.getByRole("link", { name: "Organizations" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Apps" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Settings" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Inquiries" })).toBeInTheDocument();
    await expect.element(page.getByRole("button", { name: "Sign Out" })).toBeInTheDocument();
  });

  it("names every item from the shared icon-label map", async () => {
    await render(<DashboardNav />);

    // The same label in every mode is what makes "same item" true across a
    // resize; a mode that dropped `aria-label` would still pass a text-content
    // assertion wherever the label is also rendered.
    for (const [testid, label] of navItems) {
      await expect.element(page.getByTestId(testid)).toHaveAttribute("aria-label", label);
    }
  });

  it("hides Organizations from a non-admin without touching the other items", async () => {
    await render(<DashboardNav isAdmin={false} />);
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-organizations")).not.toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Settings" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Inquiries" })).toBeInTheDocument();
    await expect.element(page.getByRole("button", { name: "Sign Out" })).toBeInTheDocument();
  });

  it("calls the BFF logout from the Sign Out control", async () => {
    await render(<DashboardNav />);

    await userEvent.click(page.getByTestId("dashboard-logout-link"));

    expect(logoutMock).toHaveBeenCalled();
  });
});
