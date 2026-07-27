import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Combobox } from "./combobox";
import type { ComboboxRootProps } from "./combobox";

/*
 * Wallow-m5aq.4.6 — Combobox stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium
 * the `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while combobox.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * The popup is PORTALLED to <body>, which is outside the story canvas, so play
 * functions reach it through `screen` rather than `canvas`.
 *
 * Two fixture constraints, both measured rather than assumed:
 *   - Filtering only happens when `Root` is given `items`, so the filtering
 *     subject passes them and the static/grouped subject does not.
 *   - `Combobox.Label` labels the TRIGGER, so pairing one with a
 *     `Combobox.Input` makes Base UI log a dev-mode error. No story does.
 */

/** The suggestions the filtering subject offers. Only one of them matches "mono". */
const FONTS = ["Sans-serif", "Serif", "Monospace", "Cursive"];

interface FontComboboxProps {
  /** The initially selected font, or `null` for the empty field. */
  readonly defaultValue?: string | null;
  /** Whether the whole combobox ignores interaction. */
  readonly disabled?: boolean;
  /** Renders the reset button inside the field. */
  readonly withClear?: boolean;
  /** Called with the newly selected font. */
  readonly onValueChange?: ComboboxRootProps["onValueChange"];
}

/**
 * A complete, realistic combobox — the story subject. Stories drive the real
 * `Combobox` namespace through this so every part is exercised together rather
 * than one part at a time.
 *
 * `Clear` is rendered `keepMounted` so the reset button holds its place in the
 * field instead of making the row jump the moment something is selected; Base UI
 * fades it in through `data-visible`, which is why the recipe keys off that
 * attribute rather than off presence.
 */
