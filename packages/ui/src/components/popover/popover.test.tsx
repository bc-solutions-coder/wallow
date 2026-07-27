import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Popover } from "./popover";

/*
 * Popover behavioural spec (Wallow-m5aq.3.3), shaped after the Wallow-m5aq.3.1
 * Dialog exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `popoverPopupRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into popover.styles.ts.
 *   4. Stories carry the visual coverage (see popover.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <button aria-haspopup="dialog" aria-expanded data-base-ui-click-trigger>  <- Popover.Trigger
 *     …gains data-popup-open, data-pressed and aria-controls="<popup id>" while open
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-base-ui-portal>                                    <- Popover.Portal
 *     <div data-open role="presentation" data-base-ui-inert style="user-select:none">
 *                                                                <- Popover.Backdrop
 *     <div data-open data-side data-align role="presentation"
 *          style="position:absolute; left:…; top:…; --positioner-width:…">
 *                                                                <- Popover.Positioner
 *       <span data-base-ui-focus-guard data-type="inside">
 *       <div data-open data-side data-align role="dialog" tabindex="-1"
 *            aria-labelledby aria-describedby style="--popup-width:auto">
 *                                                                <- Popover.Popup
 *         <div data-open data-side data-align aria-hidden="true"
 *              style="position:absolute; left:…">                <- Popover.Arrow
 *         <div>                                                  <- Popover.Viewport
 *           <div data-current="true">                              (Base UI's own)
 *             <h2 id>                                            <- Popover.Title
 *             <p id>                                             <- Popover.Description
 *             <button>                                           <- Popover.Close
 *       <span data-base-ui-focus-guard data-type="inside">
 *
 * Six consequences worth knowing before editing this file. The first four are
 * the Dialog exemplar's; the last two are where a popover DIVERGES from it:
 *
 *   - the whole open half is PORTALLED to <body>, so every open-state query goes
 *     through `document.body`, never through `render`'s `container`;
 *   - nothing under Popover.Portal exists in the DOM at all while the popover is
 *     closed — these are not hidden elements, they are absent ones;
 *   - closing may be ANIMATION-FRAME-DEFERRED: Base UI gates the unmount behind
 *     `useOpenChangeComplete` -> `useAnimationsFinished`. Both close paths
 *     happened to be gone synchronously when measured here, but that is not a
 *     contract — every absence assertion below uses `await expect.poll(...)`
 *     uniformly, exactly as the exemplar rules;
 *   - `data-starting-style` / `data-ending-style` only exist DURING a transition,
 *     so no spec here asserts them on an element. They are pinned as
 *     `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *     recipe class sets instead, which is what the catalog actually owns;
 *   - A POPOVER IS NON-MODAL BY DEFAULT, so Base UI renders NO pointer blocker
 *     (measured: the fixed `<div role="presentation" aria-hidden>` appears only
 *     under `<Popover.Root modal>`, and is pinned by its own spec below). That is
 *     the exemplar's gotcha (2) inverted: `userEvent.click` from `vitest/browser`
 *     drives real Playwright input and lands cleanly on parts INSIDE an open
 *     popup here — measured at 24ms, against the ~15s timeout a modal dialog
 *     gives. The close-part spec therefore uses a genuine `userEvent.click`, not
 *     the direct `element.click()` dialog.test.tsx needs. The BACKDROP is still
 *     unclickable in this project, but for an unrelated reason: it carries no
 *     inline geometry, so without Tailwind its `fixed inset-0` recipe class does
 *     nothing and Playwright never finds a stable box. Outside-press coverage
 *     therefore still lives in popover.stories.tsx;
 *   - BASE UI OWNS THE PLACEMENT. The positioner carries inline
 *     `position/left/top` plus the `--positioner-*` custom properties, and the
 *     arrow carries an inline `position:absolute` with a side-dependent
 *     `left` or `top`. So — unlike `dialogPopupRecipe`, which owns its own fixed
 *     centring — no recipe here may contribute layout: the positioner gets
 *     stacking and focus only, and the arrow gets size and paint only. A spec
 *     below pins that division.
 */

