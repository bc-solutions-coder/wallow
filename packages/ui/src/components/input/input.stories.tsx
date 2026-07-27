import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Input } from "./input";

/*
 * `@storybook/addon-vitest` turns each export below into a Vitest test case
 * rendered in the same headless Chromium the `browser` project uses, so these
 * stories are the VISUAL half of the Input spec — one per state a reviewer needs
 * to eyeball — while input.test.tsx holds the assertions about markup that a
 * screenshot cannot make.
 *
 * Callback spies come from `fn()` in `storybook/test`, never `vi.fn()`: a
 * Storybook spy is what the addon can display in the Interactions panel.
 */

const meta = {
  title: "Components/Input",
  component: Input,
  args: {
    placeholder: "name@example.com",
    onChange: fn(),
  },
} satisfies Meta<typeof Input>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};

export const Filled: Story = {
  args: { defaultValue: "ada@example.com" },
};

export const Disabled: Story = {
  args: { disabled: true, defaultValue: "ada@example.com" },
};

export const Password: Story = {
  args: { type: "password", defaultValue: "hunter2", placeholder: "Password" },
};

export const Required: Story = {
  args: { required: true, type: "email" },
};

/** The interaction half: typing reaches the caller's handler through Base UI. */
export const TypeHandling: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.type(canvas.getByRole("textbox"), "ada");

    await expect(args.onChange).toHaveBeenCalled();
    await expect(canvas.getByRole("textbox")).toHaveValue("ada");
  },
};
