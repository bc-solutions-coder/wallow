import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Textarea } from "./textarea";

/*
 * `@storybook/addon-vitest` turns each export below into a Vitest test case
 * rendered in the same headless Chromium the `browser` project uses — but unlike
 * that project, WITH the real Tailwind pipeline and the fork's theme attached.
 * These stories are therefore the VISUAL half of the Textarea spec (one per
 * state a reviewer needs to eyeball, plus the two assertions only a real
 * stylesheet can make), while textarea.test.tsx holds the markup-level edges.
 *
 * Callback spies come from `fn()` in `storybook/test`, never `vi.fn()`: a
 * Storybook spy is what the addon can display in the Interactions panel.
 */

const meta = {
  title: "Components/Textarea",
  component: Textarea,
  args: {
    placeholder: "Tell us about your project",
    onChange: fn(),
  },
} satisfies Meta<typeof Textarea>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};

export const Filled: Story = {
  args: {
    defaultValue:
      "We need a same-origin BFF frontend on top of the existing API, with SSO for two tenants.",
  },
};

export const Disabled: Story = {
  args: { disabled: true, defaultValue: "Submitted — no further edits." },
};

export const WithRows: Story = {
  args: { rows: 8 },
};

/**
 * The two style facts that separate a Textarea from an Input, asserted against
 * the REAL stylesheet: `min-h-20` gives the empty control a readable multi-line
 * box (an unstyled textarea would be ~2 rows tall), and `resize-y` keeps the
 * user's drag on the vertical axis so a long answer never breaks the form's
 * column width. Only the storybook project can see these — the `browser`
 * project loads no Tailwind, so there it would read `auto`/`both`.
 */
export const Painted: Story = {
  play: async ({ canvas }) => {
    const style = getComputedStyle(canvas.getByRole("textbox"));

    await expect(style.minHeight).toBe("80px");
    await expect(style.resize).toBe("vertical");
  },
};

/**
 * The disabled treatment, end to end: the component's own `data-disabled` stamp
 * plus the recipe's `data-[disabled]:opacity-50` have to meet for the control to
 * actually dim. Asserting the class alone (as the browser project must) would
 * pass even if the attribute were never written.
 */
export const PaintedDisabled: Story = {
  args: { disabled: true },
  play: async ({ canvas }) => {
    const textarea = canvas.getByRole("textbox");

    await expect(textarea).toHaveAttribute("data-disabled", "");
    await expect(getComputedStyle(textarea).opacity).toBe("0.5");
  },
};

/** The interaction half: typing reaches the caller's handler and the value lands. */
export const TypeHandling: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.type(canvas.getByRole("textbox"), "hello");

    await expect(args.onChange).toHaveBeenCalled();
    await expect(canvas.getByRole("textbox")).toHaveValue("hello");
  },
};
