import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Menu } from "./menu";

/*
 * Menu behavioural spec (Wallow-m5aq.3.6), shaped after the Wallow-m5aq.3.1
 * Dialog exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `menuItemRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into menu.styles.ts.
 *   4. Stories carry the visual coverage (see menu.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <button aria-haspopup="menu" aria-expanded>                   <- Menu.Trigger
 *     …gains data-popup-open, data-pressed and aria-controls="<popup id>" while open
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-base-ui-portal>                                     <- Menu.Portal
 *     <div data-open role="presentation" data-base-ui-inert>       <- Menu.Backdrop
 *     <div role="presentation" data-base-ui-inert
 *          style="position:fixed;inset:0;clip-path:…">
 *                                     ^- Base UI's OWN pointer blocker, see below
 *     <div data-open data-side data-align role="presentation"
 *          style="position:absolute;left;top;transform">          <- Menu.Positioner
 *       <span data-base-ui-focus-guard>
 *       <div data-open data-side data-align role="menu" tabindex="-1"
 *            aria-labelledby="<trigger id>" aria-orientation="vertical">
 *                                                                <- Menu.Popup
 *         <div aria-hidden style="position:absolute;left:…">      <- Menu.Arrow
 *         <div data-testid>                                       <- Menu.Viewport
 *           <div data-current="true">        …Base UI's own transition child
 *         <div role="group" aria-labelledby>                      <- Menu.Group
 *           <div role="presentation">                             <- Menu.GroupLabel
 *           <div role="menuitem" tabindex="-1">                   <- Menu.Item
 *           <a   role="menuitem" tabindex="-1" href>              <- Menu.LinkItem
 *         <div role="separator" data-orientation="horizontal">    <- Menu.Separator
 *         <div role="menuitemcheckbox" aria-checked data-checked|data-unchecked>
 *                                                                <- Menu.CheckboxItem
 *           <span aria-hidden data-checked>       …only while CHECKED
 *                                                 <- Menu.CheckboxItemIndicator
 *         <div role="group">                                      <- Menu.RadioGroup
 *           <div role="menuitemradio" aria-checked>               <- Menu.RadioItem
 *             <span aria-hidden data-checked>     …only while CHECKED
 *                                                 <- Menu.RadioItemIndicator
 *         <div role="menuitem" aria-haspopup="menu" aria-expanded> <- Menu.SubmenuTrigger
 *       <span data-base-ui-focus-guard>
 *
 * Seven consequences worth knowing before editing this file:
 *
 *   - the whole open half is PORTALLED to <body>, so every open-state query goes
 *     through `document.body`, never through `render`'s `container`;
 *   - nothing under Menu.Portal exists in the DOM at all while the menu is
 *     closed — these are not hidden elements, they are absent ones;
 *   - A MODAL MENU ALWAYS RENDERS ONE MORE ELEMENT THAN YOU WROTE: Base UI puts
 *     an unstyleable `<div role="presentation" style="position:fixed;inset:0">`
 *     inside the portal to block outside pointer events, whether or not you
 *     render a Menu.Backdrop. Its `clip-path` punches a hole for the TRIGGER
 *     only, so `userEvent.click(trigger)` is always fine, while a
 *     `userEvent.click` on anything INSIDE the open popup hits the blocker and
 *     times out on Playwright's actionability check. Interaction inside the
 *     popup therefore goes through the KEYBOARD here (which a menu wants anyway)
 *     or a direct `element.click()`. Realistic pointer coverage lives in
 *     menu.stories.tsx, where `userEvent` is `@testing-library/user-event` and
 *     dispatches synthetic events with no hit-testing at all;
 *   - CLOSING IS ANIMATION-FRAME-DEFERRED. Base UI gates the unmount behind
 *     `useOpenChangeComplete` -> `useAnimationsFinished` (measured: after an item
 *     press the popup is still in the DOM synchronously). Every absence
 *     assertion uses `await expect.poll(...)`, never a bare synchronous
 *     `expect(...).toBeNull()`;
 *   - ROVING FOCUS IS ASYNCHRONOUS. Base UI moves `document.activeElement`
 *     between rows a tick after the key, so every focus assertion is polled too.
 *     Opening by CLICK focuses the POPUP itself, not the first row — that is the
 *     difference from Dialog, where focus lands on the first tabbable child.
 *     Opening by ARROW KEY from the trigger focuses the first row directly;
 *   - `data-starting-style` / `data-ending-style` only exist DURING a transition,
 *     so no spec here asserts them on an element. They are pinned as
 *     `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *     recipe class sets instead, which is what the catalog actually owns;
 *   - `CheckboxItemIndicator` and `RadioItemIndicator` are ABSENT from the DOM
 *     while their row is unchecked (measured), which is why both item recipes
 *     reserve a left gutter instead of letting the indicator sit in the flow.
 */

