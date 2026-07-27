import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Autocomplete } from "./autocomplete";
import type { AutocompleteRootProps } from "./autocomplete";

/*
 * Wallow-m5aq.4.6 — Autocomplete stories, the visual half of the component's
 * spec (autocomplete.test.tsx holds the markup assertions a screenshot cannot
 * make). Rendered by `@storybook/addon-vitest` in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached.
 *
 * These are deliberately NOT a copy of combobox.stories.tsx. Twenty of the
 * twenty-three members here ARE the combobox's parts — same function objects,
 * same recipes — so re-photographing all of them would only duplicate coverage.
 * What these stories show instead is the three things an autocomplete does
 * differently, each of them measured against @base-ui/react 1.6.0:
 *
 *   - the value it commits is the TEXT in the input, not an item object, so
 *     `Autocomplete.Value` echoes what was typed and there is no tick and no
 *     chip anywhere in this file (Base UI publishes no `Label`, no
 *     `ItemIndicator` and no `Chips`/`Chip`/`ChipRemove` on this subpath);
 *   - `openOnInputClick` defaults to FALSE here where the combobox defaults it
 *     on, so the list opens on the trigger or on a keystroke, never on a bare
 *     click into the field;
 *   - `mode` chooses between filtering the list and inline-completing the input.
 *
 * The popup is PORTALLED to <body>, so play functions reach it through `screen`
 * rather than `canvas`.
 */

/** The suggestions every story offers. Three of them share the "B" prefix. */
const CITIES = ["Berlin", "Bern", "Bristol", "Cairo"];

interface CityAutocompleteProps {
  /** The text the input starts with. */
  readonly defaultValue?: string;
  /** Whether the whole autocomplete ignores interaction. */
  readonly disabled?: boolean;
  /** How typing relates to the list and to the input — see `AutocompleteRootProps`. */
  readonly mode?: AutocompleteRootProps["mode"];
  /** Called with the input's new TEXT, not with a selected item. */
  readonly onValueChange?: AutocompleteRootProps["onValueChange"];
}

/**
 * A complete, realistic autocomplete — the story subject.
 *
 * The trigger is not decoration: with `openOnInputClick` defaulting to false,
 * it and the keyboard are the only ways to open the list.
 */
