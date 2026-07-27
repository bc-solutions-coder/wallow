import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Button } from "./button";

/*
 * EXEMPLAR STORIES (Wallow-m5aq.2.1). Stories in this package are not a side-car
 * explorer: `@storybook/addon-vitest` turns each export below into a Vitest test
 * case rendered in the same headless Chromium the `browser` project uses, and
 * the preview decorator feeds them api/branding.json's real tokens. So a story
 * is the VISUAL half of a component's spec — one per variant and per state that
 * a reviewer needs to eyeball — while `button.test.tsx` holds the assertions
 * about markup that a screenshot cannot make.
 *
 * Interactive components add a `play` function that drives the component and
 * asserts the outcome, with `fn()` from `storybook/test` for callback spies
 * (never `vi.fn()` here — a Storybook spy is what the addon can display in the
 * Interactions panel).
 */

const meta = {
  title: "Components/Button",
  component: Button,
  args: {
    children: "Continue",
    onClick: fn(),
  },
} satisfies Meta<typeof Button>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Primary: Story = {};

export const Secondary: Story = {
  args: { variant: "secondary", children: "Cancel" },
};

export const Destructive: Story = {
  args: { variant: "destructive", children: "Delete account" },
};

export const Disabled: Story = {
  args: { disabled: true },
};

/** The interaction half: a click reaches the caller's handler through Base UI. */
export const ClickHandling: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByRole("button"));

    await expect(args.onClick).toHaveBeenCalledTimes(1);
  },
};
