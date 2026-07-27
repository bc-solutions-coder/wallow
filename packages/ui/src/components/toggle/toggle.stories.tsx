import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Toggle } from "./toggle";

/*
 * Wallow-m5aq.2.12 — Toggle stories. Each export becomes a Vitest test case in
 * the same headless Chromium the `browser` project uses, but with the real
 * Tailwind pipeline attached (.storybook/preview.css), so this is the only place
 * the pressed state's colour and the button's padding can actually be seen.
 *
 * Callback spies come from `fn()` in `storybook/test` (never `vi.fn()`, which
 * the Interactions panel cannot display).
 */

const meta = {
  title: "Components/Toggle",
  component: Toggle,
  args: { children: "Bold", onPressedChange: fn() },
} satisfies Meta<typeof Toggle>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unpressed: Story = {};

export const Pressed: Story = {
  args: { defaultPressed: true },
};

export const Disabled: Story = {
  args: { disabled: true },
};

export const DisabledPressed: Story = {
  args: { disabled: true, defaultPressed: true },
};

/** Composed onto a link through Base UI's `render` prop, recipe and all. */
export const AsLink: Story = {
  args: {
    defaultPressed: true,
    render: <a href="#bold" />,
    nativeButton: false,
  },
};

/** The interaction half: a click flips the button into the pressed state. */
export const Toggling: Story = {
  play: async ({ args, canvas }) => {
    const toggle = canvas.getByRole("button", { name: "Bold" });

    await expect(toggle.hasAttribute("data-pressed")).toBe(false);

    await userEvent.click(toggle);

    await expect(toggle.hasAttribute("data-pressed")).toBe(true);
    await expect(toggle.getAttribute("aria-pressed")).toBe("true");
    await expect(args.onPressedChange).toHaveBeenCalledTimes(1);
  },
};