/**
 * Utilities `Popover.Trigger` must render. Deliberately colourless for the same
 * reason as the dialog's: the trigger is routinely composed onto a real `Button`
 * via `render`, and a background here would be merged away by tailwind-merge and
 * silently beat the Button's own.
 */
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

/**
 * Utilities `Popover.Backdrop` must render. Lighter than the dialog's `/50`
 * scrim, because a popover is non-modal chrome rather than a page-blocking one,
 * and `z-40` rather than `z-50` so it always sits UNDER the positioner it dims
 * behind.
 */
const BACKDROP_CLASSES = [
  "fixed",
  "inset-0",
  "z-40",
  "bg-foreground/20",
  "transition-opacity",
  "duration-150",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:opacity-0",
];

/**
 * Utilities `Popover.Positioner` must render. Base UI writes this element's
 * `position`, `left`, `top` and `--positioner-*` custom properties INLINE, so the
 * recipe may only add stacking and focus concerns — never layout that would
 * fight the positioning engine. Identical reasoning to `selectPositionerRecipe`.
 */
const POSITIONER_CLASSES = ["z-50", "outline-none"];

/**
 * Utilities `Popover.Popup` must render. The card itself: everything here is
 * paint and box, and NOTHING is placement — contrast `dialogPopupRecipe`, which
 * owns `fixed` centring because Base UI positions nothing for a dialog.
 */
const POPUP_CLASSES = [
  "min-w-56",
  "max-w-sm",
  "rounded-lg",
  "border",
  "border-border",
  "bg-popover",
  "p-4",
  "text-popover-foreground",
  "shadow-lg",
  "outline-none",
  "transition-all",
  "duration-150",
  "data-[starting-style]:scale-95",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:scale-95",
  "data-[ending-style]:opacity-0",
];

/**
 * Utilities `Popover.Arrow` must render. Base UI positions the arrow inline on
 * whichever axis the resolved side needs, so this recipe contributes size and
 * paint only: a square rotated into a diamond, wearing the popup's own surface
 * and border tokens so it reads as part of the card rather than a coloured
 * sticker on it.
 */
const ARROW_CLASSES = [
  "h-2.5",
  "w-2.5",
  "rotate-45",
  "rounded-sm",
  "border",
  "border-border",
  "bg-popover",
];

/**
 * Utilities `Popover.Viewport` must render. The part exists to cross-fade
 * content when one popup is opened by several triggers; Base UI absolutely
 * positions the outgoing copy inside it, so it needs a positioning context and a
 * clip, and nothing else.
 */
const VIEWPORT_CLASSES = ["relative", "overflow-hidden"];

/**
 * Utilities `Popover.Title` must render. COLOURLESS on purpose: the popup
 * already establishes `text-popover-foreground`, and restating a colour here
 * would break any fork whose popover foreground differs from its page one.
 */
const TITLE_CLASSES = ["text-sm", "font-semibold"];

/** Utilities `Popover.Description` must render. */
const DESCRIPTION_CLASSES = ["mt-1", "text-sm", "text-muted-foreground"];

/** Utilities `Popover.Close` must render. */
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

/**
 * Every member `@base-ui/react/popover` publishes on its namespace, sorted.
 * `Handle` and `createHandle` are the imperative open/close API for detached
 * triggers; they are re-exported unwrapped rather than dropped, so this
 * catalog's namespace keys still mirror Base UI's 1:1.
 */
