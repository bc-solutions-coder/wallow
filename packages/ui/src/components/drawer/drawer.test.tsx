import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Drawer } from "./drawer";
import type { DrawerSide } from "./drawer.styles";

/*
 * Drawer behavioural spec (Wallow-m5aq.3.10), shaped after the Wallow-m5aq.3.1
 * Dialog exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `drawerPopupRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into drawer.styles.ts.
 *   4. Stories carry the visual coverage (see drawer.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed).
 * Drawer publishes SEVENTEEN namespace members — Dialog's eleven plus Content,
 * Indent, IndentBackground, Provider, SwipeArea and VirtualKeyboardProvider.
 *
 *   <div data-inactive|data-active style="--drawer-swipe-progress">  <- Drawer.Indent
 *     <button aria-haspopup="dialog" aria-expanded data-base-ui-click-trigger>
 *                                                              <- Drawer.Trigger
 *       …gains data-popup-open and aria-controls="<popup id>" while open
 *     <div data-closed|data-open data-swipe-direction role="presentation" aria-hidden
 *          style="touch-action: pan-y">                        <- Drawer.SwipeArea
 *   <div data-inactive|data-active>                     <- Drawer.IndentBackground
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-base-ui-portal>                                    <- Drawer.Portal
 *     <div role="presentation" aria-hidden style="position:fixed;inset:0">
 *                                       ^- Base UI's OWN pointer blocker, see below
 *     <div data-open role="presentation" aria-hidden
 *          style="--drawer-swipe-progress; --drawer-swipe-strength">
 *                                                                <- Drawer.Backdrop
 *     <div data-open role="presentation">                        <- Drawer.Viewport
 *       <span data-base-ui-focus-guard>            <- Base UI's own focus guards,
 *       <div data-open data-swipe-direction role="dialog" tabindex="-1"
 *            aria-labelledby aria-describedby
 *            style="--drawer-swipe-movement-x; --drawer-swipe-movement-y;
 *                   --drawer-swipe-progress; --drawer-snap-point-offset;
 *                   --drawer-swipe-strength; --drawer-frontmost-height;
 *                   --nested-drawers">                             <- Drawer.Popup
 *         <div data-drawer-content>                              <- Drawer.Content
 *           <h2 id>                                                <- Drawer.Title
 *           <p id>                                           <- Drawer.Description
 *           <button>                                               <- Drawer.Close
 *       <span data-base-ui-focus-guard>                  …one before, one after the
 *                                                        popup, INSIDE the viewport
 *
 * NO Base UI class lands on ANY drawer part (measured: every part's `className`
 * is empty before a recipe is applied), so every class set below can be asserted
 * as an EXACT set with no spread-in extras.
 *
 * What differs from Dialog, and cost a probe round each:
 *
 *   - VIEWPORT IS EFFECTIVELY REQUIRED. `Dialog.Viewport` is optional; omitting
 *     `Drawer.Viewport` makes Base UI log "expected to be rendered within
 *     <Drawer.Viewport>" and turns off swipe handling and touch scroll locking.
 *     Every fixture in this file therefore renders one.
 *   - FOCUS LANDS ON THE POPUP ITSELF, not on the first tabbable element inside
 *     it (Dialog does the opposite). Measured for both a trigger-open and a
 *     `defaultOpen` drawer.
 *   - `defaultOpen` DOES move focus for a drawer (to the popup). A `defaultOpen`
 *     Dialog leaves focus on `<body>`, so the exemplar's "always open through the
 *     trigger" rule does not apply here — both paths are asserted below.
 *   - SwipeArea's `data-swipe-direction` is the OPPOSITE of the Root's: the root
 *     names the direction the drawer is swiped away in, the swipe area the
 *     direction it is swiped open in (`swipeDirection="right"` on the root gives
 *     the popup `"right"` and the swipe area `"left"`).
 *   - Indent and IndentBackground live OUTSIDE the portal, so unlike every
 *     portalled part their recipes are assertable while the drawer is closed.
 *     They carry `data-inactive` closed and `data-active` open.
 *   - `Drawer.VirtualKeyboardProvider` MUST be rendered INSIDE `Drawer.Root`. It
 *     reads the root's store, and outside one it throws "Cannot destructure
 *     property 'store' of useDialogRootContext(...) as it is undefined". Base
 *     UI's published anatomy does not show the part at all.
 *
 * And the Dialog gotchas that carry over, plus one this component added:
 *
 *   - the whole open half is PORTALLED to <body>, so every open-state query goes
 *     through `document.body`, never through `render`'s `container`; nothing
 *     under Drawer.Portal is in the DOM while closed (absent, not hidden);
 *   - A MODAL DRAWER ALWAYS RENDERS ONE MORE ELEMENT THAN YOU WROTE: Base UI puts
 *     an unstyleable `<div role="presentation" aria-hidden
 *     style="position:fixed;inset:0">` first inside the portal to block outside
 *     pointer events, whether or not you render a Backdrop. Its position is an
 *     INLINE style, so it covers the window even here where no Tailwind is
 *     loaded — while the popup, whose `z-50` comes from a recipe class, gets no
 *     stacking at all. `userEvent` from `vitest/browser` drives REAL Playwright
 *     input, which hit-tests the click point, so a click on anything INSIDE an
 *     open popup hits that blocker and times out. Pointer interaction inside the
 *     popup therefore uses a direct `element.click()` here; the realistic pointer
 *     coverage (including press-the-backdrop-to-close) lives in
 *     drawer.stories.tsx, where `userEvent` is `@testing-library/user-event`;
 *   - CLOSING IS ANIMATION-FRAME-DEFERRED (measured: the popup is still in the
 *     DOM synchronously after a Close press). Every absence assertion uses
 *     `await expect.poll(...)`, never a bare synchronous `expect(...).toBeNull()`;
 *   - FOCUS IS DEFERRED TOO, so every `document.activeElement` assertion here is
 *     ALSO polled. Reading it synchronously after `render` looks fine in a green
 *     run and is a latent order dependency: measured, a spec that FAILS makes
 *     vitest capture a failure screenshot, which parks focus on `<body>`, and
 *     the very next spec's synchronous focus read then sees `<body>` instead of
 *     the popup. Polling recovers in both orders. Do not "simplify" these back;
 *   - `data-starting-style` / `data-ending-style` only exist DURING a transition,
 *     so no spec here asserts them on an element. They are pinned as
 *     `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *     recipe class sets instead, which is what the catalog actually owns.
 */

