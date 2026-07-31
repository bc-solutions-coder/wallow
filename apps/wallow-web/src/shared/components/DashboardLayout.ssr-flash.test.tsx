import type { MouseEvent, ReactNode } from "react";
import { hydrateRoot, type Root } from "react-dom/client";
import { renderToString } from "react-dom/server";
import { page } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardLayout } from "./DashboardLayout";
import { useUiStore } from "../stores/ui-store";

/**
 * The dashboard shell's PRE-HYDRATION paint.
 *
 * A spec that mounts the shell cannot see this: a mount reads `useIsDesktop`'s
 * `getSnapshot` — real `matchMedia`, correct on the first render. The flash
 * lives in the markup the server emits, so `renderToString`'s bytes go into the
 * live document at a real viewport and `checkVisibility()` measures what
 * Chromium would show. The assertions are mechanism-agnostic: omitting the
 * chrome and hiding it behind `hidden md:flex` both satisfy them.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub the router primitives the shell composes; `renderToString` has no router
// context to offer either.
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

/** Widths on either side of the `md` (48rem) breakpoint the nav switches on. */
const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

/**
 * The nav chrome that is specific to ONE viewport — the persistent desktop rail
 * and its collapse toggle, and the mobile menu button that summons the drawer.
 * Each is a lie at the other width, so each is a candidate flash.
 */
const VIEWPORT_SPECIFIC_CHROME = [
  "dashboard-nav",
  "dashboard-nav-toggle",
  "dashboard-nav-mobile-menu",
] as const;

let container: HTMLDivElement | null = null;
let root: Root | null = null;

/**
 * Put the server's own HTML into the live document — the paint a visitor gets
 * before any JavaScript has run. Parsed and adopted rather than assigned through
 * `innerHTML`: the same DOM by a route that is not a sink for a string.
 */
function paintServerMarkup(): void {
  const html: string = renderToString(<DashboardLayout />);
  const parsed: Document = new DOMParser().parseFromString(html, "text/html");
  const element: HTMLDivElement = document.createElement("div");
  element.append(...parsed.body.childNodes);
  document.body.append(element);
  container = element;
}

/** Which of the viewport-specific controls Chromium would actually show now. */
function paintedChrome(): string[] {
  return VIEWPORT_SPECIFIC_CHROME.filter((testid: string): boolean => {
    const node: Element | null = document.querySelector(`[data-testid="${testid}"]`);
    return node !== null && node.checkVisibility();
  });
}

beforeEach(() => {
  useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
});

afterEach(async () => {
  // `hydrateRoot` schedules its work rather than doing it inline, and unmounting
  // over an unflushed root makes React abandon hydration — an unhandled error
  // that fails the run from the teardown of a passing case.
  await new Promise((resolve) => {
    setTimeout(resolve, 0);
  });
  root?.unmount();
  root = null;
  container?.remove();
  container = null;
});

describe("the visibility instrument these cases measure with", () => {
  it("reads Tailwind's own utilities, so a hidden element reads as hidden", () => {
    // Every assertion below distinguishes "painted" from "not painted" through
    // computed style, which is meaningful only while the browser project's
    // stylesheet is attached. Without it every element reads visible, and chrome
    // hidden by `hidden md:flex` is reported as a flash it is not.
    const probe: HTMLDivElement = document.createElement("div");
    probe.className = "hidden";
    document.body.append(probe);
    try {
      expect(probe.checkVisibility()).toBe(false);
    } finally {
      probe.remove();
    }
  });
});

describe("dashboard shell pre-hydration paint", () => {
  it("paints no desktop rail on a phone", async () => {
    await page.viewport(...MOBILE_VIEWPORT);

    paintServerMarkup();

    // A server that guesses desktop makes a phone's first paint the full sidebar
    // rail, yanked away a frame later once hydration reads matchMedia.
    expect(paintedChrome()).not.toContain("dashboard-nav");
  });

  it("paints no desktop collapse toggle on a phone", async () => {
    await page.viewport(...MOBILE_VIEWPORT);

    paintServerMarkup();

    // Below `md` there is no rail to collapse, so the button means nothing.
    expect(paintedChrome()).not.toContain("dashboard-nav-toggle");
  });

  it("paints no mobile menu button on a laptop", async () => {
    await page.viewport(...DESKTOP_VIEWPORT);

    paintServerMarkup();

    // Answering the server snapshot `false` rather than `true` passes both cases
    // above while only relocating the flash.
    expect(paintedChrome()).not.toContain("dashboard-nav-mobile-menu");
  });
});

describe("dashboard shell hydration of the server markup", () => {
  it("settles on the mobile treatment on a phone", async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    paintServerMarkup();

    root = hydrateRoot(container!, <DashboardLayout />);

    // Not painting the wrong chrome must not mean never painting the right one.
    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).toBeVisible();
    await expect.element(page.getByTestId("dashboard-nav")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-toggle")).not.toBeInTheDocument();
  });

  it("settles on the desktop rail on a laptop", async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
    paintServerMarkup();

    root = hydrateRoot(container!, <DashboardLayout />);

    await expect.element(page.getByTestId("dashboard-nav")).toBeVisible();
    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeVisible();
    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).not.toBeInTheDocument();
  });
});
