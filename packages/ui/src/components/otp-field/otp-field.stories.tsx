import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { OTPField } from "./otp-field";

/*
 * The VISUAL half of the OTPField spec. Unlike `otp-field.test.tsx`, these
 * render under the real Tailwind pipeline (`.storybook/preview.css`) against the
 * fork's real theme, so this is the only place the row's spacing, the square
 * slots and the filled-slot border can actually be seen.
 *
 * `@storybook/addon-vitest` runs each export below as a Vitest test case, and
 * callback spies come from `fn()` in `storybook/test` (never `vi.fn()`, which
 * the Interactions panel cannot display).
 */

const meta = {
  title: "Components/OTPField",
  component: OTPField.Root,
  args: {
    length: 6,
    onValueChange: fn(),
    onValueComplete: fn(),
  },
  render: ({ length, ...args }) => (
    <OTPField.Root length={length} {...args}>
      {Array.from({ length }, (_, index) => (
        <OTPField.Input key={index} />
      ))}
    </OTPField.Root>
  ),
} satisfies Meta<typeof OTPField.Root>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};

export const PartiallyFilled: Story = {
  args: { defaultValue: "123" },
};

export const Complete: Story = {
  args: { defaultValue: "123456" },
};

export const Masked: Story = {
  args: { mask: true, defaultValue: "123456" },
};

export const Disabled: Story = {
  args: { disabled: true, defaultValue: "123456" },
};

export const ReadOnly: Story = {
  args: { readOnly: true, defaultValue: "123456" },
};

/** The grouped layout: a separator splitting the code into two halves. */
export const WithSeparator: Story = {
  args: { length: 6, defaultValue: "123" },
  render: ({ length, ...args }) => (
    <OTPField.Root length={length} {...args}>
      <OTPField.Input />
      <OTPField.Input />
      <OTPField.Input />
      <OTPField.Separator />
      <OTPField.Input />
      <OTPField.Input />
      <OTPField.Input />
    </OTPField.Root>
  ),
};

/**
 * The interaction half. It also carries the one assertion no `*.test.tsx` in
 * this package can make: that the row's recipe really lays the slots out in a
 * row. The vitest browser project loads no Tailwind, so `flex` there is an
 * unresolved class name — here it is a computed style.
 */
export const Entering: Story = {
  args: { length: 4 },
  play: async ({ args, canvas }) => {
    const row = canvas.getByRole("group");
    const slots = canvas.getAllByRole("textbox");

    await expect(getComputedStyle(row).display).toBe("flex");

    await userEvent.click(slots[0]);
    await userEvent.keyboard("1234");

    await expect(args.onValueComplete).toHaveBeenCalledTimes(1);
    await expect(row.hasAttribute("data-complete")).toBe(true);
    for (const slot of slots) {
      await expect(slot.hasAttribute("data-filled")).toBe(true);
    }
  },
};