/** Utilities `Drawer.Trigger` must render. Colourless for the same reason
 * `dialogTriggerRecipe` is: a trigger is routinely composed onto a real
 * `Button` via `render`, and a background here would beat the Button's own. */
const TRIGGER_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "rounded-md",
  "text-sm",
  "font-medium",
  "transition-colors",
  "data-[disabled]:opacity-50",
];

/** Utilities `Drawer.SwipeArea` must render whatever edge it sits on. Below the
 * backdrop's `z-50`: once the drawer is open the strip has done its job, and
 * Base UI switches it to `pointer-events: none` inline. */
const SWIPE_AREA_BASE_CLASSES = ["fixed", "z-40"];

/** The per-edge half of the swipe area recipe: a thin strip on that edge. */
const SWIPE_AREA_SIDE_CLASSES: Record<DrawerSide, string[]> = {
  bottom: ["inset-x-0", "bottom-0", "h-6"],
  left: ["inset-y-0", "left-0", "w-6"],
  right: ["inset-y-0", "right-0", "w-6"],
  top: ["inset-x-0", "top-0", "h-6"],
};

/** Utilities `Drawer.Backdrop` must render. Side-agnostic — it covers the
 * window whichever edge the drawer is anchored to. */
const BACKDROP_CLASSES = [
  "fixed",
  "inset-0",
  "z-50",
  "bg-foreground/50",
  "transition-opacity",
  "duration-300",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:opacity-0",
];

/** Utilities `Drawer.Viewport` must render on every side. It is the fixed
 * layer; the popup is `relative` inside it, which is what lets `side` move the
 * anchor without every part relearning its own inset. */
const VIEWPORT_BASE_CLASSES = ["fixed", "inset-0", "z-50", "flex"];

/** The per-edge half of the viewport recipe: which edge it pins the popup to. */
const VIEWPORT_SIDE_CLASSES: Record<DrawerSide, string[]> = {
  bottom: ["items-end", "justify-center"],
  left: ["items-stretch", "justify-start"],
  right: ["items-stretch", "justify-end"],
  top: ["items-start", "justify-center"],
};

