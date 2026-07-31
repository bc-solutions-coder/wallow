import {
  computedColor,
  contrastRatio,
  effectiveBackground,
  isTransparent,
  textContrast,
  type Rgba,
} from "@bc-solutions-coder/testing/contrast";
import { buttonRecipe } from "@bc-solutions-coder/ui/button";
import type { ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { expectClasses, waitForTestId } from "@shared/testing/style-contract";
import { DashboardLayout } from "./DashboardLayout";
import { navIconLabels } from "./nav-icons";
import { useUiStore } from "../stores/ui-store";

/**
 * The two dashboard nav controls ARE the catalog's outline button
 * (Wallow-lrlm.6.5).
 *
 * `DashboardLayout` hand-rolls `navControlClass`, and the catalog already ships
 * exactly that control — `buttonRecipe`'s `outline` arm is a border with no
 * surface. The hand-rolled copy has already drifted from it once
 * (`hover:bg-muted` where the recipe says `hover:bg-accent
 * hover:text-accent-foreground`), which is the whole argument for deleting the
 * string rather than correcting it.
 *
 * WHAT THIS FILE ASSERTS THAT THE SIBLINGS DO NOT. `DashboardLayout.restyle.test.tsx`
 * pins that the recipe's classes are PRESENT and that the rail's palette is
 * absent. Present-ness alone is satisfied by a control that carries the recipe's
 * classes AND a hand-rolled leftover beside them, so the load-bearing assertion
 * here is the NEGATIVE one: every button-surface utility on the merged class
 * attribute must be one `buttonRecipe` can emit. That is "hand-rolls nothing"
 * stated about what actually reached the DOM, which is `twMerge(recipe,
 * className)` and not the string the shell passes in.
 *
 * AND IT MEASURES, because a class list cannot see paint. Swapping
 * `hover:bg-muted` for the recipe's `hover:bg-accent hover:text-accent-foreground`
 * repaints both of the control's states, and Wallow-lrlm.5.4 shipped a 1.27:1
 * hover defect past a green class-string suite. Since Wallow-8ytl this project
 * renders the real fork theme, so the colours can be read back — the technique,
 * the probes and the mouse-parking discipline follow
 * `DashboardNav.sidebar-surface.test.tsx`.
 *
 * BOTH MODES, ON THE DOCUMENT ELEMENT — and it has to be the document element.
 * The theme emits `:root` / `.light` / `.dark` blocks setting the RAW variables,
 * while `@theme` declares each token `--color-x: var(--x, …)` on `:root` alone. A
 * `var()` inside a custom property is substituted at computed-value time ON THE
 * DECLARING ELEMENT, so a `<div className="dark">` wrapper rebinds the raw
 * variable for descendants and the already-computed token never notices: the
 * utilities keep painting light. A wrapper therefore measures light mode TWICE
 * and calls one of them dark. The first case below is the tripwire for exactly
 * that, and `afterEach` hands the mode back so it cannot leak.
 *
 * VIEWPORT: only one control exists at a given width (Wallow-0byr.2), so each
 * case sets the width whose control it means.
 */

// Stub the router primitives the shell composes, as every sibling `DashboardLayout`
// spec does. `activeProps` is pulled out and dropped: no case here reads an active
// row, and letting it reach the anchor would put an object on a DOM attribute.
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

/** The theme modes the fork ships, as the class that selects each one's palette. */
const MODES: readonly string[] = ["light", "dark"];

/**
 * WCAG 2.1 AA for a non-text graphic. Both controls are glyph-only — the label is
 * on `aria-label`, so what a sighted reader has to make out is the icon, and the
 * icon inherits the button's `color`.
 */
const AA_NON_TEXT = 3;

/**
 * How long a colour transition is given to finish. Tailwind's default duration is
 * 150ms; the budget is generous because it bounds a poll, and a poll that ends
 * early is a flake while one that ends late costs nothing on a passing run.
 */
const TRANSITION_TIMEOUT = 2000;

/**
 * `buttonRecipe`'s `outline` arm, copied from
 * `packages/ui/src/components/button/button.styles.ts`.
 *
 * Copied, and then CHECKED against the recipe below rather than trusted — a spec
 * that asserts against a string this app maintains would let the catalog move
 * without anything going red, which is the drift this bead exists to end.
 */
const OUTLINE_SURFACE =
  "border border-border bg-transparent text-foreground hover:bg-accent hover:text-accent-foreground";

/** Every `buttonRecipe` axis, so the vocabulary below is the whole recipe. */
const VARIANTS = ["primary", "secondary", "destructive", "outline", "ghost", "link"] as const;
const SIZES = ["sm", "md", "lg", "icon"] as const;
const WIDTHS = ["auto", "full"] as const;
const SHAPES = ["rounded", "pill"] as const;
const SURFACES = ["page", "sidebar"] as const;

/**
 * Every class `buttonRecipe` can emit, across every combination of its five axes.
 *
 * The union rather than one call's output on purpose: which `size`, `width` and
 * `shape` these controls want is the green phase's decision (the bead only fixes
 * `variant`), and a spec that pinned one combination would be asserting a choice
 * it was not given. What it CAN say is that nothing outside the recipe's own
 * vocabulary painted the button.
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
 * A utility that paints a BUTTON: its surface, text, border, radius, padding,
 * type scale, weight, ring or box.
 *
 * Positioning, margin and display utilities are deliberately outside this set.
 * The bead says so explicitly — `relative z-20 mb-4` and the pre-hydration
 * `hidden md:inline-block` / `md:hidden` are layout the recipe does not own and
 * the caller still has to pass — so flagging them would fail a correct
 * implementation, and forbidding a future `shrink-0` is not this bead's business.
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
 * The shell, a parking target for the mouse, and one probe per token this file
 * needs a REFERENCE colour for.
 *
 * The parking target is pinned top-right above everything: Playwright's
 * actionability check retries to timeout on an element something else covers, and
 * the mouse position persists across cases in a browser-mode file, so a case that
 * hovers has to hand the cursor back.
 */
function LayoutUnderTest() {
  return (
    <div>
      <div data-testid="control-park" className="fixed top-0 right-0 z-50 size-8" />
      <div data-testid="probe-background" className="bg-background size-4" />
      <div data-testid="probe-accent" className="bg-accent size-4" />
      <DashboardLayout />
    </div>
  );
}

/** Select a mode the only way the token layer honours: on the document element. */
function applyMode(mode: string): void {
  document.documentElement.classList.toggle("dark", mode === "dark");
  document.documentElement.classList.toggle("light", mode === "light");
}

/** The colour a probe element paints, i.e. the value of the token it names. */
function probe(testId: string): Rgba {
  return computedColor(page.getByTestId(testId).element(), "background-color");
}

/**
 * Fail loudly when the theme is absent. Without it every measurement here is
 * vacuous in the same direction: a theme-less page paints every token
 * `rgba(0, 0, 0, 0)`, and a contrast ratio between two nothings is 1:1 — which
 * fails — or, worse, an inequality between two nothings, which passes.
 */
function expectThemed(color: Rgba, label: string): Rgba {
  expect(isTransparent(color), `${label}: paints nothing — is the fork theme loaded?`).toBe(false);
  return color;
}

/** Park the cursor away from the shell so no case measures a stale hover. */
async function parkMouse(): Promise<void> {
  await userEvent.hover(page.getByTestId("control-park").element());
}

/**
 * Wait for `element`'s rendered surface to settle, then hand it back.
 *
 * `buttonRecipe`'s BASE string carries `motion-safe:transition-colors`, so a
 * catalog button's fill arrives over Tailwind's default 150ms rather than
 * instantly. A colour read the moment the cursor lands is therefore the RESTING
 * colour caught mid-transition — which reads exactly like "the hover class never
 * applied", i.e. it fails a correct implementation for the wrong reason. The
 * hand-rolled control this bead retires had no transition, so nothing in this
 * directory needed to know that before.
 *
 * `settled` is the colour the caller expects to see or to stop seeing; the poll
 * is what makes the wait bounded rather than a sleep.
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
  useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
});

// The document is shared by every case in the file, so a stamped mode is state
// that has to be handed back — otherwise the last `dark` case silently repaints
// whatever runs next.
afterEach(() => {
  document.documentElement.classList.remove("dark", "light");
});

describe("the mode axis itself", () => {
  it("repaints the tokens, so a dark-mode case is not light mode measured twice", async () => {
    // The tripwire for every `describe.each(MODES)` below. Selecting the mode with
    // a wrapper `<div className={mode}>` measures the LIGHT palette under both
    // labels and passes — dark coverage that does not exist. This epic has found
    // that pattern twice already; if `applyMode` ever stops reaching the token
    // layer, THIS fails first and says so.
    await page.viewport(...DESKTOP_VIEWPORT);
    applyMode("light");
    await render(<LayoutUnderTest />);
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

    // The discriminating half. `text-foreground hover:bg-accent
    // hover:text-accent-foreground` alone is also `ghost`'s string, so a constant
    // trimmed down to those would pin "a quiet button" rather than "the outline
    // button" and this file would stop being able to tell them apart.
    expect(
      pinned.filter((cls: string): boolean => !ghost.has(cls)),
      "OUTLINE_SURFACE names nothing that distinguishes outline from ghost",
    ).not.toEqual([]);
  });

  it("recognises a hand-rolled utility and ignores the layout classes the caller keeps", () => {
    // Demonstrated, not trusted: a detector that matched nothing would make every
    // negative case in this file pass on any markup at all. `relative z-20 mb-4`
    // and `md:hidden` are the layout the caller legitimately passes; `px-3` and
    // `border-border` are the recipe's own; only the last two are offenders.
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

  it("carries the recipe's outline arm on its merged class attribute", async () => {
    await render(<LayoutUnderTest />);
    const control: Element = await waitForTestId(testId);

    expectClasses(control, OUTLINE_SURFACE);
  });

  it("carries no button-surface utility the recipe cannot emit", async () => {
    await render(<LayoutUnderTest />);
    const control: Element = await waitForTestId(testId);

    // The load-bearing negative. What reaches the DOM is `twMerge(buttonRecipe(),
    // className)`, so a leftover the shell still hand-rolls survives beside the
    // recipe's classes whenever it does not collide with one of them — and
    // `expectClasses` above would stay green through it.
    expect(
      handRolledSurfaceUtilities([...control.classList]),
      `${testId} still paints itself with a string the catalog does not own`,
    ).toEqual([]);
  });

  it("keeps its element, type and a11y wiring", async () => {
    await render(<LayoutUnderTest />);
    const control: Element = await waitForTestId(testId);
    // The two controls report DIFFERENT things through the same attribute, which
    // is why the expectation is per-control rather than shared: the rail toggle
    // reports whether the rail is expanded (`beforeEach` leaves it uncollapsed, so
    // `true`), and the mobile button reports whether the drawer is open (`false`).
    const expected: Record<string, string> =
      testId === "dashboard-nav-toggle"
        ? {
            "aria-controls": "dashboard-nav",
            "aria-expanded": "true",
            "aria-label": navIconLabels.navToggle,
          }
        : {
            "aria-controls": "dashboard-nav-drawer",
            "aria-expanded": "false",
            "aria-label": navIconLabels.mobileMenu,
          };

    // A catalog `Button` is Base UI's, which can SUBSTITUTE its element through
    // `render` and does not have to keep a native `type`. Both are contract here:
    // `type="button"` is what stops the control submitting a form it is ever
    // nested in, and `aria-controls` is what names the region it operates.
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

  /*
   * One control is measured rather than both: the bead's binding constraint is
   * that the two stay ONE declaration's worth of styling, and the class cases
   * above assert both carry the identical recipe arm. Measuring the second would
   * re-measure the same tokens at a narrower viewport.
   */

  it("stays legible at rest against the page it sits on", async () => {
    await render(<LayoutUnderTest />);
    const control: Element = await waitForTestId("dashboard-nav-toggle");
    await parkMouse();

    const background: Rgba = expectThemed(probe("probe-background"), "bg-background");

    // An outline button draws no surface of its own, so it must be sitting on the
    // page. If this ever fails it means something opaque got underneath the glyph
    // — which is precisely what the sidebar palette would do here, and why
    // Wallow-lrlm.5.4's J1 ruling keeps it off these two controls.
    //
    // Polled rather than read once: the cursor position persists across cases in
    // this file, so parking it can START a transition back to rest.
    const surface: Rgba = await surfaceAfterTransition(control, background, "matches");

    expectThemed(surface, "the control's rendered surface");
    expectThemed(computedColor(control, "color"), "the control's glyph colour");

    const ratio: number = textContrast(control);

    expect(ratio, `the resting glyph measured ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(
      AA_NON_TEXT,
    );
  });

  it("stays legible under the cursor, on whatever the recipe fills with", async () => {
    await render(<LayoutUnderTest />);
    const control: Element = await waitForTestId("dashboard-nav-toggle");
    const background: Rgba = expectThemed(probe("probe-background"), "bg-background");

    await userEvent.hover(control);

    // A hover that changes nothing is not a hover state, and this poll is what
    // says so: it can only end by the surface differing from the page, so a
    // recipe whose hover fill never lands fails here rather than sliding through.
    // It is also the half a class assertion cannot make — `hover:bg-accent` on an
    // element whose className also said `hover:bg-muted` yields whichever twMerge
    // kept, and only the paint says which.
    const hovered: Rgba = expectThemed(
      await surfaceAfterTransition(control, background, "differs"),
      "the control's hovered surface",
    );
    const ratio: number = contrastRatio(computedColor(control, "color"), hovered);

    expect(ratio, `the hovered glyph measured ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(
      AA_NON_TEXT,
    );

    // Last thing in the file to touch the mouse, and it hands it back anyway: the
    // cursor position persists across cases in a browser-mode file.
    await parkMouse();
  });
});
