import type { ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  expectClasses,
  expectTokenColorsOnly,
  waitForTestId,
} from "@shared/testing/style-contract";
import { DashboardLayout } from "./DashboardLayout";
import { useUiStore } from "../stores/ui-store";

/**
 * Which token paints the dashboard shell's three chrome nodes: the rail toggle,
 * the mobile menu button, and the drawer's scrim.
 *
 * The two buttons live in `<main>` on `bg-background`, not on the rail, so they
 * take the page's own named tokens and the negative half of each case pins that
 * they did not take `sidebar-*` instead — the rail's palette on a light page
 * turns a control into a black box.
 *
 * Only one control exists at a given width; each case sets the width it means.
 */

// Stub the router primitives the shell composes. `activeProps` is dropped
// rather than spread — no case here reads an active row, and letting it reach
// the anchor would put an object on a DOM attribute.
vi.mock("@tanstack/react-router", () => ({
  Link: ({
    to,
    children,
    activeProps: _activeProps,
    ...rest
  }: {
    to: string;
    children?: ReactNode;
    activeProps?: { className?: string };
  } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
  Outlet: () => <div data-testid="dashboard-outlet-stub" />,
}));

const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

/**
 * The outline treatment both nav controls share: `buttonRecipe`'s own `outline`
 * arm verbatim, not a near-equivalent such as `hover:bg-muted`. That the recipe
 * still says this is checked by the sibling catalog-control spec.
 */
const CONTROL_SURFACE =
  "border border-border bg-transparent text-foreground hover:bg-accent hover:text-accent-foreground";

/** Hand-mixed tints a control on the page surface must not carry. */
const RETIRED_CONTROL: readonly string[] = ["border-foreground/20", "hover:bg-foreground/10"];

/** `element` carries neither the retired tints nor the rail's palette. */
function expectPageSurfaceControl(element: Element): void {
  const survivors = RETIRED_CONTROL.filter((cls) => element.classList.contains(cls));
  expect(survivors, "a control on the page surface must not mix its own colour").toEqual([]);

  const inverted = [...element.classList].filter((cls) => cls.includes("sidebar"));
  expect(inverted, "a control in the main column must not take the rail's palette").toEqual([]);
}

beforeEach(() => {
  useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
});

describe("DashboardLayout nav controls", () => {
  it("gives the desktop rail toggle the page's own outline tokens", async () => {
    await page.viewport(...DESKTOP_VIEWPORT);

    await render(<DashboardLayout />);
    const toggle = await waitForTestId("dashboard-nav-toggle");

    expectClasses(toggle, CONTROL_SURFACE);
    expectPageSurfaceControl(toggle);
    expectTokenColorsOnly(toggle);
  });

  it("gives the mobile menu button the identical treatment", async () => {
    await page.viewport(...MOBILE_VIEWPORT);

    await render(<DashboardLayout />);
    const menu = await waitForTestId("dashboard-nav-mobile-menu");

    // The two controls act on different axes but are the same CONTROL, and the
    // shell declares their class string twice, so they can drift apart.
    expectClasses(menu, CONTROL_SURFACE);
    expectPageSurfaceControl(menu);
  });
});

describe("DashboardLayout drawer scrim", () => {
  beforeEach(async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    useUiStore.setState({ isMobileNavOpen: true });
  });

  it("keeps the scrim translucent rather than painting it a sidebar surface", async () => {
    await render(<DashboardLayout />);
    const backdrop = await waitForTestId("dashboard-nav-backdrop");

    // A scrim dims the page it covers; an opaque token hides it. The catalog's
    // own backdrops are `bg-foreground/50` for the same reason.
    expectClasses(backdrop, "bg-foreground/40");

    const inverted = [...backdrop.classList].filter((cls) => cls.includes("sidebar"));
    expect(inverted, "the scrim is not an inverted surface").toEqual([]);
  });
});
