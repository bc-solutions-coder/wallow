import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Dialog } from "./dialog";

/*
 * Dialog behavioural spec (Wallow-m5aq.3.1), the EXEMPLAR every later Wave-2
 * overlay spec is shaped after — itself shaped after the Wallow-m5aq.2.1 Button
 * and Wallow-m5aq.2.8 Select exemplars:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `dialogPopupRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into dialog.styles.ts.
 *   4. Stories carry the visual coverage (see dialog.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <button aria-haspopup="dialog" aria-expanded data-base-ui-click-trigger>  <- Dialog.Trigger
 *     …gains data-popup-open and aria-controls="<popup id>" while open
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-base-ui-portal>                                    <- Dialog.Portal
 *     <div role="presentation" aria-hidden style="position:fixed;inset:0">
 *                                       ^- Base UI's OWN pointer blocker, see below
 *     <div data-open role="presentation" aria-hidden>             <- Dialog.Backdrop
 *     <div data-open role="presentation">                        <- Dialog.Viewport
 *       <div data-open role="dialog" tabindex="-1" aria-labelledby aria-describedby>
 *                                                                <- Dialog.Popup
 *         <h2 id>                                                <- Dialog.Title
 *         <p id>                                                 <- Dialog.Description
 *         <button>                                               <- Dialog.Close
 *
 * Six consequences worth knowing before editing this file:
 *
 *   - the whole open half is PORTALLED to <body>, so every open-state query goes
 *     through `document.body`, never through `render`'s `container`;
 *   - nothing under Dialog.Portal exists in the DOM at all while the dialog is
 *     closed — these are not hidden elements, they are absent ones;
 *   - A MODAL DIALOG ALWAYS RENDERS ONE MORE ELEMENT THAN YOU WROTE: Base UI puts
 *     an unstyleable `<div role="presentation" aria-hidden
 *     style="position:fixed;inset:0">` first inside the portal to block outside
 *     pointer events, whether or not you render a Dialog.Backdrop. Its position
 *     is an INLINE style, so it covers the window even here where no Tailwind is
 *     loaded — while the popup, whose `z-50` comes from a recipe class, gets no
 *     stacking at all. `userEvent` from `vitest/browser` drives REAL Playwright
 *     input, which hit-tests the click point, so a click on anything INSIDE an
 *     open popup hits that blocker and times out on the actionability check.
 *     Pointer interaction inside the popup therefore uses a direct
 *     `element.click()` here, which dispatches straight at the node. The
 *     realistic pointer coverage (including press-the-backdrop-to-close) lives in
 *     dialog.stories.tsx, where `userEvent` is `@testing-library/user-event` and
 *     dispatches synthetic events with no hit-testing at all;
 *   - CLOSING IS ANIMATION-FRAME-DEFERRED. Base UI gates the unmount behind
 *     `useOpenChangeComplete` -> `useAnimationsFinished`, so the popup is still in
 *     the DOM for at least one rAF after the close resolves (measured: still
 *     present immediately after a Close press). Every absence assertion uses
 *     `await expect.poll(...)`, never a bare synchronous `expect(...).toBeNull()`;
 *   - `data-starting-style` / `data-ending-style` only exist DURING a transition,
 *     so no spec here asserts them on an element. They are pinned as
 *     `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *     recipe class sets instead, which is what the catalog actually owns;
 *   - focus only moves into the popup when the dialog is opened by the TRIGGER.
 *     A `defaultOpen` dialog leaves focus on <body> (measured), so every focus
 *     assertion below goes through `openDialog()` rather than `defaultOpen`.
 */

/** Utilities `Dialog.Trigger` must render. Deliberately colourless: the trigger is
 * routinely composed onto a real `Button` via `render`, and a background here
 * would be merged away by tailwind-merge and silently beat the Button's own. */
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

