import {
  computedColor,
  contrastRatio,
  effectiveBackground,
  isTransparent,
  textContrast,
  type Rgba,
} from "@bc-solutions-coder/testing/contrast";
import { buttonRecipe } from "@bc-solutions-coder/ui/button";
import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { useNavStore } from "./nav-store";
import { defaultNavControlLabels } from "./nav-icons";
import { ShellFixture } from "./shell.fixtures";

/**
 * The shell's two nav controls are the catalog's outline button.
 *
 * The load-bearing assertion is the negative one: what reaches the DOM is
 * `twMerge(recipe, className)`, so a hand-rolled leftover survives beside the
 * recipe's classes. And it MEASURES, because a class list cannot see paint.
 *
 * The mode is stamped on `document.documentElement` because `@theme` declares
 * each token on `:root` alone — a `<div className="dark">` measures light twice.
 */

// `activeProps` is pulled out and dropped: letting it reach the anchor would put
// an object on a DOM attribute. The default navigation is suppressed so no stray
// click moves the test iframe.
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
    <a
      href={to}
      onClick={(event: MouseEvent<HTMLAnchorElement>) => {
        event.preventDefault();
      }}
      {...rest}
    >
      {children}
    </a>
  ),
}));

const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

const MODES: readonly string[] = ["light", "dark"];

/** WCAG 2.1 AA for a non-text graphic; both controls are glyph-only. */
const AA_NON_TEXT = 3;

/** Bounds the transition poll below. Tailwind's own duration is 150ms. */
const TRANSITION_TIMEOUT = 2000;

/** `buttonRecipe`'s `outline` arm, copied — and CHECKED against it below. */
const OUTLINE_SURFACE =
  "border border-border bg-transparent text-foreground hover:bg-accent hover:text-accent-foreground";

/** Every `buttonRecipe` axis, so the vocabulary below is the whole recipe. */
const VARIANTS = ["primary", "secondary", "destructive", "outline", "ghost", "link"] as const;
const SIZES = ["sm", "md", "lg", "icon"] as const;
const WIDTHS = ["auto", "full"] as const;
const SHAPES = ["rounded", "pill"] as const;
const SURFACES = ["page", "sidebar"] as const;

/**
 * Every class `buttonRecipe` can emit, across every combination of its five
 * axes. The union rather than one call's output: which `size`, `width` and
 * `shape` these controls want is not the subject.
 */
const RECIPE_VOCABULARY: ReadonlySet<string> = new Set(
  VARIANTS.flatMap((variant) =>
    SIZES.flatMap((size) =>
      WIDTHS.flatMap((width) =>
        SHAPES.flatMap((shape) =>
          SURFACES.map((surface) => buttonRecipe({ variant, size, width, shape, surface })),
        ),
      ),
    ),
  )
    .join(" ")
    .split(/\s+/u)
    .filter((cls: string): boolean => cls !== ""),
);

/**
 * A utility that paints a BUTTON. Positioning, margin and display are outside
 * the set — layout the recipe does not own and the caller still has to pass.
 */
const BUTTON_SURFACE_UTILITY =
  /^(?:[^\s:]+:)*(?:bg-|text-|border|rounded|p-|px-|py-|font-|ring|outline|shadow|size-|w-|h-)/u;

/** Every button-surface utility in `classes` that `buttonRecipe` cannot emit. */
function handRolledSurfaceUtilities(classes: readonly string[]): readonly string[] {
  return classes.filter(
    (cls: string): boolean => BUTTON_SURFACE_UTILITY.test(cls) && !RECIPE_VOCABULARY.has(cls),
  );
}

/**
 * Wait for `testId` to commit, then return it — `render()` returns before React
 * has painted, so every case gates on the control before measuring it.
 */
async function waitForTestId(testId: string): Promise<HTMLElement> {
  await expect.element(page.getByTestId(testId)).toBeInTheDocument();
  const elements = page.getByTestId(testId).elements();
  expect(elements, `expected exactly one [data-testid="${testId}"]`).toHaveLength(1);
  return elements[0] as HTMLElement;
}

/** The shell plus one probe per token needing a REFERENCE colour. */
function ShellUnderTest() {
  return (
    <div>
      <div data-testid="probe-background" className="bg-background size-4" />
      <div data-testid="probe-accent" className="bg-accent size-4" />
      <ShellFixture />
    </div>
  );
}