/**
 * Utilities `Drawer.Popup` must render on every side. Base UI positions NOTHING
 * itself — the popup's inline style is only the swipe custom properties — so
 * the viewport/popup pair owns the anchoring outright.
 *
 * No padding here: `Drawer.Content` carries it, so a caller can run an image or
 * a sticky header edge-to-edge beside a padded content box.
 */
const POPUP_BASE_CLASSES = [
  "relative",
  "z-50",
  "flex",
  "flex-col",
  "overflow-y-auto",
  "border-border",
  "bg-popover",
  "text-popover-foreground",
  "shadow-lg",
  "outline-none",
  "transition-transform",
  "duration-300",
  "data-[swiping]:duration-0",
];

/**
 * The per-edge half of the popup recipe: shape, which border and radius face
 * inward, and the slide.
 *
 * `translate-x-(--drawer-swipe-movement-x)` / `-y` are the live swipe follow —
 * Base UI writes those custom properties onto this element as the finger moves —
 * and the `data-[starting-style]:` / `data-[ending-style]:` translates are the
 * slide-in and slide-out off that same edge.
 */
const POPUP_SIDE_CLASSES: Record<DrawerSide, string[]> = {
  bottom: [
    "w-full",
    "max-h-[90vh]",
    "rounded-t-lg",
    "border-t",
    "translate-y-(--drawer-swipe-movement-y)",
    "data-[starting-style]:translate-y-full",
    "data-[ending-style]:translate-y-full",
  ],
  left: [
    "h-full",
    "w-80",
    "max-w-[90vw]",
    "rounded-r-lg",
    "border-r",
    "translate-x-(--drawer-swipe-movement-x)",
    "data-[starting-style]:-translate-x-full",
    "data-[ending-style]:-translate-x-full",
  ],
  right: [
    "h-full",
    "w-80",
    "max-w-[90vw]",
    "rounded-l-lg",
    "border-l",
    "translate-x-(--drawer-swipe-movement-x)",
    "data-[starting-style]:translate-x-full",
    "data-[ending-style]:translate-x-full",
  ],
  top: [
    "w-full",
    "max-h-[90vh]",
    "rounded-b-lg",
    "border-b",
    "translate-y-(--drawer-swipe-movement-y)",
    "data-[starting-style]:-translate-y-full",
    "data-[ending-style]:-translate-y-full",
  ],
};

/** Utilities `Drawer.Content` must render — the padded box inside the panel. */
const CONTENT_CLASSES = ["flex", "flex-col", "gap-2", "p-6"];

/** Utilities `Drawer.Title` must render. COLOURLESS, following
 * `popoverTitleRecipe` rather than `dialogTitleRecipe`: the popup already
 * establishes `text-popover-foreground`. */
const TITLE_CLASSES = ["text-xl", "font-semibold"];

/** Utilities `Drawer.Description` must render. */
const DESCRIPTION_CLASSES = ["mt-1", "text-sm", "text-muted-foreground"];

/** Utilities `Drawer.Close` must render. */
const CLOSE_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "rounded-md",
  "text-sm",
  "font-medium",
  "text-muted-foreground",
  "transition-colors",
  "hover:text-foreground",
  "data-[disabled]:opacity-50",
];

/** Utilities `Drawer.Indent` must render: the app UI scales back while any
 * drawer under the provider is open. `data-[active]:` is the whole effect. */
const INDENT_CLASSES = [
  "origin-top",
  "transition-transform",
  "duration-300",
  "data-[active]:scale-[0.97]",
];

/** Utilities `Drawer.IndentBackground` must render: the layer that fills the
 * gap the indent's scale opens up. */
const INDENT_BACKGROUND_CLASSES = [
  "fixed",
  "inset-0",
  "-z-10",
  "bg-foreground",
  "opacity-0",
  "transition-opacity",
  "duration-300",
  "data-[active]:opacity-100",
];

/**
 * Every member `@base-ui/react/drawer` publishes on its namespace, sorted.
 * Seventeen, against Dialog's eleven. `Handle` and `createHandle` are the
 * imperative open/close API for detached triggers; they are re-exported
 * unwrapped rather than dropped, so this catalog's namespace keys still mirror
 * Base UI's 1:1.
 */
