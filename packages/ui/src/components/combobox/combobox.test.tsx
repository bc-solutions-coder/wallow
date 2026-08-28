import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Combobox } from "./combobox";

/*
 * Combobox behavioural spec (Wallow-m5aq.4.6), shaped after the Wallow-m5aq.2.8
 * Select spec, which is the right template for this component — not Dialog:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `comboboxItemRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into combobox.styles.ts.
 *   4. Stories carry the visual coverage (see combobox.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <div role="group" data-popup-open data-list-empty>      <- Combobox.InputGroup
 *     <input role="combobox" aria-autocomplete="list">      <- Combobox.Input
 *     <button data-visible>                                 <- Combobox.Clear
 *     <button tabindex="-1" data-popup-open>                <- Combobox.Trigger
 *       <span aria-hidden="true">                           <- Combobox.Icon
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-open data-side data-align data-empty>         <- Combobox.Positioner
 *     <div data-open data-empty>                            <- Combobox.Popup
 *       <div role="status" aria-live="polite">              <- Combobox.Status
 *       <div role="listbox" data-empty>                     <- Combobox.List
 *         <div role="group">                                <- Combobox.Group
 *           <div id="…">                                    <- Combobox.GroupLabel
 *           <div role="option" aria-selected data-selected> <- Combobox.Item
 *             <span data-selected>                          <- Combobox.ItemIndicator
 *
 * FIVE MEASURED DIVERGENCES FROM THE WAVE-2 OVERLAYS, all load-bearing here:
 *
 *   - THERE IS NO POINTER BLOCKER. A combobox popup is non-modal: Base UI parks
 *     no focus, locks no scroll and lays no `pointer-events` shield over the
 *     page. `userEvent.click` on an item lands directly, so "select a filtered
 *     result" is a real pointer test in this file rather than a story-only one.
 *     This is the sharpest break from Dialog, where every pointer interaction had
 *     to go around the blocker.
 *   - FOCUS STAYS ON THE INPUT while the popup is open — that is the whole point
 *     of a combobox — so `renderOpen` waits for the POPUP to mount, not for the
 *     list to take focus the way the Select spec does.
 *   - `Combobox.Icon`'s state interface is EMPTY and the rendered `<span>`
 *     carries no `data-*` attribute at all, so Select's
 *     `data-[popup-open]:rotate-180` chevron flip is impossible on this part.
 *     ICON_CLASSES is static on purpose, and a test below pins that.
 *   - `Combobox.List` gets NO Base UI class of its own (Select.List gets
 *     `base-ui-disable-scrollbar`), so its rendered class set is the recipe alone.
 *   - `Combobox.Row` re-roles the items inside it from `option` to `gridcell`,
 *     so it needs its own `grid` fixture rather than a slot in the main one.
 *
 * Three fixture constraints worth knowing before editing this file:
 *   - `Combobox.Label` labels the TRIGGER. Rendering one alongside a
 *     `Combobox.Input` makes Base UI log a dev-mode error, so the label lives in
 *     its own input-less fixture.
 *   - `Combobox.Empty` renders its children only while the filtered list is
 *     empty, and filtering only happens when `Root` is given `items`, so Empty is
 *     asserted in the filtering fixture rather than the static anatomy one.
 *   - `Combobox.Clear` and `Combobox.ItemIndicator` are unmounted while there is
 *     nothing to clear / nothing selected, so both are rendered `keepMounted`
 *     where their styling is under test.
 */

/** Utilities `Combobox.Label` must render. */
const LABEL_CLASSES = ["text-sm", "font-medium", "text-foreground"];

/**
 * Utilities `Combobox.InputGroup` must render. The field's border and background
 * live here rather than on the input, so the clear button and the chevron sit
 * INSIDE the visual field instead of beside it.
 */
const INPUT_GROUP_CLASSES = [
  "flex",
  "w-full",
  "items-center",
  "gap-2",
  "rounded-md",
  "border",
  "border-input",
  "bg-background",
  "px-3",
  "py-2",
  "text-sm",
  "text-foreground",
  "data-[popup-open]:border-ring",
  "data-[disabled]:opacity-50",
];

/** Utilities `Combobox.Input` must render. */
const INPUT_CLASSES = [
  "min-w-0",
  "flex-1",
  "bg-transparent",
  "text-sm",
  "text-foreground",
  "outline-none",
  "placeholder:text-muted-foreground",
];

/** Utilities `Combobox.Trigger` must render. */
const TRIGGER_CLASSES = [
  "inline-flex",
  "shrink-0",
  "cursor-default",
  "items-center",
  "justify-center",
  "text-muted-foreground",
  "outline-none",
  "data-[disabled]:opacity-50",
];