/** Select a mode the only way the token layer honours: on the document element. */
function applyMode(mode: string): void {
  document.documentElement.classList.toggle("dark", mode === "dark");
  document.documentElement.classList.toggle("light", mode === "light");
}

function probe(testId: string): Rgba {
  return computedColor(page.getByTestId(testId).element(), "background-color");
}

/**
 * Fail loudly when the theme is absent: a theme-less page paints every token
 * `rgba(0, 0, 0, 0)`, and an inequality between two nothings passes.
 */
function expectThemed(color: Rgba, label: string): Rgba {
  expect(isTransparent(color), `${label}: paints nothing — is the fork theme loaded?`).toBe(false);
  return color;
}

/**
 * `buttonRecipe`'s base carries `motion-safe:transition-colors`, so a colour
 * read the moment the cursor lands is the RESTING colour caught mid-transition
 * — indistinguishable from "the hover class never applied".
 */
async function surfaceAfterTransition(
  element: Element,
  reference: Rgba,
  expectation: "matches" | "differs",
): Promise<Rgba> {
  const target: string = JSON.stringify(reference);
  const read = (): string => JSON.stringify(effectiveBackground(element));
  const polled = expect.poll(read, { timeout: TRANSITION_TIMEOUT });

  await (expectation === "matches" ? polled.toBe(target) : polled.not.toBe(target));

  return effectiveBackground(element);
}

beforeEach(() => {
  useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
});

// The document is shared, so a stamped mode has to be handed back.
afterEach(() => {
  document.documentElement.classList.remove("dark", "light");
});

describe("the mode axis itself", () => {
  it("repaints the tokens, so a dark-mode case is not light mode measured twice", async () => {
    // The tripwire for every `describe.each(MODES)` below.
    await page.viewport(...DESKTOP_VIEWPORT);
    applyMode("light");
    await render(<ShellUnderTest />);
    const lightBackground: Rgba = expectThemed(probe("probe-background"), "bg-background, light");
    const lightAccent: Rgba = expectThemed(probe("probe-accent"), "bg-accent, light");

    applyMode("dark");
    const darkBackground: Rgba = expectThemed(probe("probe-background"), "bg-background, dark");
    const darkAccent: Rgba = expectThemed(probe("probe-accent"), "bg-accent, dark");

    expect(darkBackground, "bg-background paints identically in both modes").not.toEqual(
      lightBackground,
    );
    expect(darkAccent, "bg-accent paints identically in both modes").not.toEqual(lightAccent);
  });
});

describe("the outline surface this file pins", () => {
  it("is the string buttonRecipe's own outline arm emits", () => {
    const outline: ReadonlySet<string> = new Set(
      buttonRecipe({ variant: "outline" }).split(/\s+/u),
    );
    const ghost: ReadonlySet<string> = new Set(buttonRecipe({ variant: "ghost" }).split(/\s+/u));
    const pinned: readonly string[] = OUTLINE_SURFACE.split(/\s+/u);

    expect(
      pinned.filter((cls: string): boolean => !outline.has(cls)),
      "OUTLINE_SURFACE has drifted from buttonRecipe — copy the recipe, do not invent a third answer",
    ).toEqual([]);

    // `text-foreground hover:bg-accent hover:text-accent-foreground` is also
    // `ghost`'s string, so a constant trimmed to those pins the wrong button.
    expect(
      pinned.filter((cls: string): boolean => !ghost.has(cls)),
      "OUTLINE_SURFACE names nothing that distinguishes outline from ghost",
    ).not.toEqual([]);
  });

  it("recognises a hand-rolled utility and ignores the layout classes the caller keeps", () => {
    // A detector that matched nothing passes every negative case below.
    expect(
      handRolledSurfaceUtilities([
        "relative",
        "z-20",
        "mb-4",
        "md:hidden",
        "px-3",
        "border-border",
        "rounded-full",
        "hover:bg-muted",
        "text-lime-500",
      ]),
    ).toEqual(["hover:bg-muted", "text-lime-500"]);
  });
});

/** The two controls, each with the width at which it is the one that exists. */
const CONTROLS: readonly (readonly [string, readonly [number, number]])[] = [
  ["dashboard-nav-toggle", DESKTOP_VIEWPORT],
  ["dashboard-nav-mobile-menu", MOBILE_VIEWPORT],
];

