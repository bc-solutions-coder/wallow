import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Checkbox } from "../checkbox";
import { CheckboxGroup } from "./checkbox-group";

/*
 * Wallow-m5aq.2.5 — Checkbox Group stories. Each export becomes a Vitest test
 * case in the same headless Chromium the `browser` project uses, with the real
 * Tailwind pipeline attached (see .storybook/main.ts).
 *
 * A group is only visible through the boxes it drives, so every story renders
 * real `Checkbox.Root`s inside it — which also makes these the checked/unchecked
 * /indeterminate states in their natural setting.
 */

const Tick = <Checkbox.Indicator>✓</Checkbox.Indicator>;
const Dash = <Checkbox.Indicator>–</Checkbox.Indicator>;

const meta = {
  title: "Components/CheckboxGroup",
  component: CheckboxGroup,
  args: {
    onValueChange: fn(),
    children: (
      <>
        <Checkbox.Root name="releases" data-testid="releases">
          {Tick}
        </Checkbox.Root>
        <Checkbox.Root name="security" data-testid="security">
          {Tick}
        </Checkbox.Root>
      </>
    ),
  },
} satisfies Meta<typeof CheckboxGroup>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Nothing ticked. */
export const Empty: Story = {};

/** Some ticked — the ordinary case a form starts in. */
export const PartiallyChecked: Story = {
  args: { defaultValue: ["releases"] },
};

/**
 * A parent box summarising the others. It sits in the MIXED state while only
 * some of `allValues` are ticked, and ticking it ticks everything.
 */
export const WithParentCheckbox: Story = {
  args: {
    defaultValue: ["releases"],
    allValues: ["releases", "security"],
    children: (
      <>
        <Checkbox.Root parent data-testid="all">
          {Dash}
        </Checkbox.Root>
        <Checkbox.Root name="releases" data-testid="releases">
          {Tick}
        </Checkbox.Root>
        <Checkbox.Root name="security" data-testid="security">
          {Tick}
        </Checkbox.Root>
      </>
    ),
  },
};

export const Disabled: Story = {
  args: { disabled: true, defaultValue: ["releases"] },
};

/** The interaction half: ticking a box hands the caller the whole new array. */
export const Toggling: Story = {
  args: { defaultValue: ["releases"] },
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByTestId("security"));

    await expect(args.onValueChange).toHaveBeenCalledTimes(1);
    await expect(args.onValueChange).toHaveBeenCalledWith(
      ["releases", "security"],
      expect.anything(),
    );
  },
};