/**
 * Utilities `Combobox.Icon` must render — STATIC, with no `data-[…]:` modifier.
 * `ComboboxIconState` is the empty interface and the element carries no state
 * attribute, so a modifier here would be CSS that can never match.
 */
const ICON_CLASSES = ["flex", "size-4", "shrink-0", "items-center", "justify-center"];

/**
 * Utilities `Combobox.Clear` must render. It fades on `data-visible` rather than
 * on presence, because `keepMounted` leaves it in the DOM with nothing to clear.
 */
const CLEAR_CLASSES = [
  "flex",
  "size-4",
  "shrink-0",
  "cursor-default",
  "items-center",
  "justify-center",
  "rounded-sm",
  "text-muted-foreground",
  "outline-none",
  "data-[visible]:opacity-100",
];

/** Utilities `Combobox.Backdrop` must render. */
const BACKDROP_CLASSES = ["fixed", "inset-0"];

/**
 * Utilities `Combobox.Positioner` must render. Base UI owns this element's
 * `position`/`transform` inline styles, so the recipe may only add stacking and
 * focus concerns — layout utilities here would fight the positioning engine.
 */
const POSITIONER_CLASSES = ["z-50", "outline-none"];

/**
 * Utilities `Combobox.Popup` must render. `--anchor-width` is Base UI's own
 * custom property, published on the positioner and inherited here, and it is
 * what makes the popup at least as wide as the field it drops out of.
 *
 * No transition utilities, deliberately: this follows `Select.Popup` rather than
 * `Dialog.Popup`, because a combobox popup re-renders on every keystroke and a
 * fade there would both feel wrong and make `toBeVisible()` racy in the stories.
 */
const POPUP_CLASSES = [
  "min-w-[var(--anchor-width)]",
  "rounded-md",
  "border",
  "border-border",
  "bg-popover",
  "py-1",
  "text-popover-foreground",
  "shadow-md",
];

/** Utilities `Combobox.Arrow` must render. */
const ARROW_CLASSES = ["flex", "text-popover-foreground"];

/** Utilities `Combobox.List` must render. Base UI adds no class of its own here. */
const LIST_CLASSES = ["max-h-64", "overflow-y-auto", "outline-none"];

/** Utilities `Combobox.Status` must render. */
const STATUS_CLASSES = ["px-3", "py-1.5", "text-sm", "text-muted-foreground"];

/** Utilities `Combobox.Empty` must render. */
const EMPTY_CLASSES = ["px-3", "py-6", "text-center", "text-sm", "text-muted-foreground"];

/** Utilities `Combobox.Group` must render. */
const GROUP_CLASSES = ["py-1"];

/** Utilities `Combobox.GroupLabel` must render. */
const GROUP_LABEL_CLASSES = ["px-3", "py-1.5", "text-xs", "font-medium", "text-muted-foreground"];

/** Utilities `Combobox.Row` must render. */
const ROW_CLASSES = ["flex", "gap-1"];

/** Utilities `Combobox.Item` must render. */
const ITEM_CLASSES = [
  "flex",
  "cursor-default",
  "select-none",
  "items-center",
  "gap-2",
  "px-3",
  "py-1.5",
  "text-sm",
  "outline-none",
  "data-[highlighted]:bg-accent",
  "data-[highlighted]:text-accent-foreground",
  "data-[disabled]:opacity-50",
];

/** Utilities `Combobox.ItemIndicator` must render. */
const ITEM_INDICATOR_CLASSES = [
  "flex",
  "size-4",
  "shrink-0",
  "items-center",
  "justify-center",
  "text-primary",
];

/** Utilities `Combobox.Chips` must render. */
const CHIPS_CLASSES = ["flex", "flex-wrap", "items-center", "gap-1"];

/**
 * Utilities `Combobox.Chip` must render. `ComboboxChipState` carries only
 * `disabled` — there is no `highlighted` on a chip — so the only state modifier
 * a chip recipe can own is `data-[disabled]:`.
 */
const CHIP_CLASSES = [
  "inline-flex",
  "cursor-default",
  "items-center",
  "gap-1",
  "rounded-sm",
  "bg-secondary",
  "px-2",
  "py-0.5",
  "text-xs",
  "text-secondary-foreground",
  "outline-none",
  "data-[disabled]:opacity-50",
];

/** Utilities `Combobox.ChipRemove` must render. */
const CHIP_REMOVE_CLASSES = [
  "inline-flex",
  "size-3",
  "shrink-0",
  "cursor-default",
  "items-center",
  "justify-center",
  "rounded-sm",
  "text-secondary-foreground",
  "outline-none",
];