function FontCombobox({
  defaultValue = null,
  disabled,
  withClear,
  onValueChange,
}: FontComboboxProps): ReactElement {
  return (
    <Combobox.Root
      items={FONTS}
      defaultValue={defaultValue}
      disabled={disabled}
      onValueChange={onValueChange}
    >
      <Combobox.InputGroup data-testid="font-input-group">
        <Combobox.Input data-testid="font-input" placeholder="Search fonts" />
        {withClear ? (
          <Combobox.Clear data-testid="font-clear" keepMounted>
            ×
          </Combobox.Clear>
        ) : null}
        <Combobox.Trigger data-testid="font-trigger">
          <Combobox.Icon>▾</Combobox.Icon>
        </Combobox.Trigger>
      </Combobox.InputGroup>
      <span data-testid="font-value">
        <Combobox.Value />
      </span>
      <Combobox.Portal>
        <Combobox.Positioner data-testid="font-positioner">
          <Combobox.Popup data-testid="font-popup">
            <Combobox.Empty data-testid="font-empty">No fonts found</Combobox.Empty>
            <Combobox.List data-testid="font-list">
              {(font: string) => (
                <Combobox.Item key={font} value={font} data-testid={`font-item-${font}`}>
                  {font}
                  <Combobox.ItemIndicator>✓</Combobox.ItemIndicator>
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
 * The sectioned variant. `Root` gets no `items`, so this list is static: groups
 * are written out by hand, which is also what a fork does when its sections come
 * from the server rather than from a flat array.
 */
function GroupedCombobox(): ReactElement {
  return (
    <Combobox.Root defaultValue="Serif">
      <Combobox.InputGroup data-testid="grouped-input-group">
        <Combobox.Input data-testid="grouped-input" placeholder="Search fonts" />
        <Combobox.Trigger data-testid="grouped-trigger">
          <Combobox.Icon>▾</Combobox.Icon>
        </Combobox.Trigger>
      </Combobox.InputGroup>
      <Combobox.Portal>
        <Combobox.Positioner>
          <Combobox.Popup data-testid="grouped-popup">
            <Combobox.Status data-testid="grouped-status">4 fonts</Combobox.Status>
            <Combobox.List>
              <Combobox.Group data-testid="grouped-serifs">
                <Combobox.GroupLabel>With serifs</Combobox.GroupLabel>
                <Combobox.Item value="Serif" data-testid="grouped-item-Serif">
                  Serif
                  <Combobox.ItemIndicator>✓</Combobox.ItemIndicator>
                </Combobox.Item>
              </Combobox.Group>
              <Combobox.Separator />
              <Combobox.Group data-testid="grouped-rest">
                <Combobox.GroupLabel>Everything else</Combobox.GroupLabel>
                <Combobox.Item value="Sans-serif" data-testid="grouped-item-Sans-serif">
                  Sans-serif
                  <Combobox.ItemIndicator>✓</Combobox.ItemIndicator>
                </Combobox.Item>
                <Combobox.Item value="Monospace" disabled data-testid="grouped-item-Monospace">
                  Monospace
                </Combobox.Item>
              </Combobox.Group>
            </Combobox.List>
          </Combobox.Popup>
        </Combobox.Positioner>
      </Combobox.Portal>
    </Combobox.Root>
  );
}

/**
 * The multi-select variant, where the selection lives in the field as removable
 * pills. `Value`'s render-prop child receives the whole selected array here
 * rather than a single item, which is what the chips map over.
 */
function TagCombobox(): ReactElement {
  return (
    <Combobox.Root multiple defaultValue={["Serif", "Monospace"]} items={FONTS}>
      <Combobox.Chips data-testid="tag-chips">
        <Combobox.Value>
          {(selected: string[]) =>
            selected.map((font) => (
              <Combobox.Chip key={font} data-testid={`tag-chip-${font}`}>
                {font}
                <Combobox.ChipRemove data-testid={`tag-chip-remove-${font}`}>×</Combobox.ChipRemove>
              </Combobox.Chip>
            ))
          }
        </Combobox.Value>
        <Combobox.Input data-testid="tag-input" placeholder="Add a font" />
      </Combobox.Chips>
      <Combobox.Portal>
        <Combobox.Positioner>
          <Combobox.Popup data-testid="tag-popup">
            <Combobox.List>
              {(font: string) => (
                <Combobox.Item key={font} value={font} data-testid={`tag-item-${font}`}>
                  {font}
                </Combobox.Item>
              )}
            </Combobox.List>
          </Combobox.Popup>
        </Combobox.Positioner>
      </Combobox.Portal>
    </Combobox.Root>
  );
}

const meta = {
  title: "Components/Combobox",
  component: FontCombobox,
  args: {
    onValueChange: fn(),
  },
} satisfies Meta<typeof FontCombobox>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The closed field with nothing chosen — the state a form shows first. */
export const Default: Story = {};

/** A font already committed, so the input carries its label. */
export const WithSelection: Story = {
  args: { defaultValue: "Monospace" },
};

/** The reset button inside the field, fading in once there is something to clear. */
export const WithClear: Story = {
  args: { defaultValue: "Monospace", withClear: true },
};

/** The whole combobox greyed out and out of the tab order. */
export const Disabled: Story = {
  args: { disabled: true, defaultValue: "Serif" },
};

/** Suggestions under labelled headings, separated by a rule. */
export const Grouped: Story = {
  render: () => <GroupedCombobox />,
};

/** Several fonts committed at once, each as a removable pill in the field. */
export const MultiSelect: Story = {
  render: () => <TagCombobox />,
};

/**
 * The acceptance-criteria journey: narrow the list BY TYPING, then commit the
 * result that survived the filter.
 *
 * A combobox popup is non-modal — no focus trap, no scroll lock, no
 * `pointer-events` blocker over the page — so the click on the filtered row is a
 * plain click straight onto the item, unlike every overlay in the Wave-2
 * catalog.
 */
export const FilterAndSelect: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByTestId("font-input"));

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("font-popup");
    await expect(popup).toBeVisible();
    await expect(screen.getByTestId("font-item-Serif")).toBeVisible();

    await userEvent.type(canvas.getByTestId("font-input"), "mono");

    // Filtering runs through React state, so the non-matching rows leave on a
    // later commit than the keystrokes.
    await waitFor(() => {
      expect(screen.queryByTestId("font-item-Serif")).toBeNull();
    });
    await expect(screen.getByTestId("font-item-Monospace")).toBeVisible();

    await userEvent.click(screen.getByTestId("font-item-Monospace"));

    await expect(args.onValueChange).toHaveBeenCalledWith("Monospace", expect.anything());
    await waitFor(async () => {
      await expect(canvas.getByTestId("font-value")).toHaveTextContent("Monospace");
    });
  },
};

/** Typing something nothing matches, which is what `Combobox.Empty` is for. */
export const NoMatches: Story = {
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("font-input"));
    await userEvent.type(canvas.getByTestId("font-input"), "zzz");

    const empty = await screen.findByTestId("font-empty");
    await expect(empty).toBeVisible();
    await expect(empty).toHaveTextContent("No fonts found");
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes combobox.test.tsx's class-set assertions and still paints
 * nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  play: async ({ canvas }) => {
    const group = canvas.getByTestId("font-input-group");

    // `border`/`border-input` on the input group — the field's outline lives
    // there rather than on the input, so the chevron sits inside it.
    await expect(getComputedStyle(group).borderTopWidth).not.toBe("0px");
    await expect(getComputedStyle(group).borderTopStyle).toBe("solid");

    await userEvent.click(canvas.getByTestId("font-input"));
    const popup = await screen.findByTestId("font-popup");

    // `bg-popover` on the popup, against the unstyled transparent default.
    await expect(getComputedStyle(popup).backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    // `px-3` on the item.
    await expect(getComputedStyle(screen.getByTestId("font-item-Serif")).paddingLeft).not.toBe(
      "0px",
    );
  },
};
