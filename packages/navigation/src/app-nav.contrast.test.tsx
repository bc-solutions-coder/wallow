import {
  computedColor,
  contrastRatio,
  effectiveBackground,
  isTransparent,
  over,
  type Rgba,
} from "@bc-solutions-coder/testing/contrast";
import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useNavStore } from "./nav-store";
import { ShellFixture } from "./shell.fixtures";

/**
 * MEASURED contrast for the rail, because a class string is blind to what the
 * browser paints. A row is a `NavigationMenu.Link`, so the DOM gets
 * `twMerge(navigationMenuLinkRecipe(), navRowClass)`, and twMerge drops a class
 * only where the caller conflicts AT THE SAME VARIANT — the recipe's hover
 * colour survives an unmodified `text-sidebar-foreground` and can drop a hovered
 * label to 1.27:1 with the whole suite green.
 */

// The active-row treatment reaches the DOM only through `activeProps`.
const routerState = vi.hoisted(() => ({ activePath: "" }));

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  className?: string;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

vi.mock("@tanstack/react-router", () => ({
  Link: ({ to, children, className, activeProps, onClick, ...rest }: LinkStubProps) => (
    <a
      href={to}
      className={[className, to === routerState.activePath ? activeProps?.className : undefined]
        .filter(Boolean)
        .join(" ")}
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

const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

/** WCAG 2.1 AA for body-sized text. A nav label is `text-sm`, so AA applies. */
const AA_TEXT = 4.5;

/**
 * Move the cursor off the nav entirely. Playwright's mouse position PERSISTS
 * across tests in a file, so a row left hovered becomes the next case's "rest".
 */
async function parkMouse(): Promise<void> {
  await userEvent.hover(page.getByTestId("nav-park").element());
}

/** The rendered text colour of `element`, composited over the surface it sits on. */
function paintedText(element: Element): Rgba {
  return over(computedColor(element, "color"), effectiveBackground(element));
}

/**
 * Assert a label is legible on the surface it is painted against, and that the
 * measurement is a real one. `surfaceRoot` is the element that MUST carry a fill:
 * on a theme-less page every ancestor is transparent, `effectiveBackground` walks
 * past them to its white page-canvas fallback, and black text on white is 21:1.
 */
function expectLegible(element: Element, surfaceRoot: Element, label: string): number {
  expect(
    isTransparent(computedColor(surfaceRoot, "background-color")),
    `${label}: the surface itself paints nothing — is the fork theme loaded?`,
  ).toBe(false);

  const surface: Rgba = effectiveBackground(element);
  const text: Rgba = paintedText(element);

  expect(isTransparent(computedColor(element, "color")), `${label}: text is transparent`).toBe(
    false,
  );
  expect(surface.a, `${label}: surface resolved to nothing`).toBe(1);

  const ratio: number = contrastRatio(text, surface);

  expect(ratio, `${label}: measured ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(AA_TEXT);

  return ratio;
}

beforeEach(() => {
  routerState.activePath = "";
  useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
});

/**
 * The shell plus a parking target for {@link parkMouse}, pinned top-right above
 * everything: the drawer's backdrop covers the whole viewport, and Playwright's
 * actionability check retries to timeout on a covered element.
 */
function NavUnderTest() {
  return (
    <>
      <div data-testid="nav-park" className="fixed top-0 right-0 z-50 size-8" />
      <ShellFixture />
    </>
  );
}

describe("rail contrast (measured, not asserted by class name)", () => {
  beforeEach(async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
  });

  it("paints the rail on a real surface rather than transparent", async () => {
    await render(<NavUnderTest />);
    const rail: Element = page.getByTestId("dashboard-nav").element();

    const railFill: Rgba = computedColor(rail, "background-color");

    expect(isTransparent(railFill)).toBe(false);
    // ...and it is the SIDEBAR surface, not a page background inherited from a
    // parent. The two differ in this fork.
    expect(railFill).not.toEqual(effectiveBackground(document.body));
  });

  it("keeps a nav label legible against the rail at rest", async () => {
    await render(<NavUnderTest />);
    await parkMouse();

    expectLegible(
      page.getByTestId("dashboard-nav-apps").element(),
      page.getByTestId("dashboard-nav").element(),
      "idle row",
    );
  });

  it("keeps a nav label legible against the rail WHEN HOVERED", async () => {
    await render(<NavUnderTest />);

    const row: Element = page.getByTestId("dashboard-nav-apps").element();
    const rail: Element = page.getByTestId("dashboard-nav").element();
    const idleRatio: number = expectLegible(row, rail, "row before hover");

    await userEvent.hover(row);
    // The hovered row takes `bg-sidebar-accent`, so its surface must change —
    // otherwise this case is re-measuring the rest state.
    await expect
      .poll(() => effectiveBackground(row).a === 1 && computedColor(row, "background-color").a > 0)
      .toBe(true);

    const hoverRatio: number = expectLegible(row, row, "row while hovered");

    // If the two states measured identically the hover assertion is worthless.
    expect(
      Math.abs(hoverRatio - idleRatio) > 0.01 ||
        effectiveBackground(row).r !== effectiveBackground(document.body).r,
    ).toBe(true);

    await parkMouse();
  });

  it("keeps the ACTIVE row's label legible against its own surface", async () => {
    routerState.activePath = "/dashboard/apps";
    await render(<NavUnderTest />);
    await parkMouse();

    // The active row carries `bg-sidebar-accent` itself, so it IS the surface.
    const activeRow: Element = page.getByTestId("dashboard-nav-apps").element();

    expectLegible(activeRow, activeRow, "active row");
  });
});

describe("drawer contrast", () => {
  beforeEach(async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    useNavStore.setState({ isMobileNavOpen: true });
  });

  it("paints the drawer on a real surface and keeps its labels legible", async () => {
    await render(<NavUnderTest />);
    await parkMouse();

    const drawer: Element = page.getByTestId("dashboard-nav-drawer").element();

    expect(isTransparent(computedColor(drawer, "background-color"))).toBe(false);
    expectLegible(page.getByTestId("dashboard-nav-apps").element(), drawer, "drawer row");
  });
});
