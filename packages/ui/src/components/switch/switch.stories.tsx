import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Switch } from "./switch";

/*
 * The VISUAL half of the Switch spec. Unlike `switch.test.tsx`, these render
 * under the real Tailwind pipeline (`.storybook/preview.css`) against the fork's
 * real theme, so this is the only place the track's size and the thumb's travel
 * can actually be seen — a switch is pure geometry, with no text to fall back on.
 *
 * `@storybook/addon-vitest` runs each export below as a Vitest test case, and
 * callback spies come from `fn()` in `storybook/test` (never `vi.fn()`, which
 * the Interactions panel cannot display).
 */

const meta = {
  title: "Components/Switch",
  component: Switch.Root,
  args: { onCheckedChange: fn() },
  render: (args) => (
    <Switch.Root {...args}>
      <Switch.Thumb data-testid="switch-thumb" />
    </Switch.Root>
  ),
} satisfies Meta<typeof Switch.Root>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Off: Story = {};

export const On: Story = {
  args: { defaultChecked: true },
};

export const Disabled: Story = {
  args: { disabled: true },
};

export const DisabledOn: Story = {
  args: { disabled: true, defaultChecked: true },
};

export const ReadOnly: Story = {
  args: { readOnly: true, defaultChecked: true },
};

/** The interaction half: a click flips both parts into the checked state. */
export const Toggling: Story = {
  play: async ({ args, canvas }) => {
    const root = canvas.getByRole("switch");
    const thumb = canvas.getByTestId("switch-thumb");

    await expect(root.hasAttribute("data-unchecked")).toBe(true);

    await userEvent.click(root);

    await expect(root.hasAttribute("data-checked")).toBe(true);
    await expect(thumb.hasAttribute("data-checked")).toBe(true);
    await expect(args.onCheckedChange).toHaveBeenCalledTimes(1);
  },
};
