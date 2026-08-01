import { useNavStore } from "@bc-solutions-coder/navigation";
import {
  computedColor,
  contrastRatio,
  effectiveBackground,
  isTransparent,
  over,
  textContrast,
  type Rgba,
} from "@bc-solutions-coder/testing/contrast";
import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardLayout } from "./DashboardLayout";

/**
 * What Sign Out's failure banner PAINTS on the rail.
 *
 * It stays here rather than in `@bc-solutions-coder/navigation` because reaching
 * the banner requires rejecting the SDK's `logout()`, and the package carries no
 * SDK edge. `ErrorBanner` defaults to the page palette while the rail is dark in
 * both modes, so measuring is the point — a class string cannot see what
 * `twMerge` produced. The mode is stamped on `document.documentElement`, since
 * `@theme` declares each token on `:root` alone and a wrapper element measures
 * light mode twice.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

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

// Rejecting `logout()` is the only way to render the in-rail ErrorBanner.
vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, logout: logoutMock };
});

const DESKTOP_VIEWPORT = [1280, 800] as const;

/** WCAG 2.1 AA for body-sized text — the banner's message is `text-sm`. */
const AA_TEXT = 4.5;

/** WCAG 2.1 AA for a non-text boundary: the banner's edge against the rail. */
const AA_NON_TEXT = 3;

const MODES: readonly string[] = ["light", "dark"];

/**
 * The shell plus a parking target for the mouse, pinned above everything because
 * Playwright retries to timeout on a covered element.
 */
function ShellUnderTest() {
  return (
    <div>
      <div data-testid="nav-park" className="fixed top-0 right-0 z-50 size-8" />
      <div data-testid="probe-sidebar" className="bg-sidebar size-4" />
      <DashboardLayout />
    </div>
  );
}

/** Select a mode the only way the token layer honours: on the document element. */
function applyMode(mode: string): void {
  document.documentElement.classList.toggle("dark", mode === "dark");
  document.documentElement.classList.toggle("light", mode === "light");
}

/**
 * Fail loudly when the theme is absent: a theme-less page paints every token
 * `rgba(0, 0, 0, 0)`, and a ratio against nothing is meaningless.
 */
function expectThemed(color: Rgba, label: string): Rgba {
  expect(isTransparent(color), `${label}: paints nothing — is the fork theme loaded?`).toBe(false);
  return color;
}

/** Render the shell and drive Sign Out into its failure state. */
async function failSignOut(): Promise<void> {
  logoutMock.mockRejectedValue(new Error("sign out refused"));

  await render(<ShellUnderTest />);
  await userEvent.click(page.getByTestId("dashboard-logout-link").element());
  await expect.element(page.getByTestId("dashboard-logout-error")).toBeInTheDocument();
  await userEvent.hover(page.getByTestId("nav-park").element());
}

beforeEach(async () => {
  logoutMock.mockReset();
  useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
  await page.viewport(...DESKTOP_VIEWPORT);
});

// The document is shared, so a stamped mode has to be handed back.
afterEach(() => {
  document.documentElement.classList.remove("dark", "light");
});

describe("the mode axis itself", () => {
  it("repaints the rail token, so a dark-mode case is not light mode measured twice", async () => {
    // The tripwire for both `describe.each(MODES)` cases below.
    applyMode("light");
    await render(<ShellUnderTest />);
    const light: Rgba = expectThemed(
      computedColor(page.getByTestId("probe-sidebar").element(), "background-color"),
      "bg-sidebar in light mode",
    );

    applyMode("dark");
    const dark: Rgba = expectThemed(
      computedColor(page.getByTestId("probe-sidebar").element(), "background-color"),
      "bg-sidebar in dark mode",
    );

    expect(dark, "bg-sidebar paints identically in both modes").not.toEqual(light);
  });
});

describe.each(MODES)("the sign-out error banner on the rail — %s mode", (mode: string) => {
  beforeEach(() => {
    applyMode(mode);
  });

  it("keeps its message legible against the rail", async () => {
    await failSignOut();

    const banner: Element = page.getByTestId("dashboard-logout-error").element();
    const message: Element | null = banner.querySelector("p");

    expect(message, "the banner rendered no message paragraph").not.toBeNull();
    expectThemed(
      computedColor(page.getByTestId("dashboard-nav").element(), "background-color"),
      "the rail",
    );

    // `textContrast` composites both sides, so a `/10` fill is measured as what
    // the eye sees rather than as its authored colour.
    const ratio: number = textContrast(message as Element);

    expect(ratio, `the banner's message measured ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(
      AA_TEXT,
    );
  });

  it("is visually delineated from the rail it sits on", async () => {
    await failSignOut();

    const banner: Element = page.getByTestId("dashboard-logout-error").element();
    const rail: Rgba = expectThemed(
      computedColor(page.getByTestId("dashboard-nav").element(), "background-color"),
      "the rail",
    );

    // A banner may announce itself with a fill OR an edge, but with one of them.
    const fill: number = contrastRatio(effectiveBackground(banner), rail);
    const edge: number = contrastRatio(over(computedColor(banner, "border-top-color"), rail), rail);

    expect(
      Math.max(fill, edge),
      `the banner's fill measured ${fill.toFixed(2)}:1 and its edge ${edge.toFixed(2)}:1`,
    ).toBeGreaterThanOrEqual(AA_NON_TEXT);
  });
});
