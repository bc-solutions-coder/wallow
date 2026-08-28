import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Select } from "./select";

/*
 * Select behavioural spec (Wallow-m5aq.2.8), shaped after the Wallow-m5aq.2.1
 * Button exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `selectTriggerRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into select.styles.ts.
 *   4. Stories carry the visual coverage (see select.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <div id="…-label">                                  <- Select.Label
 *   <button role="combobox" aria-haspopup="listbox">    <- Select.Trigger
 *     <span>alpha</span>                                <- Select.Value
 *     <span aria-hidden="true"><svg/></span>            <- Select.Icon
 *   <input aria-hidden tabindex="-1" name value>        <- SIBLING of the trigger
 *
 *   …and, only while open, portalled onto <body>:
 *   <div role="presentation" data-open data-side data-align>  <- Select.Positioner
 *     <div role="presentation" data-open tabindex="-1">       <- Select.Popup
 *       <div role="listbox" class="base-ui-disable-scrollbar">  <- Select.List
 *         <div role="group">                                  <- Select.Group
 *           <div id="…">                                      <- Select.GroupLabel
 *           <div role="option" data-selected data-highlighted> <- Select.Item
 *             <div>Alpha</div>                                <- Select.ItemText
 *             <span data-selected aria-hidden="true">         <- Select.ItemIndicator
 *
 * Five consequences worth knowing before editing this file:
 *   - the popup is PORTALLED to <body>, so every open-state query goes through
 *     `document.body`, never through `render`'s `container`;
 *   - nothing under Select.Portal exists in the DOM at all while the select is
 *     closed — these are not hidden elements, they are absent ones;
 *   - `Select.List` is the one part whose rendered class set is the recipe PLUS
 *     a Base UI class (`base-ui-disable-scrollbar`), which is why LIST_CLASSES
 *     is spread with that extra name at the assertion site;
 *   - `Select.ItemIndicator` is unmounted while its item is unselected, and the
 *     scroll arrows are unmounted while the list does not overflow, so both are
 *     rendered with `keepMounted` where their styling is under test;
 *   - `Select.Value` renders the item's raw VALUE (`"beta"`), not its
 *     `ItemText`, unless `Select.Root` is given an `items` map.
 */

/** Utilities `Select.Label` must render. */
const LABEL_CLASSES = ["text-sm", "font-medium", "text-foreground"];