/** Utilities `Menu.Trigger` must render. Deliberately colourless: the trigger is
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

/**
 * Utilities `Menu.Backdrop` must render. A menu backdrop is an outside-press
 * catcher, NOT a scrim — a menu does not dim the page — and Base UI gives the
 * element no inline positioning (measured), so covering the window is entirely
 * the recipe's job.
 */
const BACKDROP_CLASSES = ["fixed", "inset-0"];

/**
 * Utilities `Menu.Positioner` must render. Base UI owns this element's inline
 * `position`/`left`/`top`/`transform`, so the recipe may only add stacking and
 * focus concerns — the same rule as `selectPositionerRecipe`, and the opposite
 * of `dialogPopupRecipe`, which owns its own centring.
 */
const POSITIONER_CLASSES = ["z-50", "outline-none"];

/**
 * Utilities `Menu.Popup` must render. `relative` is load-bearing: Base UI gives
 * `Menu.Arrow` an inline `position: absolute` and `left` but no `top`, so the
 * popup has to be the arrow's containing block.
 */
const POPUP_CLASSES = [
  "relative",
  "min-w-32",
  "rounded-md",
  "border",
  "border-border",
  "bg-popover",
  "p-1",
  "text-popover-foreground",
  "shadow-md",
  "outline-none",
  "transition-all",
  "duration-150",
  "data-[starting-style]:scale-95",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:scale-95",
  "data-[ending-style]:opacity-0",
];

/**
 * Utilities `Menu.Arrow` must render. The four `data-side` values are measured
 * (`bottom`, `top`, `inline-start`, `inline-end`), and the offsets exist because
 * Base UI sets the arrow's `left` inline but never its cross-axis position.
 */
const ARROW_CLASSES = [
  "size-2.5",
  "rotate-45",
  "rounded-sm",
  "border",
  "border-border",
  "bg-popover",
  "data-[side=bottom]:-top-1",
  "data-[side=top]:-bottom-1",
  "data-[side=inline-start]:-right-1",
  "data-[side=inline-end]:-left-1",
];

/** Utilities `Menu.Viewport` must render: the clipping a content cross-fade needs. */
const VIEWPORT_CLASSES = ["relative", "overflow-hidden"];

/** Utilities `Menu.Group` must render. */
const GROUP_CLASSES = ["flex", "flex-col"];

/** Utilities `Menu.RadioGroup` must render — the same stack as a plain group. */
const RADIO_GROUP_CLASSES = ["flex", "flex-col"];

/** Utilities `Menu.GroupLabel` must render. */
const GROUP_LABEL_CLASSES = ["px-2", "py-1.5", "text-xs", "font-medium", "text-muted-foreground"];

/**
 * The row shape every selectable part shares. Five recipes are composed from it
 * (`Item`, `LinkItem`, `CheckboxItem`, `RadioItem`, `SubmenuTrigger`), so the
 * green phase should factor exactly this list into one module-private constant
 * in menu.styles.ts rather than repeating it five times.
 *
 * Note the horizontal padding is NOT here: the checkbox and radio rows replace
 * the symmetric `px-2` with an asymmetric gutter, and leaving `px-2` in the
 * shared part would leave both `px-2` and `pl-8` on those rows (tailwind-merge
 * does not treat `pl` as overriding `px`).
 */
const ITEM_BASE_CLASSES = [
  "flex",
  "cursor-default",
  "select-none",
  "items-center",
  "gap-2",
  "rounded-sm",
  "py-1.5",
  "text-sm",
  "outline-none",
  "data-[highlighted]:bg-accent",
  "data-[highlighted]:text-accent-foreground",
  "data-[disabled]:opacity-50",
];