const BASE_UI_PART_NAMES = [
  "Arrow",
  "Backdrop",
  "Close",
  "Description",
  "Handle",
  "Popup",
  "Portal",
  "Positioner",
  "Root",
  "Title",
  "Trigger",
  "Viewport",
  "createHandle",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/**
 * The part carrying `data-testid`, searched across the whole document because
 * the open half of a popover is portalled out of the render container.
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
 * Base UI's own modal pointer blocker, if it rendered: a fixed, aria-hidden
 * presentation layer it inserts as the portal's first child. It is unstyleable
 * and carries no test id, so it is found by shape rather than by name.
 */
function maybePointerBlocker(): HTMLElement | null {
  return document.body.querySelector<HTMLElement>(
    "[data-base-ui-portal] > [role='presentation'][aria-hidden='true']",
  );
}

/**
 * Every part at once, so one fixture can carry the whole anatomy. The plain
 * button gives focus somewhere to land that is not a Base UI part.
 */
function FullPopover(): ReactElement {
  return (
    <Popover.Root>
      <Popover.Trigger data-testid="p-trigger">Open</Popover.Trigger>
      <Popover.Portal data-testid="p-portal">
        <Popover.Backdrop data-testid="p-backdrop" />
        <Popover.Positioner data-testid="p-positioner">
          <Popover.Popup data-testid="p-popup">
            <Popover.Arrow data-testid="p-arrow" />
            <Popover.Viewport data-testid="p-viewport">
              <Popover.Title data-testid="p-title">Share this project</Popover.Title>
              <Popover.Description data-testid="p-description">
                Anyone with the link can view it.
              </Popover.Description>
              <button type="button" data-testid="p-copy">
                Copy link
              </button>
              <Popover.Close data-testid="p-close">Done</Popover.Close>
            </Popover.Viewport>
          </Popover.Popup>
        </Popover.Positioner>
      </Popover.Portal>
    </Popover.Root>
  );
}

/**
 * Renders the full fixture and opens it through the trigger.
 *
 * Opening by TRIGGER rather than `defaultOpen` is load-bearing for every focus
 * assertion here: a `defaultOpen` popover leaves `document.activeElement` where
 * it was (measured), because Base UI only runs its focus-management pass for an
 * open transition it actually observed.
 */
async function openPopover(): Promise<void> {
  await render(<FullPopover />);

  await userEvent.click(part("p-trigger"));
  expect(part("p-trigger").getAttribute("aria-expanded")).toBe("true");
}

describe("Popover", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A key added here
    // that Base UI does not have (or a missing one) fails.
    expect(Object.keys(Popover).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("renders the trigger as a button that advertises the popover", async () => {
    await render(<FullPopover />);

    const trigger = part("p-trigger");
    expect(trigger.tagName).toBe("BUTTON");
    expect(trigger.getAttribute("aria-haspopup")).toBe("dialog");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    expect(trigger.hasAttribute("data-popup-open")).toBe(false);
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the popover opens.
    await render(<FullPopover />);

    expect(maybePart("p-portal")).toBeNull();
    expect(maybePart("p-backdrop")).toBeNull();
    expect(maybePart("p-positioner")).toBeNull();
    expect(maybePart("p-popup")).toBeNull();
    expect(maybePart("p-arrow")).toBeNull();
  });

  it("opens the popover when the trigger is clicked", async () => {
    await openPopover();

    const popup = part("p-popup");
    expect(popup.getAttribute("role")).toBe("dialog");
    expect(popup.hasAttribute("data-open")).toBe(true);
    expect(part("p-positioner").hasAttribute("data-open")).toBe(true);
    expect(part("p-backdrop").hasAttribute("data-open")).toBe(true);
  });

  it("marks the trigger data-popup-open and points aria-controls at the popup", async () => {
    await openPopover();

    const trigger = part("p-trigger");
    expect(trigger.hasAttribute("data-popup-open")).toBe(true);
    expect(trigger.getAttribute("aria-controls")).toBe(part("p-popup").id);
  });

  it("names the popup with the title and describes it with the description", async () => {
    // Base UI wires these ids itself; the wrappers must not disturb them.
    await openPopover();

    const popup = part("p-popup");
    expect(popup.getAttribute("aria-labelledby")).toBe(part("p-title").id);
    expect(popup.getAttribute("aria-describedby")).toBe(part("p-description").id);
    expect(part("p-title").tagName).toBe("H2");
    expect(part("p-description").tagName).toBe("P");
  });

  it("publishes the resolved side and alignment on the positioner, popup and arrow", async () => {
    // The three anchored parts each get `data-side`/`data-align`, which is what
    // lets a recipe react to placement with `data-[side=…]:` modifiers. Nothing
    // in this catalog may compute placement itself.
    await openPopover();

    for (const testId of ["p-positioner", "p-popup", "p-arrow"]) {
      expect(part(testId).getAttribute("data-side"), testId).toBe("bottom");
      expect(part(testId).getAttribute("data-align"), testId).toBe("center");
    }
  });

  it("leaves placement to Base UI's inline styles on the positioner and arrow", async () => {
    // The reason POSITIONER_CLASSES is only stacking + focus and ARROW_CLASSES
    // only size + paint. A recipe that added `absolute`/`top-*`/`left-*` here
    // would fight these inline values, which Base UI rewrites on every scroll
    // and resize.
    await openPopover();

    const positioner = part("p-positioner");
    expect(positioner.style.position).toBe("absolute");
    expect(positioner.style.left).not.toBe("");
    expect(positioner.style.getPropertyValue("--positioner-width")).not.toBe("");

    // The arrow is placed on whichever axis the resolved side needs.
    expect(part("p-arrow").style.position).toBe("absolute");

    // …while the popup itself is placed by nothing: it only carries Base UI's
    // measurement custom properties.
    expect(part("p-popup").style.position).toBe("");
  });

  it("renders the trigger with its recipe", async () => {
    await render(<FullPopover />);

    expect(classSet(part("p-trigger"))).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("renders the backdrop and positioner with their recipes", async () => {
    await openPopover();

    expect(classSet(part("p-backdrop"))).toEqual(BACKDROP_CLASSES.toSorted());
    expect(classSet(part("p-positioner"))).toEqual(POSITIONER_CLASSES.toSorted());
  });

  it("renders the popup, arrow and viewport with their recipes", async () => {
    await openPopover();

    expect(classSet(part("p-popup"))).toEqual(POPUP_CLASSES.toSorted());
    expect(classSet(part("p-arrow"))).toEqual(ARROW_CLASSES.toSorted());
    expect(classSet(part("p-viewport"))).toEqual(VIEWPORT_CLASSES.toSorted());
  });

  it("renders the title, description and close with their recipes", async () => {
    await openPopover();

    expect(classSet(part("p-title"))).toEqual(TITLE_CLASSES.toSorted());
    expect(classSet(part("p-description"))).toEqual(DESCRIPTION_CLASSES.toSorted());
    expect(classSet(part("p-close"))).toEqual(CLOSE_CLASSES.toSorted());
  });

  it("moves focus into the popup when the trigger opens it", async () => {
    // Base UI's default `initialFocus`: the first tabbable element inside the
    // popup, not the popup itself.
    await openPopover();

    expect(part("p-popup").contains(document.activeElement)).toBe(true);
    expect(document.activeElement).toBe(part("p-copy"));
  });

  it("lets Tab leave the popup and dismisses the popover when it does", async () => {
    // The headline difference from Dialog, which traps Tab: a non-modal popover
    // hands focus back to the page and closes behind it. Two tabbable parts
    // inside, so the third Tab is the one that leaves.
    await render(
      <div>
        <Popover.Root>
          <Popover.Trigger data-testid="t-trigger">Open</Popover.Trigger>
          <Popover.Portal>
            <Popover.Positioner>
              <Popover.Popup data-testid="t-popup">
                <Popover.Title>Share</Popover.Title>
                <button type="button" data-testid="t-copy">
                  Copy link
                </button>
                <Popover.Close data-testid="t-close">Done</Popover.Close>
              </Popover.Popup>
            </Popover.Positioner>
          </Popover.Portal>
        </Popover.Root>
        <button type="button" data-testid="t-after">
          After
        </button>
      </div>,
    );

    await userEvent.click(part("t-trigger"));
    expect(document.activeElement).toBe(part("t-copy"));

    await userEvent.keyboard("{Tab}");
    expect(document.activeElement).toBe(part("t-close"));

    await userEvent.keyboard("{Tab}");
    expect(part("t-after").contains(document.activeElement)).toBe(true);
    await expect.poll(() => maybePart("t-popup")).toBeNull();
  });

  it("closes and unmounts the popup on Escape", async () => {
    await openPopover();

    await userEvent.keyboard("{Escape}");

    // Polled, not read once: the unmount may be gated behind an animation frame.
    await expect.poll(() => maybePart("p-popup")).toBeNull();
    expect(maybePart("p-backdrop")).toBeNull();
    expect(part("p-trigger").getAttribute("aria-expanded")).toBe("false");
  });

  it("closes and unmounts the popup when the close part is pressed", async () => {
    await openPopover();

    // A genuine `userEvent.click` — the popover is non-modal, so Base UI renders
    // no pointer blocker over the popup and Playwright's actionability check
    // resolves. dialog.test.tsx cannot do this. See the header.
    await userEvent.click(part("p-close"));

    await expect.poll(() => maybePart("p-popup")).toBeNull();
  });

  it("returns focus to the trigger after closing", async () => {
    await openPopover();

    await userEvent.keyboard("{Escape}");
    await expect.poll(() => maybePart("p-popup")).toBeNull();

    expect(document.activeElement).toBe(part("p-trigger"));
  });

  it("renders Base UI's pointer blocker only when the root is modal", async () => {
    // The measured basis for the header's claim, and for using a real click on
    // the close part above: the fixed aria-hidden presentation layer that makes
    // clicks inside a modal dialog untestable simply does not exist here.
    await openPopover();
    expect(maybePointerBlocker()).toBeNull();

    await render(
      <Popover.Root defaultOpen modal>
        <Popover.Trigger data-testid="m-trigger">Open</Popover.Trigger>
        <Popover.Portal>
          <Popover.Positioner>
            <Popover.Popup data-testid="m-popup">
              <Popover.Title>Share</Popover.Title>
              <Popover.Close data-testid="m-close">Done</Popover.Close>
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>,
    );

    const blocker = maybePointerBlocker();
    expect(blocker).not.toBeNull();
    expect(blocker?.style.position).toBe("fixed");
  });

  it("reports open state to onOpenChange", async () => {
    // The caller's handler has to survive Base UI's own mergeProps.
    const onOpenChange = vi.fn();
    await render(
      <Popover.Root onOpenChange={onOpenChange}>
        <Popover.Trigger data-testid="o-trigger">Open</Popover.Trigger>
        <Popover.Portal>
          <Popover.Positioner>
            <Popover.Popup data-testid="o-popup">
              <Popover.Title>Share</Popover.Title>
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>,
    );

    await userEvent.click(part("o-trigger"));

    expect(onOpenChange).toHaveBeenCalledTimes(1);
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("honours a controlled open prop", async () => {
    const { rerender } = await render(
      <Popover.Root open={false}>
        <Popover.Portal>
          <Popover.Positioner>
            <Popover.Popup data-testid="c-popup">
              <Popover.Title>Share</Popover.Title>
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>,
    );

    expect(maybePart("c-popup")).toBeNull();

    await rerender(
      <Popover.Root open>
        <Popover.Portal>
          <Popover.Positioner>
            <Popover.Popup data-testid="c-popup">
              <Popover.Title>Share</Popover.Title>
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>,
    );

    expect(part("c-popup").hasAttribute("data-open")).toBe(true);
  });

  it("lets a caller className override popup and backdrop recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    await render(
      <Popover.Root defaultOpen>
        <Popover.Portal>
          <Popover.Backdrop data-testid="v-backdrop" className="bg-accent" />
          <Popover.Positioner>
            <Popover.Popup data-testid="v-popup" className="max-w-md bg-accent">
              <Popover.Title>Share</Popover.Title>
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>,
    );

    const backdrop = part("v-backdrop");
    expect(backdrop.classList.contains("bg-accent")).toBe(true);
    expect(backdrop.classList.contains("bg-foreground/20")).toBe(false);
    expect(backdrop.classList.contains("fixed")).toBe(true);

    const popup = part("v-popup");
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("max-w-md")).toBe(true);
    expect(popup.classList.contains("max-w-sm")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);
    expect(popup.classList.contains("data-[ending-style]:scale-95")).toBe(true);
  });

  it("carries the popup recipe onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes.
    await render(
      <Popover.Root defaultOpen>
        <Popover.Portal>
          <Popover.Positioner>
            <Popover.Popup data-testid="r-popup" render={<section />}>
              <Popover.Title>Share</Popover.Title>
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>,
    );

    const popup = part("r-popup");
    expect(popup.tagName).toBe("SECTION");
    expect(classSet(popup)).toEqual(POPUP_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Popover.Root defaultOpen>
        <Popover.Portal>
          <Popover.Positioner>
            <Popover.Popup data-testid="share-project" aria-label="Share project">
              <Popover.Title>Share project</Popover.Title>
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>,
    );

    expect(part("share-project").getAttribute("aria-label")).toBe("Share project");
  });
});