/** Utilities `Select.Trigger` must render. */
const TRIGGER_CLASSES = [
  "inline-flex",
  "w-full",
  "items-center",
  "justify-between",
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

/** Utilities `Select.Value` must render. */
const VALUE_CLASSES = ["truncate", "text-left", "data-[placeholder]:text-muted-foreground"];

/** Utilities `Select.Icon` must render. */
const ICON_CLASSES = [
  "flex",
  "size-4",
  "shrink-0",
  "items-center",
  "justify-center",
  "text-muted-foreground",
  "transition-transform",
  "data-[popup-open]:rotate-180",
];

/** Utilities `Select.Backdrop` must render. */
const BACKDROP_CLASSES = ["fixed", "inset-0"];

/**
 * Utilities `Select.Positioner` must render. Base UI owns this element's
 * `position`/`transform` inline styles, so the recipe may only add stacking and
 * focus concerns — layout utilities here would fight the positioning engine.
 */
const POSITIONER_CLASSES = ["z-50", "outline-none"];

/**
 * Utilities `Select.Popup` must render.
 *
 * `min-w-[var(--anchor-width)]` is what stops the popup shrinking to its longest
 * option and rendering narrower than the control it belongs to. Base UI
 * publishes the trigger's measured width as `--anchor-width` on
 * `Select.Positioner`, the popup's ancestor, so the `var()` resolves through
 * inheritance. It is a MINIMUM, not a fixed width: an option longer than the
 * trigger must still be allowed to widen the popup. Combobox already carries the
 * same utility on its own popup recipe.
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

/** Utilities `Select.List` must render, on top of Base UI's own scrollbar class. */
const LIST_CLASSES = ["max-h-64", "overflow-y-auto", "outline-none"];

/** The class Base UI itself puts on `Select.List`, which the recipe never owns. */
const BASE_UI_LIST_CLASS = "base-ui-disable-scrollbar";

/** Utilities `Select.Item` must render. */
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

/** Utilities `Select.ItemText` must render. */
const ITEM_TEXT_CLASSES = ["flex-1", "truncate"];

/** Utilities `Select.ItemIndicator` must render. */
const ITEM_INDICATOR_CLASSES = [
  "flex",
  "size-4",
  "shrink-0",
  "items-center",
  "justify-center",
  "text-primary",
];

/** Utilities `Select.Arrow` must render. */
const ARROW_CLASSES = ["flex", "text-popover-foreground"];

/**
 * Utilities BOTH scroll arrows must render. One recipe drives
 * `Select.ScrollUpArrow` and `Select.ScrollDownArrow`: they are the same band,
 * and Base UI already tells them apart with `data-direction`.
 */
const SCROLL_ARROW_CLASSES = [
  "flex",
  "h-6",
  "cursor-default",
  "items-center",
  "justify-center",
  "bg-popover",
  "text-muted-foreground",
];

/** Utilities `Select.Group` must render. */
const GROUP_CLASSES = ["py-1"];

/** Utilities `Select.GroupLabel` must render. */
const GROUP_LABEL_CLASSES = ["px-3", "py-1.5", "text-xs", "font-medium", "text-muted-foreground"];

/** Utilities `Select.Separator` must render. */
const SEPARATOR_CLASSES = ["my-1", "h-px", "bg-border"];

/** Every part name Base UI's `@base-ui/react/select` publishes. */
const BASE_UI_PART_NAMES = [
  "Arrow",
  "Backdrop",
  "Group",
  "GroupLabel",
  "Icon",
  "Item",
  "ItemIndicator",
  "ItemText",
  "Label",
  "List",
  "Popup",
  "Portal",
  "Positioner",
  "Root",
  "ScrollDownArrow",
  "ScrollUpArrow",
  "Separator",
  "Trigger",
  "Value",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/**
 * The part carrying `data-testid`, searched across the whole document because
 * the popup half of a select is portalled out of the render container.
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
 * Every part at once, so one fixture can carry the whole anatomy. `gamma` is
 * disabled and both scroll arrows are `keepMounted` (they only appear on their
 * own once the list overflows, which this three-item list never does).
 */
function FullSelect(): ReactElement {
  return (
    <Select.Root defaultValue="alpha">
      <Select.Label data-testid="s-label">Font</Select.Label>
      <Select.Trigger data-testid="s-trigger">
        <Select.Value data-testid="s-value" />
        <Select.Icon data-testid="s-icon" />
      </Select.Trigger>
      <Select.Portal>
        <Select.Backdrop data-testid="s-backdrop" />
        <Select.Positioner data-testid="s-positioner">
          <Select.ScrollUpArrow data-testid="s-scroll-up" keepMounted />
          <Select.Popup data-testid="s-popup">
            <Select.Arrow data-testid="s-arrow" />
            <Select.List data-testid="s-list">
              <Select.Group data-testid="s-group">
                <Select.GroupLabel data-testid="s-group-label">Serif</Select.GroupLabel>
                <Select.Item value="alpha" data-testid="s-item-alpha">
                  <Select.ItemText data-testid="s-item-alpha-text">Alpha</Select.ItemText>
                  <Select.ItemIndicator data-testid="s-item-alpha-indicator">
                    ✓
                  </Select.ItemIndicator>
                </Select.Item>
                <Select.Separator data-testid="s-separator" />
                <Select.Item value="beta" data-testid="s-item-beta">
                  <Select.ItemText>Beta</Select.ItemText>
                  <Select.ItemIndicator data-testid="s-item-beta-indicator">✓</Select.ItemIndicator>
                </Select.Item>
                <Select.Item value="gamma" disabled data-testid="s-item-gamma">
                  <Select.ItemText>Gamma</Select.ItemText>
                </Select.Item>
              </Select.Group>
            </Select.List>
          </Select.Popup>
          <Select.ScrollDownArrow data-testid="s-scroll-down" keepMounted />
        </Select.Positioner>
      </Select.Portal>
    </Select.Root>
  );
}

/**
 * Renders the full fixture and opens the popup by clicking the trigger, then
 * waits for the list to actually own focus.
 *
 * That last step is load-bearing for every keyboard test here. `aria-expanded`
 * flips as soon as the click resolves, but Base UI moves focus onto the
 * highlighted item a commit or two later, and a key sent during that window goes
 * to the body and is simply lost — an intermittent failure that only showed up
 * under full-suite load, never when this file ran alone.
 */
async function renderOpen(): Promise<void> {
  await render(<FullSelect />);

  await userEvent.click(part("s-trigger"));
  expect(part("s-trigger").getAttribute("aria-expanded")).toBe("true");

  await vi.waitFor(() => {
    expect(part("s-list").contains(document.activeElement)).toBe(true);
  });
}

describe("Select", () => {
  it("exposes exactly Base UI's part names on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A part added
    // here that Base UI does not have (or a missing one) fails.
    expect(Object.keys(Select).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("renders the trigger as a combobox button with its recipe", async () => {
    await render(<FullSelect />);

    const trigger = part("s-trigger");
    expect(trigger.tagName).toBe("BUTTON");
    expect(trigger.getAttribute("role")).toBe("combobox");
    expect(trigger.getAttribute("aria-haspopup")).toBe("listbox");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    expect(classSet(trigger)).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("renders the value and icon inside the trigger with their recipes", async () => {
    await render(<FullSelect />);

    // `Select.Value` shows the raw VALUE, not the matching ItemText, because
    // `Select.Root` was not given an `items` map.
    expect(part("s-value").textContent).toBe("alpha");
    expect(classSet(part("s-value"))).toEqual(VALUE_CLASSES.toSorted());

    expect(part("s-icon").getAttribute("aria-hidden")).toBe("true");
    expect(classSet(part("s-icon"))).toEqual(ICON_CLASSES.toSorted());
    expect(part("s-icon").parentElement).toBe(part("s-trigger"));
  });

  it("fills an empty Select.Icon with a default inline-svg chevron", async () => {
    // The catalog owes callers a chevron they do not have to supply. It has to
    // be INLINE SVG: `ui` ships no icon library and must not gain one, and a
    // text glyph (the "▾" every call site used to pass) sits off the baseline of
    // the size-4 icon box and renders differently on every platform.
    await render(<FullSelect />);

    const icon = part("s-icon");
    const chevron = icon.querySelector("svg");
    expect(chevron, "Select.Icon rendered no default chevron").not.toBeNull();

    // Nothing but the svg: a glyph left alongside it would still paint the old
    // off-baseline arrow.
    expect(icon.textContent).toBe("");
    expect(icon.children.length).toBe(1);
  });

  it("lets a caller's children replace the default chevron", async () => {
    // The default is a default, not a lock-in — a fork substituting its own icon
    // must still get exactly what it passed and no svg beside it.
    await render(
      <Select.Root defaultValue="alpha">
        <Select.Trigger>
          <Select.Value />
          <Select.Icon data-testid="ov-icon">▾</Select.Icon>
        </Select.Trigger>
      </Select.Root>,
    );

    const icon = part("ov-icon");
    expect(icon.textContent).toBe("▾");
    expect(icon.querySelector("svg")).toBeNull();
  });

  it("renders the label with its recipe and names the trigger with it", async () => {
    await render(<FullSelect />);

    const label = part("s-label");
    expect(classSet(label)).toEqual(LABEL_CLASSES.toSorted());
    expect(part("s-trigger").getAttribute("aria-labelledby")).toBe(label.id);
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the popup opens.
    await render(<FullSelect />);

    expect(maybePart("s-positioner")).toBeNull();
    expect(maybePart("s-popup")).toBeNull();
    expect(maybePart("s-list")).toBeNull();
    expect(maybePart("s-backdrop")).toBeNull();
    expect(maybePart("s-item-alpha")).toBeNull();
  });

  it("marks the trigger and value data-placeholder while nothing is selected", async () => {
    await render(
      <Select.Root>
        <Select.Trigger data-testid="p-trigger">
          <Select.Value data-testid="p-value" placeholder="Pick a font" />
        </Select.Trigger>
      </Select.Root>,
    );

    expect(part("p-trigger").hasAttribute("data-placeholder")).toBe(true);
    expect(part("p-value").hasAttribute("data-placeholder")).toBe(true);
    expect(part("p-value").textContent).toBe("Pick a font");
  });

  it("exposes a disabled select as data-disabled and drops it from the tab order", async () => {
    await render(
      <Select.Root disabled>
        <Select.Trigger data-testid="d-trigger">
          <Select.Value />
        </Select.Trigger>
      </Select.Root>,
    );

    const trigger = part("d-trigger");
    expect(trigger.hasAttribute("data-disabled")).toBe(true);
    expect(trigger.hasAttribute("disabled")).toBe(true);
    expect(trigger.getAttribute("tabindex")).toBe("-1");
  });

  it("submits its name, value and required flag through the hidden input", async () => {
    await render(
      <Select.Root name="font" defaultValue="alpha" required>
        <Select.Trigger data-testid="n-trigger">
          <Select.Value />
        </Select.Trigger>
      </Select.Root>,
    );

    const hidden = document.body.querySelector("input");
    expect(hidden).not.toBeNull();
    expect(hidden?.name).toBe("font");
    expect(hidden?.value).toBe("alpha");
    expect(hidden?.required).toBe(true);
    expect(part("n-trigger").getAttribute("aria-required")).toBe("true");
  });

  it("marks the trigger and icon data-popup-open once the popup opens", async () => {
    await renderOpen();

    expect(part("s-trigger").hasAttribute("data-popup-open")).toBe(true);
    expect(part("s-icon").hasAttribute("data-popup-open")).toBe(true);
  });

  it("renders the positioner, popup and backdrop with their recipes", async () => {
    await renderOpen();

    expect(classSet(part("s-positioner"))).toEqual(POSITIONER_CLASSES.toSorted());
    expect(classSet(part("s-popup"))).toEqual(POPUP_CLASSES.toSorted());
    expect(classSet(part("s-backdrop"))).toEqual(BACKDROP_CLASSES.toSorted());
  });

  it("renders the list with its recipe alongside Base UI's scrollbar class", async () => {
    await renderOpen();

    const list = part("s-list");
    expect(list.getAttribute("role")).toBe("listbox");
    expect(classSet(list)).toEqual([...LIST_CLASSES, BASE_UI_LIST_CLASS].toSorted());
  });

  it("renders the arrow and both scroll arrows with their recipes", async () => {
    await renderOpen();

    expect(classSet(part("s-arrow"))).toEqual(ARROW_CLASSES.toSorted());
    expect(classSet(part("s-scroll-up"))).toEqual(SCROLL_ARROW_CLASSES.toSorted());
    expect(classSet(part("s-scroll-down"))).toEqual(SCROLL_ARROW_CLASSES.toSorted());
    // One recipe, two parts: Base UI is what tells them apart.
    expect(part("s-scroll-up").getAttribute("data-direction")).toBe("up");
    expect(part("s-scroll-down").getAttribute("data-direction")).toBe("down");
  });

  it("renders the group, group label and separator with their recipes", async () => {
    await renderOpen();

    expect(part("s-group").getAttribute("role")).toBe("group");
    expect(classSet(part("s-group"))).toEqual(GROUP_CLASSES.toSorted());

    const groupLabel = part("s-group-label");
    expect(classSet(groupLabel)).toEqual(GROUP_LABEL_CLASSES.toSorted());
    expect(part("s-group").getAttribute("aria-labelledby")).toBe(groupLabel.id);

    expect(part("s-separator").getAttribute("role")).toBe("separator");
    expect(classSet(part("s-separator"))).toEqual(SEPARATOR_CLASSES.toSorted());
  });

  it("renders the selected item with data-selected and a mounted indicator", async () => {
    await renderOpen();

    const item = part("s-item-alpha");
    expect(item.getAttribute("role")).toBe("option");
    expect(item.hasAttribute("data-selected")).toBe(true);
    expect(item.getAttribute("aria-selected")).toBe("true");
    expect(classSet(item)).toEqual(ITEM_CLASSES.toSorted());

    expect(classSet(part("s-item-alpha-text"))).toEqual(ITEM_TEXT_CLASSES.toSorted());
    expect(classSet(part("s-item-alpha-indicator"))).toEqual(ITEM_INDICATOR_CLASSES.toSorted());
  });

  it("leaves an unselected item's indicator unmounted", async () => {
    // Base UI's default (`keepMounted` is false), so the tick cannot be styled
    // into invisibility — it genuinely is not in the DOM.
    await renderOpen();

    expect(part("s-item-beta").getAttribute("aria-selected")).toBe("false");
    expect(part("s-item-beta").hasAttribute("data-selected")).toBe(false);
    expect(maybePart("s-item-beta-indicator")).toBeNull();
  });

  it("exposes a disabled item as data-disabled and aria-disabled", async () => {
    await renderOpen();

    const gamma = part("s-item-gamma");
    expect(gamma.hasAttribute("data-disabled")).toBe(true);
    expect(gamma.getAttribute("aria-disabled")).toBe("true");
    expect(gamma.getAttribute("tabindex")).toBe("-1");
  });

  it("moves data-highlighted with the arrow keys, disabled items included", async () => {
    await renderOpen();

    expect(part("s-item-alpha").hasAttribute("data-highlighted")).toBe(true);

    // The highlight moves through React state, so it lands a commit after the
    // keystroke resolves — waited for, not read once.
    await userEvent.keyboard("{ArrowDown}");

    await vi.waitFor(() => {
      expect(part("s-item-beta").hasAttribute("data-highlighted")).toBe(true);
      expect(part("s-item-alpha").hasAttribute("data-highlighted")).toBe(false);
    });

    // Measured, not assumed: Base UI's composite navigation still HIGHLIGHTS a
    // disabled item rather than skipping past it (the item stays unselectable —
    // that is what `data-disabled`/`aria-disabled` are for). The recipe's
    // `data-[highlighted]:` and `data-[disabled]:` modifiers therefore have to
    // survive being on the same element at the same time.
    await userEvent.keyboard("{ArrowDown}");

    await vi.waitFor(() => {
      expect(part("s-item-gamma").hasAttribute("data-highlighted")).toBe(true);
      expect(part("s-item-gamma").hasAttribute("data-disabled")).toBe(true);
      expect(part("s-item-beta").hasAttribute("data-highlighted")).toBe(false);
    });
  });

  it("selects an item by pointer click and closes the popup", async () => {
    await renderOpen();

    await userEvent.click(part("s-item-beta"));

    expect(part("s-value").textContent).toBe("beta");
    expect(part("s-trigger").getAttribute("aria-expanded")).toBe("false");
  });

  it("selects an item by keyboard and reports it to onValueChange", async () => {
    // The caller's handler has to survive Base UI's own mergeProps.
    const onValueChange = vi.fn();
    await render(
      <Select.Root defaultValue="alpha" onValueChange={onValueChange}>
        <Select.Trigger data-testid="k-trigger">
          <Select.Value data-testid="k-value" />
        </Select.Trigger>
        <Select.Portal>
          <Select.Positioner>
            <Select.Popup>
              <Select.List>
                <Select.Item value="alpha">
                  <Select.ItemText>Alpha</Select.ItemText>
                </Select.Item>
                <Select.Item value="beta">
                  <Select.ItemText>Beta</Select.ItemText>
                </Select.Item>
              </Select.List>
            </Select.Popup>
          </Select.Positioner>
        </Select.Portal>
      </Select.Root>,
    );

    // A pointer parked (by an earlier file) where the popup mounts — and a select
    // popup aligns OVER its trigger — would hover-steal the highlight mid-sequence.
    await userEvent.unhover(part("k-trigger"));
    part("k-trigger").focus();
    await userEvent.keyboard("{Enter}");
    await userEvent.keyboard("{ArrowDown}");
    await userEvent.keyboard("{Enter}");

    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange.mock.calls[0]?.[0]).toBe("beta");
    expect(part("k-value").textContent).toBe("beta");
  });

  it("reports popup open state to onOpenChange", async () => {
    const onOpenChange = vi.fn();
    await render(
      <Select.Root defaultValue="alpha" onOpenChange={onOpenChange}>
        <Select.Trigger data-testid="o-trigger">
          <Select.Value />
        </Select.Trigger>
        <Select.Portal>
          <Select.Positioner>
            <Select.Popup>
              <Select.List>
                <Select.Item value="alpha">
                  <Select.ItemText>Alpha</Select.ItemText>
                </Select.Item>
              </Select.List>
            </Select.Popup>
          </Select.Positioner>
        </Select.Portal>
      </Select.Root>,
    );

    await userEvent.click(part("o-trigger"));

    expect(onOpenChange).toHaveBeenCalledTimes(1);
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("lets a caller className override a trigger recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    await render(
      <Select.Root defaultValue="alpha">
        <Select.Trigger data-testid="c-trigger" className="bg-accent">
          <Select.Value />
        </Select.Trigger>
      </Select.Root>,
    );

    const trigger = part("c-trigger");
    expect(trigger.classList.contains("bg-accent")).toBe(true);
    expect(trigger.classList.contains("bg-background")).toBe(false);
    expect(trigger.classList.contains("border-input")).toBe(true);
    expect(trigger.classList.contains("rounded-md")).toBe(true);
  });

  it("lets a caller className override popup and item recipe utilities", async () => {
    await render(
      <Select.Root defaultValue="alpha">
        <Select.Trigger data-testid="c-trigger">
          <Select.Value />
        </Select.Trigger>
        <Select.Portal>
          <Select.Positioner>
            <Select.Popup data-testid="c-popup" className="bg-accent">
              <Select.List>
                <Select.Item value="alpha" data-testid="c-item" className="px-6">
                  <Select.ItemText>Alpha</Select.ItemText>
                </Select.Item>
              </Select.List>
            </Select.Popup>
          </Select.Positioner>
        </Select.Portal>
      </Select.Root>,
    );
    await userEvent.click(part("c-trigger"));

    const popup = part("c-popup");
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);

    const item = part("c-item");
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
      <Select.Root defaultValue="alpha">
        <Select.Trigger data-testid="r-trigger" render={<div />} nativeButton={false}>
          <Select.Value />
        </Select.Trigger>
      </Select.Root>,
    );

    const trigger = part("r-trigger");
    expect(trigger.tagName).toBe("DIV");
    expect(classSet(trigger)).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Select.Root defaultValue="alpha">
        <Select.Trigger data-testid="signup-font" aria-label="Font">
          <Select.Value />
        </Select.Trigger>
      </Select.Root>,
    );

    expect(part("signup-font").getAttribute("aria-label")).toBe("Font");
  });
});
