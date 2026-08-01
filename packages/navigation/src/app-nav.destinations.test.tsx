import type { MouseEvent, ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AppShell } from "./app-shell";
import { useNavStore } from "./nav-store";
import { fixtureDestinations, ShellFixture } from "./shell.fixtures";

/**
 * Destination parity: every destination stays reachable, stays named and stays
 * gated in ALL THREE nav modes, and the footer slot rides along with them.
 *
 * Every case runs the same assertions against a mode fixture rather than being
 * written per mode — a destination reachable only in the mode whose spec
 * remembered to check it is the regression this file catches.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// `activeProps` is destructured away rather than spread: a router-only prop on
// an `<a>` earns an unknown-prop warning.
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

/** Every nav row, with the accessible name it must answer to in every mode. */
const navItems: ReadonlyArray<readonly [testid: string, label: string]> = [
  ["dashboard-nav-organizations", "Organizations"],
  ["dashboard-nav-apps", "Apps"],
  ["dashboard-nav-settings", "Settings"],
  ["dashboard-nav-inquiries", "Inquiries"],
  ["dashboard-logout-link", "Sign Out"],
];

describe.each(modes)("AppNav destinations — $name", (mode: NavMode) => {
  beforeEach(async () => {
    useNavStore.setState({
      isNavCollapsed: mode.isNavCollapsed,
      isMobileNavOpen: mode.isMobileNavOpen,
    });
    await page.viewport(...mode.viewport);
  });

  it("reaches every destination by its accessible name", async () => {
    await render(<ShellFixture />);

    // By ROLE + NAME, not by testid: in the collapsed rail the name is the only
    // thing there is.
    await expect.element(page.getByRole("link", { name: "Organizations" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Apps" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Settings" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Inquiries" })).toBeInTheDocument();
    await expect.element(page.getByRole("button", { name: "Sign Out" })).toBeInTheDocument();
  });

  it("names every row from the manifest entry that supplied it", async () => {
    await render(<ShellFixture />);

    // A mode that dropped `aria-label` still passes a text-content assertion
    // wherever the label is also rendered.
    for (const [testid, label] of navItems) {
      await expect.element(page.getByTestId(testid)).toHaveAttribute("aria-label", label);
    }
  });

  it("drops a gated destination without touching the other rows", async () => {
    await render(<ShellFixture isAdmin={false} />);
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();

    await expect.element(page.getByTestId("dashboard-nav-organizations")).not.toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Settings" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Inquiries" })).toBeInTheDocument();
    await expect.element(page.getByRole("button", { name: "Sign Out" })).toBeInTheDocument();
  });
});

describe("AppNav with no gate supplied", () => {
  beforeEach(async () => {
    useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
    await page.viewport(...DESKTOP_VIEWPORT);
  });

  it("renders every destination, including the ones carrying a requirement", async () => {
    // `can` is optional, and its absence must mean "everything is visible" —
    // not "no requirement is satisfied", which would empty the rail for any
    // consumer that has not wired an auth layer yet.
    await render(<AppShell destinations={fixtureDestinations} />);

    await expect.element(page.getByTestId("dashboard-nav-organizations")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();
  });
});