const BASE_UI_PART_NAMES = [
  "Backdrop",
  "Close",
  "Content",
  "Description",
  "Handle",
  "Indent",
  "IndentBackground",
  "Popup",
  "Portal",
  "Provider",
  "Root",
  "SwipeArea",
  "Title",
  "Trigger",
  "Viewport",
  "VirtualKeyboardProvider",
  "createHandle",
];

/** Every side, so the variant specs and the fixtures stay in step. */
const SIDES: DrawerSide[] = ["bottom", "left", "right", "top"];

/**
 * Base UI's `swipeDirection` for each `side`. The two are separate contracts —
 * `swipeDirection` names the direction the drawer is swiped AWAY in — so a
 * caller sets both, and this map is what "in step" means.
 */
const SWIPE_DIRECTION_FOR_SIDE: Record<DrawerSide, "down" | "left" | "right" | "up"> = {
  bottom: "down",
  left: "left",
  right: "right",
  top: "up",
};

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** A part's full expected class set for one side, base plus per-edge half. */
function sideClasses(
  base: string[],
  perSide: Record<DrawerSide, string[]>,
  side: DrawerSide,
): string[] {
  return [...base, ...perSide[side]].toSorted();
}

/**
 * The part carrying `data-testid`, searched across the whole document because
 * the open half of a drawer is portalled out of the render container.
 */
function part(testId: string): HTMLElement {
  const element = document.body.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, `no element with data-testid="${testId}"`).not.toBeNull();
  return element as HTMLElement;
}

/** The same lookup for parts that are legitimately absent. */
function maybePart(testId: string): HTMLElement | null {
  return document.body.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
}

/**
 * Every part at once, so one fixture can carry the whole anatomy. The plain
 * button inside gives the focus trap something to cycle through that is not a
 * Base UI part.
 */
function FullDrawer({ side = "right" }: { readonly side?: DrawerSide }): ReactElement {
  return (
    <Drawer.Provider>
      <Drawer.IndentBackground data-testid="w-indent-background" />
      <Drawer.Indent data-testid="w-indent">
        <Drawer.Root swipeDirection={SWIPE_DIRECTION_FOR_SIDE[side]}>
          <Drawer.Trigger data-testid="w-trigger">Open</Drawer.Trigger>
          <Drawer.SwipeArea data-testid="w-swipe-area" side={side} />
          <Drawer.Portal data-testid="w-portal">
            <Drawer.Backdrop data-testid="w-backdrop" />
            <Drawer.Viewport data-testid="w-viewport" side={side}>
              <Drawer.Popup data-testid="w-popup" side={side}>
                <Drawer.Content data-testid="w-content">
                  <Drawer.Title data-testid="w-title">Filters</Drawer.Title>
                  <Drawer.Description data-testid="w-description">
                    Narrow the results down.
                  </Drawer.Description>
                  <button type="button" data-testid="w-apply">
                    Apply
                  </button>
                  <Drawer.Close data-testid="w-close">Cancel</Drawer.Close>
                </Drawer.Content>
              </Drawer.Popup>
            </Drawer.Viewport>
          </Drawer.Portal>
        </Drawer.Root>
      </Drawer.Indent>
    </Drawer.Provider>
  );
}

/** Renders the full fixture and opens it through the trigger. */
async function openDrawer(side: DrawerSide = "right"): Promise<void> {
  await render(<FullDrawer side={side} />);

  await userEvent.click(part("w-trigger"));
  expect(part("w-trigger").getAttribute("aria-expanded")).toBe("true");
}

