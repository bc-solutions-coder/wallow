import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Combobox } from "../combobox/combobox";
import * as comboboxStyles from "../combobox/combobox.styles";
import { Autocomplete } from "./autocomplete";
import * as autocompleteStyles from "./autocomplete.styles";

/*
 * Autocomplete behavioural spec (Wallow-m5aq.4.6), shaped after the
 * Wallow-m5aq.3.7 ContextMenu spec — because this component makes the same call
 * ContextMenu made: it REUSES its sibling's wrapped parts instead of re-wrapping
 * them.
 *
 * So this file is deliberately not a second copy of combobox.test.tsx. The
 * combobox spec owns the anatomy of the shared parts; this one owns:
 *
 *   1. the SHARING ITSELF — reference equality on every reused part and every
 *      aliased recipe, which is what stops the two components drifting apart;
 *   2. the THREE MEMBERS THAT ARE GENUINELY AUTOCOMPLETE CODE (`Root`, `Value`,
 *      `useFilter`) and the one runtime difference between them and the
 *      combobox's same-named members;
 *   3. the acceptance-criteria journey — filter by typing, then select the
 *      filtered result — driven through the autocomplete's own Root.
 *
 * THE ONE RUNTIME DIFFERENCE, measured against @base-ui/react 1.6.0 in this
 * browser (not guessed): `Autocomplete.Value` echoes the INPUT value while
 * `Combobox.Value` echoes the SELECTED value, and `Autocomplete.Root`'s
 * `defaultValue`/`value`/`onValueChange` are the input's text rather than an item.
 * An autocomplete commits what the user typed; a combobox commits what they
 * picked. Everything downstream follows from that — autocomplete items carry no
 * `aria-selected`, there is no `ItemIndicator` to tick and no `Chip` to remove.
 *
 * The other measured divergence worth stating: `Autocomplete.useFilter` is Base
 * UI's `useCoreFilter`, taking `{ locale }`, where `Combobox.useFilter` is
 * `useComboboxFilter`, taking `{ multiple, value, locale }`. Same
 * `contains`/`startsWith`/`endsWith` return shape, DIFFERENT options — so the two
 * are not interchangeable and this component does not share that one.
 */

