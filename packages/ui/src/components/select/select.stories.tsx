import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement, ReactNode } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Select } from "./select";

/*
 * Wallow-m5aq.2.8 — Select stories. `@storybook/addon-vitest` turns every export
 * below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while select.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * The popup is PORTALLED to <body>, which is outside the story canvas, so the
 * play function reaches it through `screen` rather than `canvas`.
 */

/** The options every story shows. */
const FONTS = [
  { value: "sans", label: "Sans-serif" },
  { value: "serif", label: "Serif" },
  { value: "mono", label: "Monospace" },
];

interface FontSelectProps {
  /** The initially selected font, or `null` for the placeholder state. */
  readonly defaultValue?: string | null;
  /** Whether the whole select ignores interaction. */
  readonly disabled?: boolean;
  /** Shown in the trigger while nothing is selected. */
  readonly placeholder?: ReactNode;
  /** Wraps the options in a labelled group with a separator above them. */
  readonly grouped?: boolean;
  /** Called with the newly selected value. */
  readonly onValueChange?: (value: string | null) => void;
}

/**
 * A complete, realistic select — the story subject. Stories drive the real
 * `Select` namespace through this so every part is exercised together rather
 * than one part at a time.
 */
function FontSelect({
  defaultValue = "sans",
  disabled,
  placeholder,
  grouped,
  onValueChange,
}: FontSelectProps): ReactElement {
  const options = FONTS.map((font) => (
    <Select.Item key={font.value} value={font.value} data-testid={`font-${font.value}`}>
      <Select.ItemText>{font.label}</Select.ItemText>
      <Select.ItemIndicator>✓</Select.ItemIndicator>
    </Select.Item>
  ));

  return (
    <Select.Root defaultValue={defaultValue} disabled={disabled} onValueChange={onValueChange}>
      <Select.Label data-testid="font-label">Font</Select.Label>
      <Select.Trigger data-testid="font-trigger">
        <Select.Value data-testid="font-value" placeholder={placeholder} />
        <Select.Icon data-testid="font-icon" />
      </Select.Trigger>
      <Select.Portal>
        <Select.Positioner>
          <Select.Popup data-testid="font-popup">
            <Select.List>
              {grouped ? (
                <Select.Group>
                  <Select.GroupLabel>Typefaces</Select.GroupLabel>
                  <Select.Separator />
                  {options}
                </Select.Group>
              ) : (
                options
              )}
            </Select.List>
          </Select.Popup>
        </Select.Positioner>
      </Select.Portal>
    </Select.Root>
  );
}

const meta = {
  title: "Components/Select",
  component: FontSelect,
  args: {
    onValueChange: fn(),
  },
} satisfies Meta<typeof FontSelect>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The closed control with a selection — the state a form shows most of the time. */
export const Default: Story = {};

/** Nothing selected: the trigger and value both carry `data-placeholder`. */
export const Placeholder: Story = {
  args: { defaultValue: null, placeholder: "Choose a font" },
};

/** The whole select greyed out and out of the tab order. */
export const Disabled: Story = {
  args: { disabled: true },
};

/** Options under a labelled heading, separated by a rule. */
export const Grouped: Story = {
  args: { grouped: true },
};

/** The interaction half: opening the popup and choosing an option. */
export const OpenAndSelect: Story = {
  play: async ({ args, canvas }) => {
    const trigger = canvas.getByTestId("font-trigger");

    await userEvent.click(trigger);

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("font-popup");
    await expect(popup).toBeVisible();
    await expect(trigger).toHaveAttribute("data-popup-open");

    await userEvent.click(screen.getByTestId("font-mono"));

    await expect(args.onValueChange).toHaveBeenCalledWith("mono", expect.anything());
    await waitFor(async () => {
      await expect(canvas.getByTestId("font-value")).toHaveTextContent("mono");
    });
  },
};

/**
 * The popup spans the trigger instead of shrinking to its longest option — the
 * user-visible half of the Select bugfix, and a fact only this project can
 * establish. select.test.tsx pins `min-w-[var(--anchor-width)]` in the popup's
 * class set, but the `browser` project compiles no Tailwind, so it cannot tell a
 * utility backed by real CSS from one that resolves to nothing; and Base UI only
 * writes `--anchor-width` onto the positioner once the popup is really
 * measured and placed, which needs a real open popup rather than a class string.
 */
export const PopupSpansTheTrigger: Story = {
  play: async ({ canvas }) => {
    const trigger = canvas.getByTestId("font-trigger");
    const triggerWidth = trigger.getBoundingClientRect().width;

    await userEvent.click(trigger);
    const popup = await screen.findByTestId("font-popup");

    // Base UI writes --anchor-width on the positioner after the first
    // measurement pass, so the popup reaches its final width a frame or two
    // after it mounts.
    await waitFor(async () => {
      // A minimum, not an equality: an option longer than the trigger is still
      // allowed to widen the popup. The 1px slack absorbs sub-pixel layout.
      await expect(popup.getBoundingClientRect().width).toBeGreaterThanOrEqual(triggerWidth - 1);
    });
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes select.test.tsx's class-set assertions and still paints
 * nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  play: async ({ canvas }) => {
    const trigger = canvas.getByTestId("font-trigger");

    // `border`/`border-input` on the trigger, against the unstyled default of
    // `0px` / `none`.
    await expect(getComputedStyle(trigger).borderTopWidth).not.toBe("0px");
    await expect(getComputedStyle(trigger).borderTopStyle).toBe("solid");

    await userEvent.click(trigger);
    const popup = await screen.findByTestId("font-popup");

    // `bg-popover` on the popup, against the unstyled transparent default.
    await expect(getComputedStyle(popup).backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    // `px-3` on the item.
    await expect(getComputedStyle(screen.getByTestId("font-mono")).paddingLeft).not.toBe("0px");
  },
};