/** Utilities `Menu.Item` must render. */
const ITEM_CLASSES = [...ITEM_BASE_CLASSES, "px-2"];

/** Utilities `Menu.LinkItem` must render — an item that happens to be an `<a>`. */
const LINK_ITEM_CLASSES = [...ITEM_BASE_CLASSES, "px-2", "no-underline"];

/**
 * Utilities `Menu.CheckboxItem` must render. `relative` plus the asymmetric
 * `pl-8` gutter hold a place for an indicator that is absent from the DOM while
 * the row is unchecked, so toggling does not shift the label sideways.
 */
const CHECKBOX_ITEM_CLASSES = [...ITEM_BASE_CLASSES, "relative", "pr-2", "pl-8"];

/** Utilities `Menu.RadioItem` must render — the same gutter, for the same reason. */
const RADIO_ITEM_CLASSES = [...ITEM_BASE_CLASSES, "relative", "pr-2", "pl-8"];

/** Utilities `Menu.SubmenuTrigger` must render: an item that stays lit while its submenu is open. */
const SUBMENU_TRIGGER_CLASSES = [
  ...ITEM_BASE_CLASSES,
  "px-2",
  "data-[popup-open]:bg-accent",
  "data-[popup-open]:text-accent-foreground",
];

/** Utilities `Menu.CheckboxItemIndicator` must render, parked in the row's gutter. */
const CHECKBOX_ITEM_INDICATOR_CLASSES = [
  "absolute",
  "left-2",
  "flex",
  "size-4",
  "shrink-0",
  "items-center",
  "justify-center",
  "text-primary",
];

/** Utilities `Menu.RadioItemIndicator` must render. */
const RADIO_ITEM_INDICATOR_CLASSES = [
  "absolute",
  "left-2",
  "flex",
  "size-4",
  "shrink-0",
  "items-center",
  "justify-center",
  "text-primary",
];

/**
 * Utilities `Menu.Separator` must render. The negative horizontal margins pull
 * the rule out to the popup's own `p-1`, so it spans the whole card.
 */
const SEPARATOR_CLASSES = ["-mx-1", "my-1", "h-px", "bg-border"];

/**
 * Every member `@base-ui/react/menu` publishes on its namespace, sorted.
 * `Separator` is Base UI's SHARED separator, re-exported onto the menu
 * namespace by Base UI itself; `Handle`/`createHandle` are the imperative
 * open/close API for detached triggers. Both are kept rather than dropped, so
 * this catalog's namespace keys still mirror Base UI's 1:1.
 */