/** Utilities `Combobox.Separator` must render. */
const SEPARATOR_CLASSES = ["my-1", "h-px", "bg-border"];

/** Every namespace member Base UI's `@base-ui/react/combobox` publishes. */
const BASE_UI_MEMBER_NAMES = [
  "Arrow",
  "Backdrop",
  "Chip",
  "ChipRemove",
  "Chips",
  "Clear",
  "Collection",
  "Empty",
  "Group",
  "GroupLabel",
  "Icon",
  "Input",
  "InputGroup",
  "Item",
  "ItemIndicator",
  "Label",
  "List",
  "Popup",
  "Portal",
  "Positioner",
  "Root",
  "Row",
  "Separator",
  "Status",
  "Trigger",
  "Value",
  "useFilter",
  "useFilteredItems",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/**
 * The part carrying `data-testid`, searched across the whole document because
 * the popup half of a combobox is portalled out of the render container.
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
 * The static half of the anatomy: every part whose styling does not depend on
 * filtering. `gamma` is disabled, and `Clear` and both `ItemIndicator`s are
 * `keepMounted` because Base UI unmounts them when there is nothing to clear and
 * nothing selected.
 *
 * `Root` gets no `items`, so nothing here is filtered and the list stays stable
 * across a keystroke — filtering has its own fixture below.
 */
function FullCombobox(): ReactElement {
  return (
    <Combobox.Root defaultValue="alpha">
      <Combobox.InputGroup data-testid="c-input-group">
        <Combobox.Input data-testid="c-input" placeholder="Search fonts" />
        <Combobox.Clear data-testid="c-clear" keepMounted />
        <Combobox.Trigger data-testid="c-trigger">
          <Combobox.Icon data-testid="c-icon" />
        </Combobox.Trigger>
      </Combobox.InputGroup>
      <Combobox.Portal>
        <Combobox.Backdrop data-testid="c-backdrop" />
        <Combobox.Positioner data-testid="c-positioner">
          <Combobox.Popup data-testid="c-popup">
            <Combobox.Arrow data-testid="c-arrow" />
            <Combobox.Status data-testid="c-status">3 fonts</Combobox.Status>
            <Combobox.List data-testid="c-list">
              <Combobox.Group data-testid="c-group">
                <Combobox.GroupLabel data-testid="c-group-label">Serif</Combobox.GroupLabel>
                <Combobox.Item value="alpha" data-testid="c-item-alpha">
                  Alpha
                  <Combobox.ItemIndicator data-testid="c-item-alpha-indicator" keepMounted>
                    ✓
                  </Combobox.ItemIndicator>
                </Combobox.Item>
                <Combobox.Separator data-testid="c-separator" />
                <Combobox.Item value="beta" data-testid="c-item-beta">
                  Beta
                  <Combobox.ItemIndicator data-testid="c-item-beta-indicator">
                    ✓
                  </Combobox.ItemIndicator>
                </Combobox.Item>
                <Combobox.Item value="gamma" disabled data-testid="c-item-gamma">
                  Gamma
                </Combobox.Item>
              </Combobox.Group>
            </Combobox.List>
          </Combobox.Popup>
        </Combobox.Positioner>
      </Combobox.Portal>
    </Combobox.Root>
  );
}

/** The three fonts the filtering fixture offers; two of them share a prefix. */
const FONTS = ["Alpha", "Alpine", "Beta"];

/**
 * The filtering half of the anatomy. `Root` gets `items`, which is what turns
 * typing into filtering at all, and `List` takes the function child Base UI
 * implicitly wraps in a `Collection`.
 */
function FilteringCombobox(): ReactElement {
  return (
    <Combobox.Root items={FONTS}>
      <Combobox.InputGroup data-testid="f-input-group">
        <Combobox.Input data-testid="f-input" placeholder="Search fonts" />
      </Combobox.InputGroup>
      <span data-testid="f-value">
        <Combobox.Value />
      </span>
      <Combobox.Portal>
        <Combobox.Positioner data-testid="f-positioner">
          <Combobox.Popup data-testid="f-popup">
            <Combobox.Empty data-testid="f-empty">No fonts found</Combobox.Empty>
            <Combobox.List data-testid="f-list">
              {(item: string) => (
                <Combobox.Item key={item} value={item} data-testid={`f-item-${item}`}>
                  {item}
                </Combobox.Item>
              )}
            </Combobox.List>
          </Combobox.Popup>
        </Combobox.Positioner>
      </Combobox.Portal>
    </Combobox.Root>
  );
}

/**
 * Renders the static fixture and opens the popup by clicking the trigger.
 *
 * Unlike the Select spec's equivalent this waits for the POPUP to mount rather
 * than for the list to take focus: a combobox is non-modal and focus stays on
 * the input, so a wait on list focus would time out forever.
 */
async function renderOpen(): Promise<void> {
  await render(<FullCombobox />);

  await userEvent.click(part("c-trigger"));

  await vi.waitFor(() => {
    expect(maybePart("c-popup")).not.toBeNull();
  });
}

describe("Combobox", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A member added
    // here that Base UI does not have (or a missing one) fails.
    expect(Object.keys(Combobox).toSorted()).toEqual(BASE_UI_MEMBER_NAMES);
  });

  it("renders the input group and input with their recipes", async () => {
    await render(<FullCombobox />);

    const group = part("c-input-group");
    expect(group.getAttribute("role")).toBe("group");
    expect(classSet(group)).toEqual(INPUT_GROUP_CLASSES.toSorted());

    const input = part("c-input");
    expect(input.tagName).toBe("INPUT");
    expect(input.getAttribute("role")).toBe("combobox");
    expect(input.getAttribute("aria-autocomplete")).toBe("list");
    expect(input.getAttribute("aria-expanded")).toBe("false");
    expect(classSet(input)).toEqual(INPUT_CLASSES.toSorted());
  });

  it("renders the trigger, icon and clear button with their recipes", async () => {
    await render(<FullCombobox />);

    const trigger = part("c-trigger");
    expect(trigger.tagName).toBe("BUTTON");
    expect(classSet(trigger)).toEqual(TRIGGER_CLASSES.toSorted());

    const icon = part("c-icon");
    expect(icon.getAttribute("aria-hidden")).toBe("true");
    expect(icon.parentElement).toBe(trigger);
    expect(classSet(icon)).toEqual(ICON_CLASSES.toSorted());

    expect(classSet(part("c-clear"))).toEqual(CLEAR_CLASSES.toSorted());
  });

  it("fills an empty Combobox.Icon with a default inline-svg chevron", async () => {
    // The same default `Select.Icon` gets, for the same reasons: no icon library
    // in this package, and a text glyph sits off the baseline of the size-4 box.
    // Autocomplete re-exports these parts verbatim, so this one default covers
    // that component too.
    await render(<FullCombobox />);

    const icon = part("c-icon");
    const chevron = icon.querySelector("svg");
    expect(chevron, "Combobox.Icon rendered no default chevron").not.toBeNull();

    expect(icon.textContent).toBe("");
    expect(icon.children.length).toBe(1);
  });

  it("lets a caller's children replace the default chevron", async () => {
    await render(
      <Combobox.Root defaultValue="alpha">
        <Combobox.Trigger>
          <Combobox.Icon data-testid="ov-icon">▾</Combobox.Icon>
        </Combobox.Trigger>
      </Combobox.Root>,
    );

    const icon = part("ov-icon");
    expect(icon.textContent).toBe("▾");
    expect(icon.querySelector("svg")).toBeNull();
  });

  it("gives the icon no state attribute to hang a modifier on", async () => {
    // Measured, and the reason ICON_CLASSES is static: `ComboboxIconState` is
    // the empty interface, so Select's `data-[popup-open]:rotate-180` chevron
    // flip is impossible here. The trigger is where a fork must drive it from.
    await renderOpen();

    const icon = part("c-icon");
    const stateAttributes = icon.getAttributeNames().filter((name) => name.startsWith("data-"));
    expect(stateAttributes).toEqual(["data-testid"]);
    expect(part("c-trigger").hasAttribute("data-popup-open")).toBe(true);
  });

  it("renders the label with its recipe and names the trigger with it", async () => {
    // No `Combobox.Input` in this fixture on purpose: Base UI's label points at
    // the TRIGGER, and pairing it with an input logs a dev-mode error.
    await render(
      <Combobox.Root defaultValue="alpha">
        <Combobox.Label data-testid="l-label">Font</Combobox.Label>
        <Combobox.Trigger data-testid="l-trigger">
          <Combobox.Icon>▾</Combobox.Icon>
        </Combobox.Trigger>
      </Combobox.Root>,
    );

    const label = part("l-label");
    expect(classSet(label)).toEqual(LABEL_CLASSES.toSorted());
    expect(part("l-trigger").getAttribute("aria-labelledby")).toContain(label.id);
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the popup opens.
    await render(<FullCombobox />);

    expect(maybePart("c-positioner")).toBeNull();
    expect(maybePart("c-popup")).toBeNull();
    expect(maybePart("c-list")).toBeNull();
    expect(maybePart("c-backdrop")).toBeNull();
    expect(maybePart("c-item-alpha")).toBeNull();
  });

  it("renders the positioner, popup and backdrop with their recipes", async () => {
    await renderOpen();

    expect(classSet(part("c-positioner"))).toEqual(POSITIONER_CLASSES.toSorted());
    expect(classSet(part("c-popup"))).toEqual(POPUP_CLASSES.toSorted());
    expect(classSet(part("c-backdrop"))).toEqual(BACKDROP_CLASSES.toSorted());
  });

  it("renders the list, status and arrow with their recipes", async () => {
    await renderOpen();

    const list = part("c-list");
    expect(list.getAttribute("role")).toBe("listbox");
    // Unlike Select.List, Base UI adds no class of its own here, so the rendered
    // set is the recipe and nothing else.
    expect(classSet(list)).toEqual(LIST_CLASSES.toSorted());

    const status = part("c-status");
    expect(status.getAttribute("role")).toBe("status");
    expect(status.getAttribute("aria-live")).toBe("polite");
    expect(classSet(status)).toEqual(STATUS_CLASSES.toSorted());

    expect(classSet(part("c-arrow"))).toEqual(ARROW_CLASSES.toSorted());
  });

  it("renders the group, group label and separator with their recipes", async () => {
    await renderOpen();

    expect(part("c-group").getAttribute("role")).toBe("group");
    expect(classSet(part("c-group"))).toEqual(GROUP_CLASSES.toSorted());

    const groupLabel = part("c-group-label");
    expect(classSet(groupLabel)).toEqual(GROUP_LABEL_CLASSES.toSorted());
    expect(part("c-group").getAttribute("aria-labelledby")).toBe(groupLabel.id);

    expect(part("c-separator").getAttribute("role")).toBe("separator");
    expect(classSet(part("c-separator"))).toEqual(SEPARATOR_CLASSES.toSorted());
  });

  it("renders the selected item with data-selected and a mounted indicator", async () => {
    await renderOpen();

    const item = part("c-item-alpha");
    expect(item.getAttribute("role")).toBe("option");
    expect(item.hasAttribute("data-selected")).toBe(true);
    expect(item.getAttribute("aria-selected")).toBe("true");
    expect(classSet(item)).toEqual(ITEM_CLASSES.toSorted());

    expect(classSet(part("c-item-alpha-indicator"))).toEqual(ITEM_INDICATOR_CLASSES.toSorted());
  });

  it("leaves an unselected item's indicator unmounted", async () => {
    // Base UI's default (`keepMounted` is false), so the tick cannot be styled
    // into invisibility — it genuinely is not in the DOM.
    await renderOpen();

    expect(part("c-item-beta").getAttribute("aria-selected")).toBe("false");
    expect(part("c-item-beta").hasAttribute("data-selected")).toBe(false);
    expect(maybePart("c-item-beta-indicator")).toBeNull();
  });

  it("exposes a disabled item as data-disabled and aria-disabled", async () => {
    await renderOpen();

    const gamma = part("c-item-gamma");
    expect(gamma.hasAttribute("data-disabled")).toBe(true);
    expect(gamma.getAttribute("aria-disabled")).toBe("true");
  });

  it("moves data-highlighted with the arrow keys while focus stays on the input", async () => {
    await renderOpen();

    // The measured non-modal contract: the input keeps focus for the whole open
    // lifetime, even though the highlight walks the list. That is what lets a
    // caller keep typing to narrow the list without losing the highlight.
    expect(document.activeElement).toBe(part("c-input"));

    // Measured: opening already highlights the SELECTED item, so the first
    // ArrowDown moves off it rather than onto it.
    await vi.waitFor(() => {
      expect(part("c-item-alpha").hasAttribute("data-highlighted")).toBe(true);
    });

    await userEvent.keyboard("{ArrowDown}");

    await vi.waitFor(() => {
      expect(part("c-item-beta").hasAttribute("data-highlighted")).toBe(true);
      expect(part("c-item-alpha").hasAttribute("data-highlighted")).toBe(false);
    });
    expect(document.activeElement).toBe(part("c-input"));

    // Measured, not assumed, and the same as Select: Base UI still HIGHLIGHTS a
    // disabled item rather than skipping past it (the item stays unselectable —
    // that is what `data-disabled`/`aria-disabled` are for). The item recipe's
    // `data-[highlighted]:` and `data-[disabled]:` modifiers therefore have to
    // survive being on the same element at the same time.
    await userEvent.keyboard("{ArrowDown}");

    await vi.waitFor(() => {
      expect(part("c-item-gamma").hasAttribute("data-highlighted")).toBe(true);
      expect(part("c-item-gamma").hasAttribute("data-disabled")).toBe(true);
    });
  });

  it("lays down no pointer blocker over the page", async () => {
    // The single sharpest divergence from every wave-2 overlay. Dialog's open
    // popup covers the page in a `pointer-events` shield, so its specs had to
    // work around it; a combobox popup is NON-MODAL and does not, which is why
    // the pointer-selection tests below are real clicks.
    //
    // A positive control keeps this honest: the element under the pointer at the
    // page's top-left has to be something OUTSIDE the portal.
    await renderOpen();

    const outside = document.elementFromPoint(2, 2);
    expect(outside).not.toBeNull();
    expect(part("c-popup").contains(outside)).toBe(false);
    expect(document.body.style.pointerEvents).toBe("");
  });

  it("filters the list by typing and drops the non-matching items", async () => {
    await render(<FilteringCombobox />);

    await userEvent.click(part("f-input"));
    await vi.waitFor(() => {
      expect(maybePart("f-list")).not.toBeNull();
    });

    expect(maybePart("f-item-Alpha")).not.toBeNull();
    expect(maybePart("f-item-Alpine")).not.toBeNull();
    expect(maybePart("f-item-Beta")).not.toBeNull();

    await userEvent.type(part("f-input"), "alp");

    // Filtering runs through React state, so the non-matching rows leave a
    // commit after the keystrokes resolve — polled for, not read once.
    await expect.poll(() => maybePart("f-item-Beta")).toBeNull();
    expect(maybePart("f-item-Alpha")).not.toBeNull();
    expect(maybePart("f-item-Alpine")).not.toBeNull();
  });

  it("selects a filtered result by pointer click and closes the popup", async () => {
    await render(<FilteringCombobox />);

    await userEvent.click(part("f-input"));
    await userEvent.type(part("f-input"), "alpi");
    await expect.poll(() => maybePart("f-item-Alpha")).toBeNull();

    // A real click, straight onto the item — no blocker to work around.
    await userEvent.click(part("f-item-Alpine"));

    await vi.waitFor(() => {
      expect(part("f-value").textContent).toBe("Alpine");
    });
    expect((part("f-input") as HTMLInputElement).value).toBe("Alpine");
    await expect.poll(() => maybePart("f-popup")).toBeNull();
  });

  it("marks the empty state on the popup, list and input group when nothing matches", async () => {
    await render(<FilteringCombobox />);

    await userEvent.click(part("f-input"));
    await userEvent.type(part("f-input"), "zzz");

    await expect.poll(() => maybePart("f-item-Alpha")).toBeNull();

    expect(part("f-popup").hasAttribute("data-empty")).toBe(true);
    expect(part("f-list").hasAttribute("data-empty")).toBe(true);
    expect(part("f-positioner").hasAttribute("data-empty")).toBe(true);
    expect(part("f-input-group").hasAttribute("data-list-empty")).toBe(true);

    expect(classSet(part("f-empty"))).toEqual(EMPTY_CLASSES.toSorted());
  });

  it("reports the selected value and the popup state to the caller's handlers", async () => {
    // The caller's handlers have to survive Base UI's own mergeProps.
    const onValueChange = vi.fn();
    const onOpenChange = vi.fn();
    await render(
      <Combobox.Root items={FONTS} onValueChange={onValueChange} onOpenChange={onOpenChange}>
        <Combobox.InputGroup>
          <Combobox.Input data-testid="h-input" />
        </Combobox.InputGroup>
        <Combobox.Portal>
          <Combobox.Positioner>
            <Combobox.Popup>
              <Combobox.List>
                {(item: string) => (
                  <Combobox.Item key={item} value={item} data-testid={`h-item-${item}`}>
                    {item}
                  </Combobox.Item>
                )}
              </Combobox.List>
            </Combobox.Popup>
          </Combobox.Positioner>
        </Combobox.Portal>
      </Combobox.Root>,
    );

    await userEvent.click(part("h-input"));

    await vi.waitFor(() => {
      expect(onOpenChange).toHaveBeenCalled();
    });
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);

    await userEvent.click(part("h-item-Beta"));

    await vi.waitFor(() => {
      expect(onValueChange).toHaveBeenCalled();
    });
    expect(onValueChange.mock.calls[0]?.[0]).toBe("Beta");
  });

  it("selects a highlighted result with the keyboard", async () => {
    await render(<FilteringCombobox />);

    // A pointer parked (by an earlier file) where the popup mounts would
    // hover-steal the highlight from the row ArrowDown lands on.
    await userEvent.unhover(part("f-input"));
    part("f-input").focus();
    await userEvent.keyboard("alp");
    await expect.poll(() => maybePart("f-item-Beta")).toBeNull();

    await userEvent.keyboard("{ArrowDown}");
    await vi.waitFor(() => {
      expect(part("f-item-Alpha").hasAttribute("data-highlighted")).toBe(true);
    });

    await userEvent.keyboard("{Enter}");

    await vi.waitFor(() => {
      expect(part("f-value").textContent).toBe("Alpha");
    });
  });

  it("re-roles the items inside a row as grid cells", async () => {
    // Measured: `Combobox.Row` is not decoration. It turns the listbox into a
    // grid, so the very same `Combobox.Item` renders `role="gridcell"` instead of
    // `role="option"` — which is why the row lives in its own fixture.
    await render(
      <Combobox.Root grid items={[["Alpha", "Beta"]]}>
        <Combobox.InputGroup>
          <Combobox.Input data-testid="g-input" />
        </Combobox.InputGroup>
        <Combobox.Portal>
          <Combobox.Positioner>
            <Combobox.Popup>
              <Combobox.List data-testid="g-list">
                <Combobox.Row data-testid="g-row">
                  <Combobox.Item value="Alpha" data-testid="g-item-alpha">
                    Alpha
                  </Combobox.Item>
                  <Combobox.Item value="Beta" data-testid="g-item-beta">
                    Beta
                  </Combobox.Item>
                </Combobox.Row>
              </Combobox.List>
            </Combobox.Popup>
          </Combobox.Positioner>
        </Combobox.Portal>
      </Combobox.Root>,
    );

    await userEvent.click(part("g-input"));
    await vi.waitFor(() => {
      expect(maybePart("g-row")).not.toBeNull();
    });

    const row = part("g-row");
    expect(row.getAttribute("role")).toBe("row");
    expect(classSet(row)).toEqual(ROW_CLASSES.toSorted());
    expect(part("g-item-alpha").getAttribute("role")).toBe("gridcell");
  });

  it("renders a multi-select's chips, chip and chip remove with their recipes", async () => {
    await render(
      <Combobox.Root multiple defaultValue={["Alpha", "Beta"]} items={FONTS}>
        <Combobox.Chips data-testid="m-chips">
          <Combobox.Value>
            {(selected: string[]) =>
              selected.map((value) => (
                <Combobox.Chip key={value} data-testid={`m-chip-${value}`}>
                  {value}
                  <Combobox.ChipRemove data-testid={`m-chip-remove-${value}`} />
                </Combobox.Chip>
              ))
            }
          </Combobox.Value>
          <Combobox.Input data-testid="m-input" />
        </Combobox.Chips>
      </Combobox.Root>,
    );

    expect(classSet(part("m-chips"))).toEqual(CHIPS_CLASSES.toSorted());
    expect(classSet(part("m-chip-Alpha"))).toEqual(CHIP_CLASSES.toSorted());
    expect(classSet(part("m-chip-remove-Alpha"))).toEqual(CHIP_REMOVE_CLASSES.toSorted());
  });

  it("removes a selected value when its chip remove button is pressed", async () => {
    await render(
      <Combobox.Root multiple defaultValue={["Alpha", "Beta"]} items={FONTS}>
        <Combobox.Chips data-testid="r-chips">
          <Combobox.Value>
            {(selected: string[]) =>
              selected.map((value) => (
                <Combobox.Chip key={value} data-testid={`r-chip-${value}`}>
                  {value}
                  <Combobox.ChipRemove data-testid={`r-chip-remove-${value}`} />
                </Combobox.Chip>
              ))
            }
          </Combobox.Value>
          <Combobox.Input data-testid="r-input" />
        </Combobox.Chips>
      </Combobox.Root>,
    );

    await userEvent.click(part("r-chip-remove-Alpha"));

    await expect.poll(() => maybePart("r-chip-Alpha")).toBeNull();
    expect(maybePart("r-chip-Beta")).not.toBeNull();
  });

  it("clears the selected value when the clear button is pressed", async () => {
    // Measured: `Clear`'s mounted-ness tracks the SELECTED VALUE, not the typed
    // text — `defaultInputValue` alone leaves it unmounted. That is why the
    // static fixture selects `alpha` before asserting the clear button's recipe.
    await render(
      <Combobox.Root items={FONTS} defaultValue="Alpha">
        <Combobox.InputGroup>
          <Combobox.Input data-testid="x-input" />
          <Combobox.Clear data-testid="x-clear" />
        </Combobox.InputGroup>
      </Combobox.Root>,
    );

    expect((part("x-input") as HTMLInputElement).value).toBe("Alpha");

    await userEvent.click(part("x-clear"));

    await vi.waitFor(() => {
      expect((part("x-input") as HTMLInputElement).value).toBe("");
    });
    // With nothing left to clear and no `keepMounted`, the button leaves the DOM.
    await expect.poll(() => maybePart("x-clear")).toBeNull();
  });

  it("exposes a disabled combobox as data-disabled and drops it from the tab order", async () => {
    await render(
      <Combobox.Root disabled>
        <Combobox.InputGroup data-testid="d-group">
          <Combobox.Input data-testid="d-input" />
          <Combobox.Trigger data-testid="d-trigger">
            <Combobox.Icon>▾</Combobox.Icon>
          </Combobox.Trigger>
        </Combobox.InputGroup>
      </Combobox.Root>,
    );

    expect(part("d-group").hasAttribute("data-disabled")).toBe(true);
    expect(part("d-input").hasAttribute("disabled")).toBe(true);
    expect(part("d-trigger").hasAttribute("data-disabled")).toBe(true);
  });

  it("closes on Escape and takes the whole portalled subtree with it", async () => {
    // MEASURED, and the reason this uses the filtering fixture rather than
    // `renderOpen()`: Escape only closes an EMPTY-input combobox. With text in
    // the input — which the static fixture has, because selecting `alpha` fills
    // it — Base UI treats Escape as "revert what I typed" and the popup stays.
    await render(<FilteringCombobox />);

    await userEvent.click(part("f-input"));
    await vi.waitFor(() => {
      expect(maybePart("f-popup")).not.toBeNull();
    });

    await userEvent.keyboard("{Escape}");

    // Polled rather than read once: unmount runs a commit after the keystroke.
    await expect.poll(() => maybePart("f-popup")).toBeNull();
    expect(maybePart("f-positioner")).toBeNull();
    expect(maybePart("f-item-Alpha")).toBeNull();
    expect(part("f-input").getAttribute("aria-expanded")).toBe("false");
  });

  it("lets a caller className override input group and item recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    await render(
      <Combobox.Root items={FONTS}>
        <Combobox.InputGroup data-testid="o-group" className="bg-accent">
          <Combobox.Input data-testid="o-input" />
        </Combobox.InputGroup>
        <Combobox.Portal>
          <Combobox.Positioner>
            <Combobox.Popup data-testid="o-popup" className="bg-accent">
              <Combobox.List>
                {(item: string) => (
                  <Combobox.Item
                    key={item}
                    value={item}
                    data-testid={`o-item-${item}`}
                    className="px-6"
                  >
                    {item}
                  </Combobox.Item>
                )}
              </Combobox.List>
            </Combobox.Popup>
          </Combobox.Positioner>
        </Combobox.Portal>
      </Combobox.Root>,
    );

    const group = part("o-group");
    expect(group.classList.contains("bg-accent")).toBe(true);
    expect(group.classList.contains("bg-background")).toBe(false);
    expect(group.classList.contains("border-input")).toBe(true);
    expect(group.classList.contains("rounded-md")).toBe(true);

    await userEvent.click(part("o-input"));
    await vi.waitFor(() => {
      expect(maybePart("o-popup")).not.toBeNull();
    });

    const popup = part("o-popup");
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);

    const item = part("o-item-Alpha");
    expect(item.classList.contains("px-6")).toBe(true);
    expect(item.classList.contains("px-3")).toBe(false);
    expect(item.classList.contains("py-1.5")).toBe(true);
    expect(item.classList.contains("data-[highlighted]:bg-accent")).toBe(true);
  });

  it("composes the trigger onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes. `nativeButton={false}` is required when the substitute is not
    // a <button>, or Base UI logs a dev-mode error.
    await render(
      <Combobox.Root defaultValue="alpha">
        <Combobox.Trigger data-testid="p-trigger" render={<div />} nativeButton={false}>
          <Combobox.Icon>▾</Combobox.Icon>
        </Combobox.Trigger>
      </Combobox.Root>,
    );

    const trigger = part("p-trigger");
    expect(trigger.tagName).toBe("DIV");
    expect(classSet(trigger)).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Combobox.Root defaultValue="alpha">
        <Combobox.InputGroup>
          <Combobox.Input data-testid="signup-font" aria-label="Font" name="font" />
        </Combobox.InputGroup>
      </Combobox.Root>,
    );

    const input = part("signup-font");
    expect(input.getAttribute("aria-label")).toBe("Font");
    expect(input.getAttribute("name")).toBe("font");
  });
});
