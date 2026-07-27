import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Checkbox } from "./checkbox";

/*
 * Wallow-m5aq.2.5 — Checkbox stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium
 * the `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while checkbox.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * The bead requires a story per checkbox STATE — ticked, unticked and mixed —
 * because those three are what a reviewer has to eyeball; they are also the
 * three the recipe paints through `data-[checked]:` / `data-[indeterminate]:`.
 */

/** The tick mark. A checked box shows it; a mixed box shows a dash instead. */
const Tick = <Checkbox.Indicator>✓</Checkbox.Indicator>;

const meta = {
  title: "Components/Checkbox",
  component: Checkbox.Root,
  args: {
    children: Tick,
    onCheckedChange: fn(),
  },
} satisfies Meta<typeof Checkbox.Root>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Unticked — the indicator is not in the DOM at all. */
export const Unchecked: Story = {};

/** Ticked — the filled box with its tick mark. */
export const Checked: Story = {
  args: { defaultChecked: true },
};

/** Mixed — neither ticked nor unticked, as used by a Checkbox Group parent. */
export const Indeterminate: Story = {
  args: {
    indeterminate: true,
    children: <Checkbox.Indicator>–</Checkbox.Indicator>,
  },
};

export const Disabled: Story = {
  args: { disabled: true },
};

export const DisabledChecked: Story = {
  args: { disabled: true, defaultChecked: true },
};

/** The interaction half: a click ticks the box and reaches the caller's handler. */
export const Toggling: Story = {
  play: async ({ args, canvas }) => {
    const box = canvas.getByRole("checkbox");

    await userEvent.click(box);

    await expect(box).toHaveAttribute("data-checked");
    await expect(args.onCheckedChange).toHaveBeenCalledTimes(1);
  },
};