describe.each(CONTROLS)("%s as a catalog Button", (testId: string, viewport) => {
  beforeEach(async () => {
    await page.viewport(...viewport);
    applyMode("light");
  });

  it("draws the border that separates the outline arm from ghost", async () => {
    await render(<ShellUnderTest />);
    const control: Element = await waitForTestId(testId);

    // The border is the whole difference: `outline` and `ghost` agree on every
    // other surface class. Measured rather than read off `classList`, because
    // `cn()` merges a caller's `className` over the recipe — `border-border` can
    // be present while the control paints no border at all.
    expect(Number.parseFloat(getComputedStyle(control).borderTopWidth)).toBeGreaterThan(0);
    expectThemed(computedColor(control, "border-top-color"), "the control's border colour");
  });

  it("carries no button-surface utility the recipe cannot emit", async () => {
    await render(<ShellUnderTest />);
    const control: Element = await waitForTestId(testId);

    expect(
      handRolledSurfaceUtilities([...control.classList]),
      `${testId} still paints itself with a string the catalog does not own`,
    ).toEqual([]);
  });

  it("keeps its element, type and a11y wiring", async () => {
    await render(<ShellUnderTest />);
    const control: Element = await waitForTestId(testId);
    // The two controls report DIFFERENT things through `aria-expanded`: the rail
    // toggle whether the rail is expanded, the mobile button whether the drawer is.
    const expected: Record<string, string> =
      testId === "dashboard-nav-toggle"
        ? {
            "aria-controls": "dashboard-nav",
            "aria-expanded": "true",
            "aria-label": defaultNavControlLabels.navToggle,
          }
        : {
            "aria-controls": "dashboard-nav-drawer",
            "aria-expanded": "false",
            "aria-label": defaultNavControlLabels.mobileMenu,
          };

    // Base UI can SUBSTITUTE the element through `render` and need not keep a
    // native `type` — the one thing stopping a nested control submitting a form.
    expect(control.tagName.toLowerCase(), "the control is still a native button").toBe("button");
    expect(control.getAttribute("type")).toBe("button");
    expect(control.getAttribute("aria-controls")).toBe(expected["aria-controls"]);
    expect(control.getAttribute("aria-expanded")).toBe(expected["aria-expanded"]);
    expect(control.getAttribute("aria-label")).toBe(expected["aria-label"]);
  });
});

describe.each(MODES)("the desktop control's glyph — %s mode", (mode: string) => {
  beforeEach(async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
    applyMode(mode);
  });

  it("stays legible at rest against the page it sits on", async () => {
    await render(<ShellUnderTest />);
    const control: Element = await waitForTestId("dashboard-nav-toggle");
    // "At rest" is a claim about the pointer, so state it at the read: the
    // position persists across spec FILES, and the browser re-evaluates `:hover`
    // when new content mounts under it.
    await userEvent.unhover(control);

    const background: Rgba = expectThemed(probe("probe-background"), "bg-background");

    // An outline button draws no surface of its own, so it sits on the page;
    // anything else means something opaque got underneath the glyph.
    const surface: Rgba = await surfaceAfterTransition(control, background, "matches");

    expectThemed(surface, "the control's rendered surface");
    expectThemed(computedColor(control, "color"), "the control's glyph colour");

    const ratio: number = textContrast(control);

    expect(ratio, `the resting glyph measured ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(
      AA_NON_TEXT,
    );
  });

  it("stays legible under the cursor, on whatever the recipe fills with", async () => {
    await render(<ShellUnderTest />);
    const control: Element = await waitForTestId("dashboard-nav-toggle");
    const background: Rgba = expectThemed(probe("probe-background"), "bg-background");

    await userEvent.hover(control);

    // The poll can only end by the surface differing from the page, so a recipe
    // whose hover fill never lands fails here.
    const hovered: Rgba = expectThemed(
      await surfaceAfterTransition(control, background, "differs"),
      "the control's hovered surface",
    );
    const ratio: number = contrastRatio(computedColor(control, "color"), hovered);

    expect(ratio, `the hovered glyph measured ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(
      AA_NON_TEXT,
    );
  });
});
