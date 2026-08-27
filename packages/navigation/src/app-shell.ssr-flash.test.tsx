import { assertRouterStubApplied } from "@bc-solutions-coder/testing/router-stub";
import { Link } from "@tanstack/react-router";
import type { MouseEvent, ReactNode } from "react";
import { hydrateRoot, type Root } from "react-dom/client";
import { renderToString } from "react-dom/server";
import { page } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { useNavStore } from "./nav-store";
import { ShellFixture } from "./shell.fixtures";

/**
 * The shell's PRE-HYDRATION paint — and the only proof that it renders on a
 * server at all. Failure here is SILENT in every other spec: a shell that throws
 * under `renderToString` still mounts perfectly in the browser, and a consuming
 * app finds out as an empty document.
 *
 * A spec that mounts the shell cannot see the flash either: a mount reads
 * `useIsDesktop`'s `getSnapshot` — real `matchMedia`, correct on the first
 * render. It lives in the markup the server emits, so `renderToString`'s bytes
 * go into the live document at a real viewport and `checkVisibility()` measures
 * what Chromium would show — omitting the chrome and hiding it both satisfy that.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// `renderToString` has no router context to offer these.
vi.mock("@tanstack/react-router", () => ({
  Link: Object.assign(
    ({ to, children, activeProps: _activeProps, onClick, ...rest }: LinkStubProps) => (
      <a
        href={to}
        data-router-stub="true"
        onClick={(event: MouseEvent<HTMLAnchorElement>) => {
          event.preventDefault();
          onClick?.(event);
        }}
        {...rest}
      >
        {children}
      </a>
    ),
    { wallowRouterStub: true },
  ),
}));

beforeEach(() => {
  assertRouterStubApplied(Link);
});

/** Widths on either side of the `md` (48rem) breakpoint the nav switches on. */
const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

/** Nav chrome specific to ONE viewport, so a lie — a flash — at the other. */
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
 * `innerHTML`, which is a sink for a string.
 */
function paintServerMarkup(): void {
  const html: string = renderToString(<ShellFixture />);
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
  useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
});

afterEach(async () => {
  // `hydrateRoot` schedules its work, and unmounting over an unflushed root
  // makes React abandon hydration — an unhandled error from a passing case.
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
    // Every assertion below reads computed style, which is meaningful only while
    // the browser project's stylesheet is attached. Without it every element
    // reads visible and `hidden md:flex` is reported as a flash it is not.
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

describe("shell pre-hydration paint", () => {
  it("renders to a string at all, rather than throwing on the server", () => {
    // The one failure mode no mounting spec can see. A shell reaching for
    // `window`, `document` or a browser-only ui part dies here.
    expect(renderToString(<ShellFixture />)).toContain('data-testid="dashboard-shell"');
  });

  it("paints no desktop rail on a phone", async () => {
    await page.viewport(...MOBILE_VIEWPORT);

    paintServerMarkup();

    // A server that guesses desktop paints a phone the full rail, yanked away a
    // frame later once hydration reads `matchMedia`.
    expect(paintedChrome()).not.toContain("dashboard-nav");
  });

  it("paints no desktop collapse toggle on a phone", async () => {
    await page.viewport(...MOBILE_VIEWPORT);

    paintServerMarkup();

    expect(paintedChrome()).not.toContain("dashboard-nav-toggle");
  });

  it("paints no mobile menu button on a laptop", async () => {
    await page.viewport(...DESKTOP_VIEWPORT);

    paintServerMarkup();

    // Answering the server snapshot `false` passes both cases above while only
    // relocating the flash.
    expect(paintedChrome()).not.toContain("dashboard-nav-mobile-menu");
  });
});

describe("shell hydration of the server markup", () => {
  it("settles on the mobile treatment on a phone", async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    paintServerMarkup();

    root = hydrateRoot(container!, <ShellFixture />);

    // Not painting the wrong chrome must not mean never painting the right one.
    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).toBeVisible();
    await expect.element(page.getByTestId("dashboard-nav")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-toggle")).not.toBeInTheDocument();
  });

  it("settles on the desktop rail on a laptop", async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
    paintServerMarkup();

    root = hydrateRoot(container!, <ShellFixture />);

    await expect.element(page.getByTestId("dashboard-nav")).toBeVisible();
    await expect.element(page.getByTestId("dashboard-nav-toggle")).toBeVisible();
    await expect.element(page.getByTestId("dashboard-nav-mobile-menu")).not.toBeInTheDocument();
  });
});
