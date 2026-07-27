import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Toggle } from "../toggle";
import { ToggleGroup } from "./toggle-group";

/*
 * Wallow-m5aq.2.12 — Toggle Group stories. Each export becomes a Vitest test
 * case in the same headless Chromium the `browser` project uses, with the real
 * Tailwind pipeline attached (see .storybook/main.ts).
 *
 * A group is only visible through the buttons it drives, so every story renders
 * real `Toggle`s inside it — which also makes these the pressed/unpressed states
 * in their natural setting, a toolbar.
 */

const marks = (
  <>
    <Toggle value="bold" data-testid="bold">
      Bold
    </Toggle>
    <Toggle value="italic" data-testid="italic">
      Italic
    </Toggle>
    <Toggle value="underline" data-testid="underline">
      Underline
    </Toggle>
  </>
);

const meta = {
  title: "Components/ToggleGroup",
  component: ToggleGroup,
  args: { onValueChange: fn(), children: marks },
} satisfies Meta<typeof ToggleGroup>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Nothing pressed. */
export const Empty: Story = {};

/** Single-selection mode: pressing one button releases the others. */
export const SingleSelection: Story = {
  args: { defaultValue: ["bold"] },
};

/** Multiple mode: text can be bold AND italic at once. */
export const MultipleSelection: Story = {
  args: { multiple: true, defaultValue: ["bold", "italic"] },
};

/** The vertical axis the recipe's `data-[orientation=vertical]:` modifier styles. */
export const Vertical: Story = {
  args: { orientation: "vertical", defaultValue: ["bold"] },
};

export const Disabled: Story = {
  args: { disabled: true, defaultValue: ["bold"] },
};

/** The interaction half: pressing a button hands the caller the whole new array. */
export const Toggling: Story = {
  args: { defaultValue: ["bold"] },
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByTestId("italic"));

    await expect(args.onValueChange).toHaveBeenCalledTimes(1);
    await expect(args.onValueChange).toHaveBeenCalledWith(["italic"], expect.anything());
    await expect(canvas.getByTestId("bold").hasAttribute("data-pressed")).toBe(false);
  },
};