/** Every namespace member Base UI's `@base-ui/react/autocomplete` publishes. */
const BASE_UI_MEMBER_NAMES = [
  "Arrow",
  "Backdrop",
  "Clear",
  "Collection",
  "Empty",
  "Group",
  "GroupLabel",
  "Icon",
  "Input",
  "InputGroup",
  "Item",
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

/**
 * Every member this component takes straight off the `Combobox` catalog
 * component — the seventeen visible parts plus the three renderless members Base
 * UI itself shares. Twenty of the twenty-three.
 */
const SHARED_MEMBER_NAMES = [
  "Arrow",
  "Backdrop",
  "Clear",
  "Collection",
  "Empty",
  "Group",
  "GroupLabel",
  "Icon",
  "Input",
  "InputGroup",
  "Item",
  "List",
  "Popup",
  "Portal",
  "Positioner",
  "Row",
  "Separator",
  "Status",
  "Trigger",
  "useFilteredItems",
] as const;

/**
 * Every recipe autocomplete.styles.ts aliases off combobox.styles.ts, paired
 * with the combobox recipe it must BE. Written out rather than looked up by a
 * computed key so both sides stay statically typed.
 */
const ALIASED_RECIPES = [
  ["Arrow", autocompleteStyles.autocompleteArrowRecipe, comboboxStyles.comboboxArrowRecipe],
  [
    "Backdrop",
    autocompleteStyles.autocompleteBackdropRecipe,
    comboboxStyles.comboboxBackdropRecipe,
  ],
  ["Clear", autocompleteStyles.autocompleteClearRecipe, comboboxStyles.comboboxClearRecipe],
  ["Empty", autocompleteStyles.autocompleteEmptyRecipe, comboboxStyles.comboboxEmptyRecipe],
  ["Group", autocompleteStyles.autocompleteGroupRecipe, comboboxStyles.comboboxGroupRecipe],
  [
    "GroupLabel",
    autocompleteStyles.autocompleteGroupLabelRecipe,
    comboboxStyles.comboboxGroupLabelRecipe,
  ],
  ["Icon", autocompleteStyles.autocompleteIconRecipe, comboboxStyles.comboboxIconRecipe],
  ["Input", autocompleteStyles.autocompleteInputRecipe, comboboxStyles.comboboxInputRecipe],
  [
    "InputGroup",
    autocompleteStyles.autocompleteInputGroupRecipe,
    comboboxStyles.comboboxInputGroupRecipe,
  ],
  ["Item", autocompleteStyles.autocompleteItemRecipe, comboboxStyles.comboboxItemRecipe],
  ["List", autocompleteStyles.autocompleteListRecipe, comboboxStyles.comboboxListRecipe],
  ["Popup", autocompleteStyles.autocompletePopupRecipe, comboboxStyles.comboboxPopupRecipe],
  [
    "Positioner",
    autocompleteStyles.autocompletePositionerRecipe,
    comboboxStyles.comboboxPositionerRecipe,
  ],
  ["Row", autocompleteStyles.autocompleteRowRecipe, comboboxStyles.comboboxRowRecipe],
  [
    "Separator",
    autocompleteStyles.autocompleteSeparatorRecipe,
    comboboxStyles.comboboxSeparatorRecipe,
  ],
  ["Status", autocompleteStyles.autocompleteStatusRecipe, comboboxStyles.comboboxStatusRecipe],
  ["Trigger", autocompleteStyles.autocompleteTriggerRecipe, comboboxStyles.comboboxTriggerRecipe],
] as const;

/**
 * A handful of the utilities the SHARED recipes must render, mirrored from
 * combobox.test.tsx — which stays the single source of truth for the full sets,
 * because these are literally the same recipe objects. They are asserted again
 * here so this file proves the shared recipes actually reach the autocomplete's
 * DOM, rather than trusting that reference equality implies application.
 */
const INPUT_GROUP_UTILITIES = ["rounded-md", "border", "border-input", "bg-background"];

/** As above, for the popup. */
const POPUP_UTILITIES = ["rounded-md", "border-border", "bg-popover", "shadow-md"];

/** As above, for an option row. */
const ITEM_UTILITIES = ["px-3", "py-1.5", "text-sm", "data-[highlighted]:bg-accent"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/**
 * The part carrying `data-testid`, searched across the whole document because
 * the popup half of an autocomplete is portalled out of the render container.
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

/** The three fonts the fixture suggests; two of them share a prefix. */
const FONTS = ["Alpha", "Alpine", "Beta"];

/**
 * The whole anatomy an autocomplete has. Note what is missing against the
 * combobox fixture and is missing from Base UI too: no `Label`, no
 * `ItemIndicator`, no `Chips`/`Chip`/`ChipRemove`.
 */
function FullAutocomplete(): ReactElement {
  return (
    <Autocomplete.Root items={FONTS}>
      <Autocomplete.InputGroup data-testid="a-input-group">
        <Autocomplete.Input data-testid="a-input" placeholder="Search fonts" />
        <Autocomplete.Trigger data-testid="a-trigger">
          <Autocomplete.Icon data-testid="a-icon">▾</Autocomplete.Icon>
        </Autocomplete.Trigger>
      </Autocomplete.InputGroup>
      <span data-testid="a-value">
        <Autocomplete.Value />
      </span>
      <Autocomplete.Portal>
        <Autocomplete.Positioner data-testid="a-positioner">
          <Autocomplete.Popup data-testid="a-popup">
            <Autocomplete.Empty data-testid="a-empty">No fonts found</Autocomplete.Empty>
            <Autocomplete.List data-testid="a-list">
              {(item: string) => (
                <Autocomplete.Item key={item} value={item} data-testid={`a-item-${item}`}>
                  {item}
                </Autocomplete.Item>
              )}
            </Autocomplete.List>
          </Autocomplete.Popup>
        </Autocomplete.Positioner>
      </Autocomplete.Portal>
    </Autocomplete.Root>
  );
}

/**
 * Renders the fixture and opens the popup by clicking the TRIGGER.
 *
 * Measured, and a real difference from the combobox: `openOnInputClick`
 * defaults to FALSE on an autocomplete root (the combobox root defaults it on),
 * so clicking into the input does NOT open the list. Suggestions appear when the
 * user types, or when a trigger asks for them — which is why this fixture has a
 * trigger at all.
 */
async function renderOpen(): Promise<void> {
  await render(<FullAutocomplete />);

  await userEvent.click(part("a-trigger"));

  await vi.waitFor(() => {
    expect(maybePart("a-popup")).not.toBeNull();
  });
}

describe("Autocomplete", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A member added
    // here that Base UI does not have (or a missing one) fails.
    expect(Object.keys(Autocomplete).toSorted()).toEqual(BASE_UI_MEMBER_NAMES);
  });

  it("reuses the Combobox component's own wrappers for every shared member", () => {
    // Reference equality, not "looks the same": `Autocomplete.Popup` IS
    // `Combobox.Popup`. This is the assertion that makes drift between the two
    // components impossible rather than merely unlikely.
    for (const name of SHARED_MEMBER_NAMES) {
      expect(Autocomplete[name], `Autocomplete.${name} is not Combobox.${name}`).toBe(
        Combobox[name],
      );
    }
  });

  it("brings its own Root, Value and useFilter rather than the combobox's", () => {
    // The three members Base UI itself does not share. `useFilter` is the
    // subtle one: same `contains`/`startsWith`/`endsWith` return shape, but
    // `useCoreFilter({ locale })` against `useComboboxFilter({ multiple, value,
    // locale })`, so sharing it would silently drop the combobox's options.
    expect(Autocomplete.Root).not.toBe(Combobox.Root);
    expect(Autocomplete.Value).not.toBe(Combobox.Value);
    expect(Autocomplete.useFilter).not.toBe(Combobox.useFilter);
  });

  it("aliases the combobox recipes instead of declaring its own", () => {
    // The styles half of the same anti-drift guarantee: a fork that rounds the
    // combobox popup rounds this one, because there is only one recipe object.
    for (const [name, autocompleteRecipe, comboboxRecipe] of ALIASED_RECIPES) {
      expect(autocompleteRecipe, `autocomplete${name}Recipe is missing`).toBeTypeOf("function");
      expect(autocompleteRecipe, `autocomplete${name}Recipe is not combobox${name}Recipe`).toBe(
        comboboxRecipe,
      );
    }
  });

  it("declares no recipe of its own beyond the aliases", () => {
    // Seventeen aliases and nothing else: an autocomplete has no label, no item
    // indicator and no chips, so a recipe here that the combobox does not have
    // would be a part that does not exist.
    const exported = Object.keys(autocompleteStyles).toSorted();
    const expected = ALIASED_RECIPES.map(([name]) => `autocomplete${name}Recipe`).toSorted();
    expect(exported).toEqual(expected);
  });

  it("renders the input group, input, trigger and icon with the shared recipes", async () => {
    await render(<FullAutocomplete />);

    const group = part("a-input-group");
    expect(group.getAttribute("role")).toBe("group");
    for (const utility of INPUT_GROUP_UTILITIES) {
      expect(group.classList.contains(utility), `input group is missing ${utility}`).toBe(true);
    }

    const input = part("a-input");
    expect(input.getAttribute("role")).toBe("combobox");
    expect(input.getAttribute("aria-autocomplete")).toBe("list");

    expect(part("a-trigger").tagName).toBe("BUTTON");
    expect(part("a-icon").getAttribute("aria-hidden")).toBe("true");
  });

  it("renders the popup and its items with the shared recipes", async () => {
    await renderOpen();

    const popup = part("a-popup");
    for (const utility of POPUP_UTILITIES) {
      expect(popup.classList.contains(utility), `popup is missing ${utility}`).toBe(true);
    }

    const item = part("a-item-Alpha");
    for (const utility of ITEM_UTILITIES) {
      expect(item.classList.contains(utility), `item is missing ${utility}`).toBe(true);
    }
  });

  it("dresses its parts identically to the combobox's, class for class", async () => {
    // The differential guard the reference-equality tests cannot give on their
    // own: the shared recipe has to actually REACH the DOM on both sides. If a
    // future change wraps one of these parts again, the two sets diverge here
    // even though `Autocomplete.Popup === Combobox.Popup` still holds elsewhere.
    await renderOpen();

    const autocompletePopupClasses = classSet(part("a-popup"));
    const autocompleteItemClasses = classSet(part("a-item-Alpha"));

    await render(
      <Combobox.Root items={FONTS}>
        <Combobox.InputGroup>
          <Combobox.Input data-testid="cx-input" />
        </Combobox.InputGroup>
        <Combobox.Portal>
          <Combobox.Positioner>
            <Combobox.Popup data-testid="cx-popup">
              <Combobox.List>
                {(item: string) => (
                  <Combobox.Item key={item} value={item} data-testid={`cx-item-${item}`}>
                    {item}
                  </Combobox.Item>
                )}
              </Combobox.List>
            </Combobox.Popup>
          </Combobox.Positioner>
        </Combobox.Portal>
      </Combobox.Root>,
    );
    await userEvent.click(part("cx-input"));
    await vi.waitFor(() => {
      expect(maybePart("cx-popup")).not.toBeNull();
    });

    expect(autocompletePopupClasses).toEqual(classSet(part("cx-popup")));
    expect(autocompleteItemClasses).toEqual(classSet(part("cx-item-Alpha")));
  });

  it("echoes the INPUT value through Value, where the combobox echoes the selection", async () => {
    // The discriminating spec for the pair. `Autocomplete.Root`'s `defaultValue`
    // is the input's text; `Combobox.Root`'s is a selected item.
    await render(
      <Autocomplete.Root items={FONTS} defaultValue="Alp">
        <Autocomplete.InputGroup>
          <Autocomplete.Input data-testid="v-input" />
        </Autocomplete.InputGroup>
        <span data-testid="v-value">
          <Autocomplete.Value />
        </span>
      </Autocomplete.Root>,
    );

    expect((part("v-input") as HTMLInputElement).value).toBe("Alp");
    expect(part("v-value").textContent).toBe("Alp");

    await userEvent.type(part("v-input"), "ine");

    await vi.waitFor(() => {
      expect(part("v-value").textContent).toBe("Alpine");
    });
  });

  it("leaves its items unselectable, with no aria-selected to announce", async () => {
    // Measured: an autocomplete has no selection model, so Base UI publishes no
    // `aria-selected` on its options — which is also why there is no
    // `ItemIndicator` in the namespace to tick one.
    await renderOpen();

    const item = part("a-item-Alpha");
    expect(item.getAttribute("role")).toBe("option");
    expect(item.hasAttribute("aria-selected")).toBe(false);
    expect(item.hasAttribute("data-selected")).toBe(false);
  });

  it("filters the list by typing and drops the non-matching items", async () => {
    await renderOpen();

    expect(maybePart("a-item-Alpha")).not.toBeNull();
    expect(maybePart("a-item-Alpine")).not.toBeNull();
    expect(maybePart("a-item-Beta")).not.toBeNull();

    await userEvent.type(part("a-input"), "alp");

    // Filtering runs through React state, so the non-matching rows leave a
    // commit after the keystrokes resolve — polled for, not read once.
    await expect.poll(() => maybePart("a-item-Beta")).toBeNull();
    expect(maybePart("a-item-Alpha")).not.toBeNull();
    expect(maybePart("a-item-Alpine")).not.toBeNull();
  });

  it("selects a filtered result by pointer click and fills the input with it", async () => {
    await renderOpen();

    await userEvent.type(part("a-input"), "alpi");
    await expect.poll(() => maybePart("a-item-Alpha")).toBeNull();

    // A real click, straight onto the item: like the combobox and unlike every
    // wave-2 overlay, this popup lays down no pointer-events blocker.
    await userEvent.click(part("a-item-Alpine"));

    await vi.waitFor(() => {
      expect((part("a-input") as HTMLInputElement).value).toBe("Alpine");
    });
    expect(part("a-value").textContent).toBe("Alpine");
    await expect.poll(() => maybePart("a-popup")).toBeNull();
  });

  it("selects a highlighted result with the keyboard", async () => {
    await renderOpen();

    await userEvent.keyboard("alp");
    await expect.poll(() => maybePart("a-item-Beta")).toBeNull();

    await userEvent.keyboard("{ArrowDown}");
    await vi.waitFor(() => {
      expect(part("a-item-Alpha").hasAttribute("data-highlighted")).toBe(true);
    });

    await userEvent.keyboard("{Enter}");

    await vi.waitFor(() => {
      expect((part("a-input") as HTMLInputElement).value).toBe("Alpha");
    });
  });

  it("marks the empty state and renders the empty message when nothing matches", async () => {
    await renderOpen();

    await userEvent.type(part("a-input"), "zzz");

    await expect.poll(() => maybePart("a-item-Alpha")).toBeNull();

    expect(part("a-popup").hasAttribute("data-empty")).toBe(true);
    expect(part("a-list").hasAttribute("data-empty")).toBe(true);
    expect(part("a-input-group").hasAttribute("data-list-empty")).toBe(true);
    expect(part("a-empty").textContent).toBe("No fonts found");
  });

  it("reports the typed value and the popup state to the caller's handlers", async () => {
    // The caller's handlers have to survive Base UI's own mergeProps. Note that
    // `onValueChange` here fires on TYPING, not on selection — the autocomplete's
    // value is the input's text.
    const onValueChange = vi.fn();
    const onOpenChange = vi.fn();
    await render(
      <Autocomplete.Root items={FONTS} onValueChange={onValueChange} onOpenChange={onOpenChange}>
        <Autocomplete.InputGroup>
          <Autocomplete.Input data-testid="h-input" />
        </Autocomplete.InputGroup>
        <Autocomplete.Portal>
          <Autocomplete.Positioner>
            <Autocomplete.Popup>
              <Autocomplete.List>
                {(item: string) => (
                  <Autocomplete.Item key={item} value={item} data-testid={`h-item-${item}`}>
                    {item}
                  </Autocomplete.Item>
                )}
              </Autocomplete.List>
            </Autocomplete.Popup>
          </Autocomplete.Positioner>
        </Autocomplete.Portal>
      </Autocomplete.Root>,
    );

    // Typing is what opens an autocomplete — this fixture has no trigger, and a
    // click into the input does nothing.
    await userEvent.type(part("h-input"), "bet");

    await vi.waitFor(() => {
      expect(onOpenChange).toHaveBeenCalled();
      expect(onValueChange).toHaveBeenCalled();
    });
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
    expect(onValueChange.mock.calls.at(-1)?.[0]).toBe("bet");
  });

  it("lets a caller className override a shared recipe utility", async () => {
    // The cn()/tailwind-merge proof, through the shared wrapper: the conflicting
    // recipe utility is REMOVED rather than appended-after, and untouched recipe
    // utilities survive.
    await render(
      <Autocomplete.Root items={FONTS}>
        <Autocomplete.InputGroup data-testid="o-group" className="bg-accent">
          <Autocomplete.Input data-testid="o-input" />
        </Autocomplete.InputGroup>
      </Autocomplete.Root>,
    );

    const group = part("o-group");
    expect(group.classList.contains("bg-accent")).toBe(true);
    expect(group.classList.contains("bg-background")).toBe(false);
    expect(group.classList.contains("border-input")).toBe(true);
    expect(group.classList.contains("rounded-md")).toBe(true);
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Autocomplete.Root items={FONTS}>
        <Autocomplete.InputGroup>
          <Autocomplete.Input data-testid="signup-font" aria-label="Font" name="font" />
        </Autocomplete.InputGroup>
      </Autocomplete.Root>,
    );

    const input = part("signup-font");
    expect(input.getAttribute("aria-label")).toBe("Font");
    expect(input.getAttribute("name")).toBe("font");
  });
});