/** Utilities `Dialog.Backdrop` must render. */
const BACKDROP_CLASSES = [
  "fixed",
  "inset-0",
  "z-50",
  "bg-foreground/50",
  "transition-opacity",
  "duration-150",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:opacity-0",
];

/**
 * Utilities `Dialog.Viewport` must render. This part is OPTIONAL — the popup
 * positions itself, so a dialog works with or without a viewport — which is
 * exactly why the recipe may only add the scroll region and stacking. Layout or
 * centring here would fight the popup's own fixed centring in the anatomy that
 * omits the viewport.
 */
const VIEWPORT_CLASSES = ["fixed", "inset-0", "z-50", "overflow-y-auto", "outline-none"];

/**
 * Utilities `Dialog.Popup` must render. Base UI positions NOTHING for a dialog
 * (measured: the popup's only inline style is `--nested-dialogs`), so unlike
 * Select.Positioner this recipe owns the centring outright.
 */
const POPUP_CLASSES = [
  "fixed",
  "top-1/2",
  "left-1/2",
  "z-50",
  "w-full",
  "max-w-lg",
  "-translate-x-1/2",
  "-translate-y-1/2",
  "rounded-lg",
  "border",
  "border-border",
  "bg-popover",
  "p-6",
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

/** Utilities `Dialog.Title` must render. */
const TITLE_CLASSES = ["text-lg", "font-semibold", "text-foreground"];

/** Utilities `Dialog.Description` must render. */
const DESCRIPTION_CLASSES = ["mt-2", "text-sm", "text-muted-foreground"];

/** Utilities `Dialog.Close` must render. */
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
 * Every member `@base-ui/react/dialog` publishes on its namespace, sorted.
 * `Handle` and `createHandle` are the imperative open/close API for detached
 * triggers; they are re-exported unwrapped rather than dropped, so this
 * catalog's namespace keys still mirror Base UI's 1:1.
 */
const BASE_UI_PART_NAMES = [
  "Backdrop",
  "Close",
  "Description",
  "Handle",
  "Popup",
  "Portal",
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
 * the open half of a dialog is portalled out of the render container.
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
 * Every part at once, so one fixture can carry the whole anatomy. The two plain
 * buttons give the focus trap something to cycle through that is not a Base UI
 * part.
 */
function FullDialog(): ReactElement {
  return (
    <Dialog.Root>
      <Dialog.Trigger data-testid="d-trigger">Open</Dialog.Trigger>
      <Dialog.Portal data-testid="d-portal">
        <Dialog.Backdrop data-testid="d-backdrop" />
        <Dialog.Viewport data-testid="d-viewport">
          <Dialog.Popup data-testid="d-popup">
            <Dialog.Title data-testid="d-title">Delete project</Dialog.Title>
            <Dialog.Description data-testid="d-description">
              This cannot be undone.
            </Dialog.Description>
            <button type="button" data-testid="d-confirm">
              Confirm
            </button>
            <Dialog.Close data-testid="d-close">Cancel</Dialog.Close>
          </Dialog.Popup>
        </Dialog.Viewport>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

/**
 * Renders the full fixture and opens it through the trigger.
 *
 * Opening by TRIGGER rather than `defaultOpen` is load-bearing for every focus
 * assertion here: a `defaultOpen` dialog leaves `document.activeElement` on
 * `<body>` (measured), because Base UI only runs its focus-management pass for
 * an open transition it actually observed.
 */
async function openDialog(): Promise<void> {
  await render(<FullDialog />);

  await userEvent.click(part("d-trigger"));
  expect(part("d-trigger").getAttribute("aria-expanded")).toBe("true");
}

describe("Dialog", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A key added here
    // that Base UI does not have (or a missing one) fails.
    expect(Object.keys(Dialog).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("renders the trigger as a button that advertises the dialog", async () => {
    await render(<FullDialog />);

    const trigger = part("d-trigger");
    expect(trigger.tagName).toBe("BUTTON");
    expect(trigger.getAttribute("aria-haspopup")).toBe("dialog");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    expect(trigger.hasAttribute("data-popup-open")).toBe(false);
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the dialog opens.
    await render(<FullDialog />);

    expect(maybePart("d-portal")).toBeNull();
    expect(maybePart("d-backdrop")).toBeNull();
    expect(maybePart("d-viewport")).toBeNull();
    expect(maybePart("d-popup")).toBeNull();
    expect(maybePart("d-title")).toBeNull();
  });

  it("opens the dialog when the trigger is clicked", async () => {
    await openDialog();

    const popup = part("d-popup");
    expect(popup.getAttribute("role")).toBe("dialog");
    expect(popup.hasAttribute("data-open")).toBe(true);
    expect(part("d-backdrop").hasAttribute("data-open")).toBe(true);
  });

  it("marks the trigger data-popup-open and points aria-controls at the popup", async () => {
    await openDialog();

    const trigger = part("d-trigger");
    expect(trigger.hasAttribute("data-popup-open")).toBe(true);
    expect(trigger.getAttribute("aria-controls")).toBe(part("d-popup").id);
  });

  it("names the popup with the title and describes it with the description", async () => {
    // Base UI wires these ids itself; the wrappers must not disturb them.
    await openDialog();

    const popup = part("d-popup");
    expect(popup.getAttribute("aria-labelledby")).toBe(part("d-title").id);
    expect(popup.getAttribute("aria-describedby")).toBe(part("d-description").id);
    expect(part("d-title").tagName).toBe("H2");
    expect(part("d-description").tagName).toBe("P");
  });

  it("renders the trigger with its recipe", async () => {
    await render(<FullDialog />);

    expect(classSet(part("d-trigger"))).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("renders the backdrop, viewport and popup with their recipes", async () => {
    await openDialog();

    expect(classSet(part("d-backdrop"))).toEqual(BACKDROP_CLASSES.toSorted());
    expect(classSet(part("d-viewport"))).toEqual(VIEWPORT_CLASSES.toSorted());
    expect(classSet(part("d-popup"))).toEqual(POPUP_CLASSES.toSorted());
  });

  it("renders the title and description with their recipes", async () => {
    await openDialog();

    expect(classSet(part("d-title"))).toEqual(TITLE_CLASSES.toSorted());
    expect(classSet(part("d-description"))).toEqual(DESCRIPTION_CLASSES.toSorted());
  });

  it("renders the close button with its recipe", async () => {
    await openDialog();

    expect(classSet(part("d-close"))).toEqual(CLOSE_CLASSES.toSorted());
  });

  it("moves focus into the popup when the trigger opens it", async () => {
    // Base UI's default `initialFocus`: the first tabbable element inside the
    // popup, not the popup itself.
    await openDialog();

    expect(part("d-popup").contains(document.activeElement)).toBe(true);
    expect(document.activeElement).toBe(part("d-confirm"));
  });

  it("traps Tab inside the popup", async () => {
    await openDialog();

    // Two tabbable elements inside, so four presses must wrap twice. Focus
    // leaving the popup even once — onto the trigger, the body, or one of Base
    // UI's focus guards — fails.
    for (let index = 0; index < 4; index += 1) {
      await userEvent.keyboard("{Tab}");
      expect(part("d-popup").contains(document.activeElement)).toBe(true);
    }
  });

  it("closes and unmounts the popup on Escape", async () => {
    await openDialog();

    await userEvent.keyboard("{Escape}");

    // Polled, not read once: the unmount is gated behind an animation frame.
    await expect.poll(() => maybePart("d-popup")).toBeNull();
    expect(maybePart("d-backdrop")).toBeNull();
    expect(part("d-trigger").getAttribute("aria-expanded")).toBe("false");
  });

  it("closes and unmounts the popup when the close part is pressed", async () => {
    await openDialog();

    // A direct DOM click rather than `userEvent.click`: Base UI's own fixed
    // pointer blocker covers the unstyled popup in this project, so Playwright's
    // actionability check would never resolve. See the header.
    part("d-close").click();

    await expect.poll(() => maybePart("d-popup")).toBeNull();
  });

  it("returns focus to the trigger after closing", async () => {
    await openDialog();

    await userEvent.keyboard("{Escape}");
    await expect.poll(() => maybePart("d-popup")).toBeNull();

    expect(document.activeElement).toBe(part("d-trigger"));
  });

  it("reports open state to onOpenChange", async () => {
    // The caller's handler has to survive Base UI's own mergeProps.
    const onOpenChange = vi.fn();
    await render(
      <Dialog.Root onOpenChange={onOpenChange}>
        <Dialog.Trigger data-testid="o-trigger">Open</Dialog.Trigger>
        <Dialog.Portal>
          <Dialog.Popup data-testid="o-popup">
            <Dialog.Title>Settings</Dialog.Title>
          </Dialog.Popup>
        </Dialog.Portal>
      </Dialog.Root>,
    );

    await userEvent.click(part("o-trigger"));

    expect(onOpenChange).toHaveBeenCalledTimes(1);
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("honours a controlled open prop", async () => {
    const { rerender } = await render(
      <Dialog.Root open={false}>
        <Dialog.Portal>
          <Dialog.Popup data-testid="c-popup">
            <Dialog.Title>Settings</Dialog.Title>
          </Dialog.Popup>
        </Dialog.Portal>
      </Dialog.Root>,
    );

    expect(maybePart("c-popup")).toBeNull();

    await rerender(
      <Dialog.Root open>
        <Dialog.Portal>
          <Dialog.Popup data-testid="c-popup">
            <Dialog.Title>Settings</Dialog.Title>
          </Dialog.Popup>
        </Dialog.Portal>
      </Dialog.Root>,
    );

    expect(part("c-popup").hasAttribute("data-open")).toBe(true);
  });

  it("lets a caller className override popup and backdrop recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    await render(
      <Dialog.Root defaultOpen>
        <Dialog.Portal>
          <Dialog.Backdrop data-testid="v-backdrop" className="bg-accent" />
          <Dialog.Popup data-testid="v-popup" className="max-w-sm bg-accent">
            <Dialog.Title>Settings</Dialog.Title>
          </Dialog.Popup>
        </Dialog.Portal>
      </Dialog.Root>,
    );

    const backdrop = part("v-backdrop");
    expect(backdrop.classList.contains("bg-accent")).toBe(true);
    expect(backdrop.classList.contains("bg-foreground/50")).toBe(false);
    expect(backdrop.classList.contains("fixed")).toBe(true);

    const popup = part("v-popup");
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("max-w-sm")).toBe(true);
    expect(popup.classList.contains("max-w-lg")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);
    expect(popup.classList.contains("data-[ending-style]:scale-95")).toBe(true);
  });

  it("carries the popup recipe onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes.
    await render(
      <Dialog.Root defaultOpen>
        <Dialog.Portal>
          <Dialog.Popup data-testid="r-popup" render={<section />}>
            <Dialog.Title>Settings</Dialog.Title>
          </Dialog.Popup>
        </Dialog.Portal>
      </Dialog.Root>,
    );

    const popup = part("r-popup");
    expect(popup.tagName).toBe("SECTION");
    expect(classSet(popup)).toEqual(POPUP_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Dialog.Root defaultOpen>
        <Dialog.Portal>
          <Dialog.Popup data-testid="delete-project" aria-label="Delete project">
            <Dialog.Title>Delete project</Dialog.Title>
          </Dialog.Popup>
        </Dialog.Portal>
      </Dialog.Root>,
    );

    expect(part("delete-project").getAttribute("aria-label")).toBe("Delete project");
  });
});