const BASE_UI_PART_NAMES = [
  "Arrow",
  "Backdrop",
  "CheckboxItem",
  "CheckboxItemIndicator",
  "Group",
  "GroupLabel",
  "Handle",
  "Item",
  "LinkItem",
  "Popup",
  "Portal",
  "Positioner",
  "RadioGroup",
  "RadioItem",
  "RadioItemIndicator",
  "Root",
  "Separator",
  "SubmenuRoot",
  "SubmenuTrigger",
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
 * the open half of a menu is portalled out of the render container.
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

/** The `data-testid` of whatever currently holds focus, for the polled focus assertions. */
function focusedTestId(): string | null {
  return document.activeElement?.getAttribute("data-testid") ?? null;
}

/**
 * Every part at once, so one fixture can carry the whole anatomy. The rows are
 * given real text labels deliberately: this project loads no Tailwind, so a
 * textless row would measure 0x0 and be unreachable to a real pointer.
 */
function FullMenu(): ReactElement {
  return (
    <Menu.Root>
      <Menu.Trigger data-testid="m-trigger">Actions</Menu.Trigger>
      <Menu.Portal data-testid="m-portal">
        <Menu.Backdrop data-testid="m-backdrop" />
        <Menu.Positioner data-testid="m-positioner">
          <Menu.Popup data-testid="m-popup">
            <Menu.Arrow data-testid="m-arrow" />
            <Menu.Group data-testid="m-group">
              <Menu.GroupLabel data-testid="m-group-label">Project</Menu.GroupLabel>
              <Menu.Item data-testid="m-item-duplicate">Duplicate</Menu.Item>
              <Menu.Item data-testid="m-item-rename">Rename</Menu.Item>
              <Menu.LinkItem data-testid="m-link" href="https://example.com/docs">
                Open docs
              </Menu.LinkItem>
            </Menu.Group>
            <Menu.Separator data-testid="m-separator" />
            <Menu.CheckboxItem data-testid="m-checkbox">
              <Menu.CheckboxItemIndicator data-testid="m-checkbox-indicator" />
              Show grid
            </Menu.CheckboxItem>
            <Menu.RadioGroup data-testid="m-radio-group" defaultValue="list">
              <Menu.RadioItem data-testid="m-radio-list" value="list">
                <Menu.RadioItemIndicator data-testid="m-radio-list-indicator" />
                List
              </Menu.RadioItem>
              <Menu.RadioItem data-testid="m-radio-grid" value="grid">
                <Menu.RadioItemIndicator data-testid="m-radio-grid-indicator" />
                Grid
              </Menu.RadioItem>
            </Menu.RadioGroup>
            <Menu.SubmenuRoot>
              <Menu.SubmenuTrigger data-testid="m-sub-trigger">Move to</Menu.SubmenuTrigger>
              <Menu.Portal>
                <Menu.Positioner data-testid="m-sub-positioner">
                  <Menu.Popup data-testid="m-sub-popup">
                    <Menu.Item data-testid="m-sub-item">Archive</Menu.Item>
                  </Menu.Popup>
                </Menu.Positioner>
              </Menu.Portal>
            </Menu.SubmenuRoot>
          </Menu.Popup>
        </Menu.Positioner>
      </Menu.Portal>
    </Menu.Root>
  );
}

/**
 * Renders the full fixture and opens it through the trigger, then waits for Base
 * UI's focus pass to settle on the popup. Clicking the TRIGGER is safe even
 * though the popup is covered by Base UI's pointer blocker: the blocker's
 * `clip-path` punches a hole exactly over the trigger (measured).
 */
async function openMenu(): Promise<void> {
  await render(<FullMenu />);

  await userEvent.click(part("m-trigger"));
  await expect.poll(focusedTestId).toBe("m-popup");
}

/** Steps the roving focus one row and waits for it to land, since it moves a tick late. */
async function pressKey(key: string, expectedTestId: string): Promise<void> {
  await userEvent.keyboard(key);
  await expect.poll(focusedTestId).toBe(expectedTestId);
}

describe("Menu", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A key added here
    // that Base UI does not have (or a missing one) fails. Context Menu and
    // Menubar reuse these very wrappers, so the set has to stay canonical.
    expect(Object.keys(Menu).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("renders the trigger as a button that advertises the menu", async () => {
    await render(<FullMenu />);

    const trigger = part("m-trigger");
    expect(trigger.tagName).toBe("BUTTON");
    expect(trigger.getAttribute("aria-haspopup")).toBe("menu");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    expect(trigger.hasAttribute("data-popup-open")).toBe(false);
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the menu opens.
    await render(<FullMenu />);

    expect(maybePart("m-portal")).toBeNull();
    expect(maybePart("m-backdrop")).toBeNull();
    expect(maybePart("m-positioner")).toBeNull();
    expect(maybePart("m-popup")).toBeNull();
    expect(maybePart("m-item-duplicate")).toBeNull();
  });

  it("opens the menu when the trigger is clicked", async () => {
    await openMenu();

    const popup = part("m-popup");
    expect(popup.getAttribute("role")).toBe("menu");
    expect(popup.getAttribute("aria-orientation")).toBe("vertical");
    expect(popup.hasAttribute("data-open")).toBe(true);
    expect(part("m-backdrop").hasAttribute("data-open")).toBe(true);
  });

  it("marks the trigger data-popup-open and points aria-controls at the popup", async () => {
    await openMenu();

    const trigger = part("m-trigger");
    expect(trigger.hasAttribute("data-popup-open")).toBe(true);
    expect(trigger.getAttribute("aria-controls")).toBe(part("m-popup").id);
    expect(part("m-popup").getAttribute("aria-labelledby")).toBe(trigger.id);
  });

  it("gives every kind of row the role its behaviour promises", async () => {
    // The reason a menu is built on Base UI rather than on buttons in a box: the
    // roles and the checked state are wired for free, and the wrappers must not
    // disturb them.
    await openMenu();

    expect(part("m-item-duplicate").getAttribute("role")).toBe("menuitem");
    expect(part("m-link").tagName).toBe("A");
    expect(part("m-link").getAttribute("role")).toBe("menuitem");
    expect(part("m-checkbox").getAttribute("role")).toBe("menuitemcheckbox");
    expect(part("m-checkbox").getAttribute("aria-checked")).toBe("false");
    expect(part("m-radio-list").getAttribute("role")).toBe("menuitemradio");
    expect(part("m-radio-list").getAttribute("aria-checked")).toBe("true");
    expect(part("m-separator").getAttribute("role")).toBe("separator");
    expect(part("m-group").getAttribute("role")).toBe("group");
    expect(part("m-group").getAttribute("aria-labelledby")).toBe(part("m-group-label").id);
    expect(part("m-sub-trigger").getAttribute("aria-haspopup")).toBe("menu");
  });

  it("renders the trigger with its recipe", async () => {
    await render(<FullMenu />);

    expect(classSet(part("m-trigger"))).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("renders the backdrop, positioner and popup with their recipes", async () => {
    await openMenu();

    expect(classSet(part("m-backdrop"))).toEqual(BACKDROP_CLASSES.toSorted());
    expect(classSet(part("m-positioner"))).toEqual(POSITIONER_CLASSES.toSorted());
    expect(classSet(part("m-popup"))).toEqual(POPUP_CLASSES.toSorted());
  });

  it("renders the arrow with its recipe", async () => {
    await openMenu();

    expect(classSet(part("m-arrow"))).toEqual(ARROW_CLASSES.toSorted());
  });

  it("renders the groups, group label and separator with their recipes", async () => {
    await openMenu();

    expect(classSet(part("m-group"))).toEqual(GROUP_CLASSES.toSorted());
    expect(classSet(part("m-radio-group"))).toEqual(RADIO_GROUP_CLASSES.toSorted());
    expect(classSet(part("m-group-label"))).toEqual(GROUP_LABEL_CLASSES.toSorted());
    expect(classSet(part("m-separator"))).toEqual(SEPARATOR_CLASSES.toSorted());
  });

  it("renders the plain item, link item and submenu trigger with their recipes", async () => {
    await openMenu();

    expect(classSet(part("m-item-duplicate"))).toEqual(ITEM_CLASSES.toSorted());
    expect(classSet(part("m-link"))).toEqual(LINK_ITEM_CLASSES.toSorted());
    expect(classSet(part("m-sub-trigger"))).toEqual(SUBMENU_TRIGGER_CLASSES.toSorted());
  });

  it("renders the checkbox and radio rows with their recipes", async () => {
    await openMenu();

    expect(classSet(part("m-checkbox"))).toEqual(CHECKBOX_ITEM_CLASSES.toSorted());
    expect(classSet(part("m-radio-list"))).toEqual(RADIO_ITEM_CLASSES.toSorted());
  });

  it("renders the checked rows' indicators with their recipes", async () => {
    // Only the CHECKED rows have an indicator in the DOM at all, so the checkbox
    // indicator is reached through a separately-rendered checked fixture.
    await openMenu();

    expect(classSet(part("m-radio-list-indicator"))).toEqual(
      RADIO_ITEM_INDICATOR_CLASSES.toSorted(),
    );
    expect(maybePart("m-radio-grid-indicator")).toBeNull();

    await render(
      <Menu.Root open>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup>
              <Menu.CheckboxItem data-testid="k-checkbox" defaultChecked>
                <Menu.CheckboxItemIndicator data-testid="k-checkbox-indicator" />
                Show grid
              </Menu.CheckboxItem>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>,
    );

    expect(classSet(part("k-checkbox-indicator"))).toEqual(
      CHECKBOX_ITEM_INDICATOR_CLASSES.toSorted(),
    );
  });

  it("renders the viewport with its recipe", async () => {
    await render(
      <Menu.Root open>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup>
              <Menu.Viewport data-testid="v-viewport">
                <Menu.Item data-testid="v-item">Duplicate</Menu.Item>
              </Menu.Viewport>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>,
    );

    expect(classSet(part("v-viewport"))).toEqual(VIEWPORT_CLASSES.toSorted());
    // Base UI wraps the current content in its own `data-current` child, which no
    // recipe reaches — worth pinning so a later change to that shape is noticed.
    expect(part("v-viewport").firstElementChild?.getAttribute("data-current")).toBe("true");
  });

  it("opens with the arrow key from the trigger and highlights the first row", async () => {
    // The keyboard entry path a menu is judged on — so the ambient pointer must be
    // named off the fixture first: a pointer parked (by an earlier file) where a
    // row will mount would hover-steal the highlight from the first row.
    await render(<FullMenu />);

    await userEvent.unhover(part("m-trigger"));
    part("m-trigger").focus();
    await userEvent.keyboard("{ArrowDown}");

    await expect.poll(focusedTestId).toBe("m-item-duplicate");
    expect(part("m-item-duplicate").hasAttribute("data-highlighted")).toBe(true);
    expect(part("m-popup").hasAttribute("data-open")).toBe(true);
  });

  it("moves the highlight down and up the rows with the arrow keys", async () => {
    // The behavioural test this task exists for. Focus is polled at every step
    // because Base UI moves it a tick after the key, and `data-highlighted`
    // follows the same roving pointer.
    await openMenu();

    await pressKey("{ArrowDown}", "m-item-duplicate");
    expect(part("m-item-duplicate").hasAttribute("data-highlighted")).toBe(true);

    await pressKey("{ArrowDown}", "m-item-rename");
    expect(part("m-item-rename").hasAttribute("data-highlighted")).toBe(true);
    expect(part("m-item-duplicate").hasAttribute("data-highlighted")).toBe(false);

    await pressKey("{ArrowDown}", "m-link");
    await pressKey("{ArrowUp}", "m-item-rename");
    await pressKey("{ArrowUp}", "m-item-duplicate");
  });

  it("loops the highlight past the last row and jumps with Home and End", async () => {
    await openMenu();

    await pressKey("{ArrowUp}", "m-sub-trigger");
    await pressKey("{ArrowDown}", "m-item-duplicate");
    await pressKey("{End}", "m-sub-trigger");
    await pressKey("{Home}", "m-item-duplicate");
  });

  it("opens a submenu with ArrowRight and closes it again with ArrowLeft", async () => {
    await openMenu();

    await pressKey("{End}", "m-sub-trigger");
    await userEvent.keyboard("{ArrowRight}");

    await expect.poll(focusedTestId).toBe("m-sub-item");
    expect(part("m-sub-trigger").getAttribute("aria-expanded")).toBe("true");
    expect(part("m-sub-trigger").hasAttribute("data-popup-open")).toBe(true);
    expect(part("m-sub-popup").hasAttribute("data-nested")).toBe(true);

    await userEvent.keyboard("{ArrowLeft}");

    await expect.poll(() => maybePart("m-sub-popup")).toBeNull();
    await expect.poll(focusedTestId).toBe("m-sub-trigger");
    // Only the submenu closed: the parent menu is still on screen.
    expect(part("m-popup").hasAttribute("data-open")).toBe(true);
  });

  it("toggles a checkbox row without closing the menu", async () => {
    await openMenu();

    await pressKey("{End}", "m-sub-trigger");
    await pressKey("{ArrowUp}", "m-radio-grid");
    await pressKey("{ArrowUp}", "m-radio-list");
    await pressKey("{ArrowUp}", "m-checkbox");

    await userEvent.keyboard(" ");

    await expect.poll(() => part("m-checkbox").getAttribute("aria-checked")).toBe("true");
    expect(part("m-checkbox").hasAttribute("data-checked")).toBe(true);
    // The indicator only exists once the row is checked.
    expect(part("m-checkbox-indicator").hasAttribute("data-checked")).toBe(true);
    // `closeOnClick` defaults to false for a checkbox row, so the menu stays up.
    expect(part("m-popup").hasAttribute("data-open")).toBe(true);
  });

  it("moves the selection between radio rows and with it the indicator", async () => {
    await openMenu();

    expect(part("m-radio-list").getAttribute("aria-checked")).toBe("true");
    expect(maybePart("m-radio-grid-indicator")).toBeNull();

    await pressKey("{End}", "m-sub-trigger");
    await pressKey("{ArrowUp}", "m-radio-grid");
    await userEvent.keyboard(" ");

    await expect.poll(() => part("m-radio-grid").getAttribute("aria-checked")).toBe("true");
    expect(part("m-radio-list").getAttribute("aria-checked")).toBe("false");
    expect(maybePart("m-radio-list-indicator")).toBeNull();
    expect(part("m-radio-grid-indicator").hasAttribute("data-checked")).toBe(true);
  });

  it("closes and unmounts the menu on Escape", async () => {
    await openMenu();

    await userEvent.keyboard("{Escape}");

    // Polled, not read once: the unmount is gated behind an animation frame.
    await expect.poll(() => maybePart("m-popup")).toBeNull();
    expect(maybePart("m-backdrop")).toBeNull();
    expect(part("m-trigger").getAttribute("aria-expanded")).toBe("false");
  });

  it("closes and unmounts the menu when a plain item is pressed", async () => {
    await openMenu();

    // A direct DOM click rather than `userEvent.click`: Base UI's own fixed
    // pointer blocker covers the unstyled popup in this project, so Playwright's
    // actionability check would never resolve. See the header.
    part("m-item-duplicate").click();

    await expect.poll(() => maybePart("m-popup")).toBeNull();
  });

  it("returns focus to the trigger after closing", async () => {
    await openMenu();

    await userEvent.keyboard("{Escape}");
    await expect.poll(() => maybePart("m-popup")).toBeNull();

    expect(document.activeElement).toBe(part("m-trigger"));
  });

  it("reports open state to onOpenChange", async () => {
    // The caller's handler has to survive Base UI's own mergeProps.
    const onOpenChange = vi.fn();
    await render(
      <Menu.Root onOpenChange={onOpenChange}>
        <Menu.Trigger data-testid="o-trigger">Actions</Menu.Trigger>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup data-testid="o-popup">
              <Menu.Item data-testid="o-item">Duplicate</Menu.Item>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>,
    );

    await userEvent.click(part("o-trigger"));

    expect(onOpenChange).toHaveBeenCalledTimes(1);
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("honours a controlled open prop", async () => {
    const { rerender } = await render(
      <Menu.Root open={false}>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup data-testid="c-popup">
              <Menu.Item>Duplicate</Menu.Item>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>,
    );

    expect(maybePart("c-popup")).toBeNull();

    await rerender(
      <Menu.Root open>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup data-testid="c-popup">
              <Menu.Item>Duplicate</Menu.Item>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>,
    );

    expect(part("c-popup").hasAttribute("data-open")).toBe(true);
  });

  it("lets a caller className override popup and item recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    await render(
      <Menu.Root open>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup data-testid="w-popup" className="min-w-64 bg-accent">
              <Menu.Item data-testid="w-item" className="px-4 text-destructive">
                Delete
              </Menu.Item>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>,
    );

    const popup = part("w-popup");
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("min-w-64")).toBe(true);
    expect(popup.classList.contains("min-w-32")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);
    expect(popup.classList.contains("data-[ending-style]:scale-95")).toBe(true);

    const item = part("w-item");
    expect(item.classList.contains("px-4")).toBe(true);
    expect(item.classList.contains("px-2")).toBe(false);
    expect(item.classList.contains("text-destructive")).toBe(true);
    expect(item.classList.contains("text-sm")).toBe(true);
    expect(item.classList.contains("data-[highlighted]:bg-accent")).toBe(true);
  });

  it("carries the popup and item recipes onto other elements through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes.
    await render(
      <Menu.Root open>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup data-testid="r-popup" render={<nav />}>
              <Menu.Item data-testid="r-item" render={<span />}>
                Duplicate
              </Menu.Item>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>,
    );

    const popup = part("r-popup");
    expect(popup.tagName).toBe("NAV");
    expect(classSet(popup)).toEqual(POPUP_CLASSES.toSorted());

    const item = part("r-item");
    expect(item.tagName).toBe("SPAN");
    expect(classSet(item)).toEqual(ITEM_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Menu.Root open>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup data-testid="project-actions" aria-label="Project actions">
              <Menu.Item data-testid="project-delete" disabled>
                Delete
              </Menu.Item>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>,
    );

    expect(part("project-actions").getAttribute("aria-label")).toBe("Project actions");
    expect(part("project-delete").hasAttribute("data-disabled")).toBe(true);
  });
});