describe("Drawer", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A key added here
    // that Base UI does not have (or a missing one) fails.
    expect(Object.keys(Drawer).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("renders the trigger as a button that advertises the drawer", async () => {
    await render(<FullDrawer />);

    const trigger = part("w-trigger");
    expect(trigger.tagName).toBe("BUTTON");
    expect(trigger.getAttribute("aria-haspopup")).toBe("dialog");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    expect(trigger.hasAttribute("data-popup-open")).toBe(false);
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the drawer opens.
    await render(<FullDrawer />);

    expect(maybePart("w-portal")).toBeNull();
    expect(maybePart("w-backdrop")).toBeNull();
    expect(maybePart("w-viewport")).toBeNull();
    expect(maybePart("w-popup")).toBeNull();
    expect(maybePart("w-content")).toBeNull();
  });

  it("keeps the trigger, swipe area and indent parts mounted while closed", async () => {
    // These live OUTSIDE the portal, which is why their recipes are assertable
    // in the closed state and the portalled ones are not.
    await render(<FullDrawer />);

    expect(part("w-swipe-area").hasAttribute("data-closed")).toBe(true);
    expect(part("w-indent").hasAttribute("data-inactive")).toBe(true);
    expect(part("w-indent-background").hasAttribute("data-inactive")).toBe(true);
  });

  it("opens the drawer when the trigger is clicked", async () => {
    await openDrawer();

    const popup = part("w-popup");
    expect(popup.getAttribute("role")).toBe("dialog");
    expect(popup.hasAttribute("data-open")).toBe(true);
    expect(part("w-backdrop").hasAttribute("data-open")).toBe(true);
  });

  it("marks the trigger data-popup-open and points aria-controls at the popup", async () => {
    await openDrawer();

    const trigger = part("w-trigger");
    expect(trigger.hasAttribute("data-popup-open")).toBe(true);
    expect(trigger.getAttribute("aria-controls")).toBe(part("w-popup").id);
  });

  it("names the popup with the title and describes it with the description", async () => {
    // Base UI wires these ids itself; the wrappers must not disturb them.
    await openDrawer();

    const popup = part("w-popup");
    expect(popup.getAttribute("aria-labelledby")).toBe(part("w-title").id);
    expect(popup.getAttribute("aria-describedby")).toBe(part("w-description").id);
    expect(part("w-title").tagName).toBe("H2");
    expect(part("w-description").tagName).toBe("P");
    expect(part("w-content").hasAttribute("data-drawer-content")).toBe(true);
  });

  it("marks the indent parts active while the drawer is open", async () => {
    await openDrawer();

    expect(part("w-indent").hasAttribute("data-active")).toBe(true);
    expect(part("w-indent-background").hasAttribute("data-active")).toBe(true);
  });

  it("gives the swipe area the opposite swipe direction to the popup", async () => {
    // The root names the direction the drawer is swiped AWAY in; the swipe area
    // the direction it is swiped OPEN in. Getting these backwards is the easiest
    // way to ship a drawer that cannot be opened by gesture.
    await openDrawer("right");

    expect(part("w-popup").getAttribute("data-swipe-direction")).toBe("right");
    expect(part("w-swipe-area").getAttribute("data-swipe-direction")).toBe("left");
  });

  it("renders the trigger with its recipe", async () => {
    await render(<FullDrawer />);

    expect(classSet(part("w-trigger"))).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("renders the indent and indent background with their recipes", async () => {
    await render(<FullDrawer />);

    expect(classSet(part("w-indent"))).toEqual(INDENT_CLASSES.toSorted());
    expect(classSet(part("w-indent-background"))).toEqual(INDENT_BACKGROUND_CLASSES.toSorted());
  });

  it("renders the backdrop with its recipe", async () => {
    await openDrawer();

    expect(classSet(part("w-backdrop"))).toEqual(BACKDROP_CLASSES.toSorted());
  });

  it("renders the content, title, description and close with their recipes", async () => {
    await openDrawer();

    expect(classSet(part("w-content"))).toEqual(CONTENT_CLASSES.toSorted());
    expect(classSet(part("w-title"))).toEqual(TITLE_CLASSES.toSorted());
    expect(classSet(part("w-description"))).toEqual(DESCRIPTION_CLASSES.toSorted());
    expect(classSet(part("w-close"))).toEqual(CLOSE_CLASSES.toSorted());
  });

  describe("side variant", () => {
    // The one cva variant this component has. Each side has to move three parts
    // together — the strip, the alignment and the panel's own shape and slide —
    // so they are asserted together rather than one part at a time.
    for (const side of SIDES) {
      it(`anchors the swipe area, viewport and popup to the ${side} edge`, async () => {
        await openDrawer(side);

        expect(classSet(part("w-swipe-area"))).toEqual(
          sideClasses(SWIPE_AREA_BASE_CLASSES, SWIPE_AREA_SIDE_CLASSES, side),
        );
        expect(classSet(part("w-viewport"))).toEqual(
          sideClasses(VIEWPORT_BASE_CLASSES, VIEWPORT_SIDE_CLASSES, side),
        );
        expect(classSet(part("w-popup"))).toEqual(
          sideClasses(POPUP_BASE_CLASSES, POPUP_SIDE_CLASSES, side),
        );
      });
    }

    it("defaults to the bottom sheet when no side is given", async () => {
      // The default matches Base UI's own `swipeDirection` default of "down", so
      // a caller who sets neither still gets a coherent bottom sheet.
      await render(
        <Drawer.Root defaultOpen>
          <Drawer.Portal>
            <Drawer.Viewport data-testid="s-viewport">
              <Drawer.Popup data-testid="s-popup">
                <Drawer.Title>Filters</Drawer.Title>
              </Drawer.Popup>
            </Drawer.Viewport>
          </Drawer.Portal>
        </Drawer.Root>,
      );

      expect(classSet(part("s-viewport"))).toEqual(
        sideClasses(VIEWPORT_BASE_CLASSES, VIEWPORT_SIDE_CLASSES, "bottom"),
      );
      expect(classSet(part("s-popup"))).toEqual(
        sideClasses(POPUP_BASE_CLASSES, POPUP_SIDE_CLASSES, "bottom"),
      );
    });
  });

  it("moves focus onto the popup itself when the trigger opens it", async () => {
    // A DIVERGENCE FROM DIALOG, measured: a dialog focuses the first tabbable
    // element inside the popup, a drawer focuses the scroll container itself so
    // the panel can be paged with the keyboard straight away.
    await openDrawer();

    await expect.poll(() => document.activeElement).toBe(part("w-popup"));
  });

  it("moves focus onto the popup for a defaultOpen drawer too", async () => {
    // The other DIVERGENCE FROM DIALOG: a `defaultOpen` dialog leaves focus on
    // <body>, so its specs had to open through the trigger. A drawer does not.
    await render(
      <Drawer.Root defaultOpen>
        <Drawer.Portal>
          <Drawer.Viewport>
            <Drawer.Popup data-testid="f-popup">
              <Drawer.Title>Filters</Drawer.Title>
              <button type="button" data-testid="f-apply">
                Apply
              </button>
            </Drawer.Popup>
          </Drawer.Viewport>
        </Drawer.Portal>
      </Drawer.Root>,
    );

    await expect.poll(() => document.activeElement).toBe(part("f-popup"));
  });

  it("traps Tab inside the popup", async () => {
    await openDrawer();

    // Two tabbable elements inside, so four presses must wrap twice. Focus
    // leaving the popup even once — onto the trigger, the body, or one of Base
    // UI's focus guards — fails.
    for (let index = 0; index < 4; index += 1) {
      await userEvent.keyboard("{Tab}");
      expect(part("w-popup").contains(document.activeElement)).toBe(true);
    }
  });

  it("closes and unmounts the popup on Escape", async () => {
    await openDrawer();

    await userEvent.keyboard("{Escape}");

    // Polled, not read once: the unmount is gated behind an animation frame.
    await expect.poll(() => maybePart("w-popup")).toBeNull();
    expect(maybePart("w-backdrop")).toBeNull();
    expect(part("w-trigger").getAttribute("aria-expanded")).toBe("false");
  });

  it("closes and unmounts the popup when the close part is pressed", async () => {
    await openDrawer();

    // A direct DOM click rather than `userEvent.click`: Base UI's own fixed
    // pointer blocker covers the unstyled popup in this project, so Playwright's
    // actionability check would never resolve. See the header.
    part("w-close").click();

    await expect.poll(() => maybePart("w-popup")).toBeNull();
  });

  it("returns focus to the trigger after closing", async () => {
    await openDrawer();

    await userEvent.keyboard("{Escape}");
    await expect.poll(() => maybePart("w-popup")).toBeNull();

    await expect.poll(() => document.activeElement).toBe(part("w-trigger"));
  });

  it("reports open state to onOpenChange", async () => {
    // The caller's handler has to survive Base UI's own mergeProps.
    const onOpenChange = vi.fn();
    await render(
      <Drawer.Root onOpenChange={onOpenChange}>
        <Drawer.Trigger data-testid="o-trigger">Open</Drawer.Trigger>
        <Drawer.Portal>
          <Drawer.Viewport>
            <Drawer.Popup data-testid="o-popup">
              <Drawer.Title>Filters</Drawer.Title>
            </Drawer.Popup>
          </Drawer.Viewport>
        </Drawer.Portal>
      </Drawer.Root>,
    );

    await userEvent.click(part("o-trigger"));

    expect(onOpenChange).toHaveBeenCalledTimes(1);
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("honours a controlled open prop", async () => {
    const { rerender } = await render(
      <Drawer.Root open={false}>
        <Drawer.Portal>
          <Drawer.Viewport>
            <Drawer.Popup data-testid="c-popup">
              <Drawer.Title>Filters</Drawer.Title>
            </Drawer.Popup>
          </Drawer.Viewport>
        </Drawer.Portal>
      </Drawer.Root>,
    );

    expect(maybePart("c-popup")).toBeNull();

    await rerender(
      <Drawer.Root open>
        <Drawer.Portal>
          <Drawer.Viewport>
            <Drawer.Popup data-testid="c-popup">
              <Drawer.Title>Filters</Drawer.Title>
            </Drawer.Popup>
          </Drawer.Viewport>
        </Drawer.Portal>
      </Drawer.Root>,
    );

    expect(part("c-popup").hasAttribute("data-open")).toBe(true);
  });

  it("renders a drawer wrapped in the virtual keyboard provider", async () => {
    // The part exists in Base UI 1.6.0 and is wired here, but it only takes
    // effect for a software keyboard, so what is assertable is the PLACEMENT:
    // inside the Root. Outside one it throws on the missing root store, and
    // Base UI's published anatomy does not show the part at all.
    await render(
      <Drawer.Root defaultOpen>
        <Drawer.VirtualKeyboardProvider>
          <Drawer.Portal>
            <Drawer.Viewport>
              <Drawer.Popup data-testid="k-popup">
                <Drawer.Content data-testid="k-content">
                  <Drawer.Title>Filters</Drawer.Title>
                  <input data-testid="k-input" />
                </Drawer.Content>
              </Drawer.Popup>
            </Drawer.Viewport>
          </Drawer.Portal>
        </Drawer.VirtualKeyboardProvider>
      </Drawer.Root>,
    );

    expect(part("k-popup").hasAttribute("data-open")).toBe(true);
    expect(classSet(part("k-content"))).toEqual(CONTENT_CLASSES.toSorted());
  });

  it("lets a caller className override popup and backdrop recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    await render(
      <Drawer.Root defaultOpen swipeDirection="right">
        <Drawer.Portal>
          <Drawer.Backdrop data-testid="v-backdrop" className="bg-accent" />
          <Drawer.Viewport side="right">
            <Drawer.Popup data-testid="v-popup" side="right" className="w-96 bg-accent">
              <Drawer.Title>Filters</Drawer.Title>
            </Drawer.Popup>
          </Drawer.Viewport>
        </Drawer.Portal>
      </Drawer.Root>,
    );

    const backdrop = part("v-backdrop");
    expect(backdrop.classList.contains("bg-accent")).toBe(true);
    expect(backdrop.classList.contains("bg-foreground/50")).toBe(false);
    expect(backdrop.classList.contains("fixed")).toBe(true);

    const popup = part("v-popup");
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("w-96")).toBe(true);
    expect(popup.classList.contains("w-80")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);
    expect(popup.classList.contains("data-[ending-style]:translate-x-full")).toBe(true);
  });

  it("carries the popup recipe onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes.
    await render(
      <Drawer.Root defaultOpen>
        <Drawer.Portal>
          <Drawer.Viewport>
            <Drawer.Popup data-testid="r-popup" render={<section />}>
              <Drawer.Title>Filters</Drawer.Title>
            </Drawer.Popup>
          </Drawer.Viewport>
        </Drawer.Portal>
      </Drawer.Root>,
    );

    const popup = part("r-popup");
    expect(popup.tagName).toBe("SECTION");
    expect(classSet(popup)).toEqual(sideClasses(POPUP_BASE_CLASSES, POPUP_SIDE_CLASSES, "bottom"));
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Drawer.Root defaultOpen>
        <Drawer.Portal>
          <Drawer.Viewport>
            <Drawer.Popup data-testid="filter-drawer" aria-label="Filters">
              <Drawer.Title>Filters</Drawer.Title>
            </Drawer.Popup>
          </Drawer.Viewport>
        </Drawer.Portal>
      </Drawer.Root>,
    );

    expect(part("filter-drawer").getAttribute("aria-label")).toBe("Filters");
  });
});
