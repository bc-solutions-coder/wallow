import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { NumberField } from "./number-field";

/*
 * The VISUAL half of the NumberField spec. Unlike `number-field.test.tsx`,
 * these render under the real Tailwind pipeline (`.storybook/preview.css`)
 * against the fork's real theme, so this is the only place the stepper's
 * geometry — the button/input sizes that make the group a single seamless box —
 * can actually be seen.
 *
 * `@storybook/addon-vitest` runs each export below as a Vitest test case, and
 * callback spies come from `fn()` in `storybook/test` (never `vi.fn()`, which
 * the Interactions panel cannot display).
 */

const meta = {
  title: "Components/NumberField",
  component: NumberField.Root,
  args: { onValueChange: fn() },
  render: (args) => (
    <NumberField.Root {...args}>
      <NumberField.ScrubArea data-testid="number-field-scrub">Quantity</NumberField.ScrubArea>
      <NumberField.Group>
        <NumberField.Decrement data-testid="number-field-decrement">-</NumberField.Decrement>
        <NumberField.Input data-testid="number-field-input" />
        <NumberField.Increment data-testid="number-field-increment">+</NumberField.Increment>
      </NumberField.Group>
    </NumberField.Root>
  ),
} satisfies Meta<typeof NumberField.Root>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { defaultValue: 1 },
};

export const Empty: Story = {};

/** Both boundaries in force: the field opens with its increment button already dead. */
export const AtMaximum: Story = {
  args: { defaultValue: 10, min: 0, max: 10 },
};

export const Disabled: Story = {
  args: { defaultValue: 3, disabled: true },
};

export const ReadOnly: Story = {
  args: { defaultValue: 3, readOnly: true },
};

/** `format` is an `Intl.NumberFormatOptions`, so the display can be anything Intl knows. */
export const Currency: Story = {
  args: {
    defaultValue: 1234.5,
    step: 0.5,
    format: { style: "currency", currency: "USD" },
    locale: "en-US",
  },
};

export const LargeStep: Story = {
  args: { defaultValue: 0, step: 25 },
};

/** The interaction half: pressing the stepper buttons moves the value one step. */
export const Stepping: Story = {
  args: { defaultValue: 2 },
  play: async ({ args, canvas }) => {
    const input = canvas.getByTestId("number-field-input");
    const increment = canvas.getByTestId("number-field-increment");
    const decrement = canvas.getByTestId("number-field-decrement");

    await expect(input).toHaveValue("2");

    await userEvent.click(increment);

    await expect(input).toHaveValue("3");
    await expect(args.onValueChange).toHaveBeenCalledTimes(1);

    await userEvent.click(decrement);

    await expect(input).toHaveValue("2");
  },
};
