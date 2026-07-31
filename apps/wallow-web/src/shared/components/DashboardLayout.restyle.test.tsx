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
 * Token spec for the dashboard shell's CONTROLS (Wallow-lrlm.5.4).
 *
 * `DashboardNav.restyle.test.tsx` covers the inverted panel. This file covers
 * the three nodes that sit OUTSIDE it and were styled relative to it anyway —
 * the desktop rail toggle, the mobile menu button, and the drawer's scrim — each
 * of which reached for a `foreground/NN` tint.
 *
 * THE CONTROLS ARE NOT ON THE SIDEBAR, AND MUST NOT TAKE ITS PALETTE. Both
 * buttons live in `<main>`, on `bg-background`, alongside the page content;
 * `DashboardLayout`'s own doc comment says why ("the controls must stay in the
 * main column: a toggle inside the collapsed rail would be the thing it is meant
 * to reveal"). `border-foreground/20` and `hover:bg-foreground/10` there are a
 * hand-mixed OUTLINE BUTTON on the page surface, not an inversion — so they take
 * the page's own named tokens, `border-border` and `bg-muted`, and the negative
 * half of each case pins that they did not take `sidebar-*` instead. Painting a
 * control on a light page with the rail's palette would turn it into a black box.
 *
 * WHY `bg-muted` RATHER THAN A NEW HOVER TOKEN: Wallow-lrlm.3.5 already made
 * this exact substitution when `ListRow`'s `hover:bg-background/50` became
 * `hover:bg-muted`, and Wallow-lrlm.5.3 followed it for the MFA confirm panel.
 * A recessed surface is `bg-muted` in this theme; a third answer would be drift.
 *
 * THE SCRIM IS DELIBERATELY LEFT TRANSLUCENT — see `dashboard-chrome-tokens.test.ts`
 * for the reasoning and the carve-out it is pinned by.
 *
 * VIEWPORT: only one control exists at a given width (Wallow-0byr.2), so each
 * case sets the width whose control it means.
 */

// Stub the router primitives the shell composes, as the sibling `DashboardLayout`
// specs do. `activeProps` is pulled out and dropped rather than spread: the real
// `Link` merges it in only for the active route, and letting it reach the anchor
// would put an object on a DOM attribute. No case here reads an active row.
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
 * The outline-button treatment both nav controls share, in page tokens — which is
 * `buttonRecipe`'s own `outline` arm, verbatim
 * (`packages/ui/src/components/button/button.styles.ts`), since Wallow-lrlm.6.5
 * stopped the shell hand-rolling it.
 *
 * The change from what this constant used to say is `hover:bg-muted` becoming
 * `hover:bg-accent hover:text-accent-foreground` (plus the arm's explicit
 * `bg-transparent`). That divergence is the reason the bead exists: Wallow-lrlm.5.4
 * moved these controls onto page tokens and picked `bg-muted` for the recessed
 * hover, which is within visual noise of the catalog's answer but is not the
 * catalog's answer. Adopting the variant deletes the fork rather than restating it,
 * so this constant follows the recipe — a third answer invented here would be the
 * same drift under a new name. The recipe is asserted to still SAY this in
 * `DashboardLayout.catalog-control.test.tsx`, which also pins that nothing outside
 * the recipe's vocabulary paints these controls.
 */
const CONTROL_SURFACE =
  "border border-border bg-transparent text-foreground hover:bg-accent hover:text-accent-foreground";

/** The hand-mixed tints both controls used to carry. */
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
    // shell declares their class string twice. They must not drift apart while
    // one of them is migrated.
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

    // A scrim dims the page it covers; an opaque token would hide it. The
    // catalog's own backdrops (`drawerBackdropRecipe`, `alertDialogBackdropRecipe`)
    // are `bg-foreground/50` for exactly this reason, so the shell keeps the
    // idiom rather than inventing a token that cannot be see-through.
    expectClasses(backdrop, "bg-foreground/40");

    const inverted = [...backdrop.classList].filter((cls) => cls.includes("sidebar"));
    expect(inverted, "the scrim is not an inverted surface").toEqual([]);
  });
});