function CityAutocomplete({
  defaultValue,
  disabled,
  mode,
  onValueChange,
}: CityAutocompleteProps): ReactElement {
  return (
    <Autocomplete.Root
      items={CITIES}
      defaultValue={defaultValue}
      disabled={disabled}
      mode={mode}
      onValueChange={onValueChange}
    >
      <Autocomplete.InputGroup data-testid="city-input-group">
        <Autocomplete.Input data-testid="city-input" placeholder="Search cities" />
        <Autocomplete.Trigger data-testid="city-trigger">
          <Autocomplete.Icon>▾</Autocomplete.Icon>
        </Autocomplete.Trigger>
      </Autocomplete.InputGroup>
      <span data-testid="city-value">
        <Autocomplete.Value />
      </span>
      <Autocomplete.Portal>
        <Autocomplete.Positioner data-testid="city-positioner">
          <Autocomplete.Popup data-testid="city-popup">
            <Autocomplete.Empty data-testid="city-empty">No cities found</Autocomplete.Empty>
            <Autocomplete.List data-testid="city-list">
              {(city: string) => (
                <Autocomplete.Item key={city} value={city} data-testid={`city-item-${city}`}>
                  {city}
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
 * The sectioned variant, written out by hand rather than filtered: it is the
 * combobox's `Group`/`GroupLabel`/`Separator`/`Status` parts, and it is here to
 * show that they need no autocomplete-specific styling to look right.
 */
function GroupedAutocomplete(): ReactElement {
  return (
    <Autocomplete.Root>
      <Autocomplete.InputGroup data-testid="grouped-input-group">
        <Autocomplete.Input data-testid="grouped-input" placeholder="Search cities" />
        <Autocomplete.Trigger data-testid="grouped-trigger">
          <Autocomplete.Icon>▾</Autocomplete.Icon>
        </Autocomplete.Trigger>
      </Autocomplete.InputGroup>
      <Autocomplete.Portal>
        <Autocomplete.Positioner>
          <Autocomplete.Popup data-testid="grouped-popup">
            <Autocomplete.Status data-testid="grouped-status">Recent searches</Autocomplete.Status>
            <Autocomplete.List>
              <Autocomplete.Group data-testid="grouped-germany">
                <Autocomplete.GroupLabel>Germany</Autocomplete.GroupLabel>
                <Autocomplete.Item value="Berlin" data-testid="grouped-item-Berlin">
                  Berlin
                </Autocomplete.Item>
              </Autocomplete.Group>
              <Autocomplete.Separator />
              <Autocomplete.Group data-testid="grouped-switzerland">
                <Autocomplete.GroupLabel>Switzerland</Autocomplete.GroupLabel>
                <Autocomplete.Item value="Bern" data-testid="grouped-item-Bern">
                  Bern
                </Autocomplete.Item>
              </Autocomplete.Group>
            </Autocomplete.List>
          </Autocomplete.Popup>
        </Autocomplete.Positioner>
      </Autocomplete.Portal>
    </Autocomplete.Root>
  );
}

const meta = {
  title: "Components/Autocomplete",
  component: CityAutocomplete,
  args: {
    onValueChange: fn(),
  },
} satisfies Meta<typeof CityAutocomplete>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The closed field with an empty query — the state a search box shows first. */
export const Default: Story = {};

/** A query already in the field, which for an autocomplete IS the value. */
export const WithQuery: Story = {
  args: { defaultValue: "Berlin" },
};

/** The whole autocomplete greyed out and out of the tab order. */
export const Disabled: Story = {
  args: { disabled: true, defaultValue: "Berlin" },
};

/** Suggestions under labelled headings, wearing the combobox's group parts. */
export const Grouped: Story = {
  render: () => <GroupedAutocomplete />,
};

/**
 * The acceptance-criteria journey: narrow the suggestions BY TYPING, then commit
 * the one that survived the filter.
 *
 * Two measured differences from the combobox's version of this story are
 * load-bearing here. Opening starts at the TRIGGER, because a click into the
 * input does not open the list. And the assertion at the end reads the INPUT's
 * value, because that is what an autocomplete commits — `Autocomplete.Value`
 * echoes the typed text where `Combobox.Value` would echo a selected item.
 */
export const FilterAndSelect: Story = {
  play: async ({ args, canvas }) => {
    const input = canvas.getByTestId("city-input");

    await userEvent.click(canvas.getByTestId("city-trigger"));

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("city-popup");
    await expect(popup).toBeVisible();
    await expect(screen.getByTestId("city-item-Cairo")).toBeVisible();

    await userEvent.type(input, "berl");

    // Filtering runs through React state, so the non-matching rows leave on a
    // later commit than the keystrokes.
    await waitFor(() => {
      expect(screen.queryByTestId("city-item-Bern")).toBeNull();
    });
    await expect(screen.getByTestId("city-item-Berlin")).toBeVisible();

    // A suggestion list is non-modal — no focus trap, no scroll lock, no
    // `pointer-events` blocker — so this click lands straight on the row.
    await userEvent.click(screen.getByTestId("city-item-Berlin"));

    await waitFor(async () => {
      await expect(input).toHaveValue("Berlin");
    });
    await expect(args.onValueChange).toHaveBeenCalledWith("Berlin", expect.anything());
    await expect(canvas.getByTestId("city-value")).toHaveTextContent("Berlin");
  },
};

/** A query nothing matches, which is what `Autocomplete.Empty` is for. */
export const NoMatches: Story = {
  play: async ({ canvas }) => {
    await userEvent.type(canvas.getByTestId("city-input"), "zzz");

    const empty = await screen.findByTestId("city-empty");
    await expect(empty).toBeVisible();
    await expect(empty).toHaveTextContent("No cities found");
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes autocomplete.test.tsx's class-set assertions and still
 * paints nothing.
 *
 * This is also the story that would catch the aliasing being quietly broken: it
 * asserts through the AUTOCOMPLETE namespace that the combobox's recipes have
 * actually painted these parts.
 */
export const PaintedByTheDesignTokens: Story = {
  play: async ({ canvas }) => {
    const group = canvas.getByTestId("city-input-group");

    // `border`/`border-input` on the input group.
    await expect(getComputedStyle(group).borderTopWidth).not.toBe("0px");
    await expect(getComputedStyle(group).borderTopStyle).toBe("solid");

    await userEvent.click(canvas.getByTestId("city-trigger"));
    const popup = await screen.findByTestId("city-popup");

    // `bg-popover` on the popup, against the unstyled transparent default.
    await expect(getComputedStyle(popup).backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    // `px-3` on the suggestion row.
    await expect(getComputedStyle(screen.getByTestId("city-item-Cairo")).paddingLeft).not.toBe(
      "0px",
    );
  },
};
