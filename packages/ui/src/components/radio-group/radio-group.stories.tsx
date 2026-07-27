import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Radio } from "../radio";
import { RadioGroup } from "./radio-group";

/*
 * The visual half of the radio group's spec: one story per orientation and per
 * state a reviewer needs to eyeball. `@storybook/addon-vitest` runs each of
 * them as a browser test case. Behavioural assertions live in
 * radio-group.test.tsx.
 *
 * `fn()` comes from `storybook/test`, never `vi.fn()` — a Storybook spy is what
 * the addon can display in the Interactions panel.
 */

const fruitRadios = (
  <>
    <Radio.Root value="apple" aria-label="Apple">
      <Radio.Indicator />
    </Radio.Root>
    <Radio.Root value="pear" aria-label="Pear">
      <Radio.Indicator />
    </Radio.Root>
  </>
);

const meta = {
  title: "Components/RadioGroup",
  component: RadioGroup,
  args: {
    name: "fruit",
    "aria-label": "Fruit",
    children: fruitRadios,
    onValueChange: fn(),
  },
} satisfies Meta<typeof RadioGroup>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Vertical: Story = {};

export const Horizontal: Story = {
  args: { orientation: "horizontal" },
};

export const Preselected: Story = {
  args: { defaultValue: "pear" },
};

export const Disabled: Story = {
  args: { disabled: true, defaultValue: "apple" },
};

export const ReadOnly: Story = {
  args: { readOnly: true, defaultValue: "apple" },
};

/** The interaction half: selecting a radio reports its value to the caller. */
export const SelectionHandling: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByRole("radio", { name: "Pear" }));

    await expect(args.onValueChange).toHaveBeenCalledTimes(1);
    await expect(canvas.getByRole("radio", { name: "Pear" })).toHaveAttribute("data-checked");
  },
};
