import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, userEvent } from "storybook/test";

import { RadioGroup } from "../radio-group";
import { Radio } from "./radio";

/*
 * `@storybook/addon-vitest` turns each export below into a Vitest test case
 * rendered in the same headless Chromium the `browser` project uses, so these
 * stories are the VISUAL half of the radio's spec — one per state a reviewer
 * needs to eyeball — while radio.test.tsx holds the assertions about markup
 * that a screenshot cannot make.
 *
 * A radio is meaningless outside a group (the group owns the selected value and
 * the shared `name`), so every story renders one inside a `RadioGroup`.
 */

const meta = {
  title: "Components/Radio",
  component: Radio.Root,
  args: {
    value: "apple",
    "aria-label": "Apple",
    children: <Radio.Indicator />,
  },
  render: (args) => (
    <RadioGroup name="fruit" aria-label="Fruit">
      <Radio.Root {...args} />
    </RadioGroup>
  ),
} satisfies Meta<typeof Radio.Root>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unselected: Story = {};

/** Selected state — the group's `defaultValue` is what selects a radio. */
export const Selected: Story = {
  render: (args) => (
    <RadioGroup name="fruit" aria-label="Fruit" defaultValue="apple">
      <Radio.Root {...args} />
    </RadioGroup>
  ),
};

export const Disabled: Story = {
  args: { disabled: true },
};

export const ReadOnly: Story = {
  args: { readOnly: true },
};

export const Required: Story = {
  args: { required: true },
};

/** The interaction half: clicking the radio selects it. */
export const SelectOnClick: Story = {
  play: async ({ canvas }) => {
    const radio = canvas.getByRole("radio", { name: "Apple" });

    await userEvent.click(radio);

    await expect(radio).toHaveAttribute("data-checked");
  },
};
