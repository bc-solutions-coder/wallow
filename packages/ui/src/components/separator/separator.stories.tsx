import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect } from "storybook/test";

import { Separator } from "./separator";

/*
 * Wallow-m5aq.4.3 — Separator stories. `@storybook/addon-vitest` turns each
 * export below into a Vitest test case rendered in the same headless Chromium
 * the `browser` project uses, but with the real Tailwind pipeline attached
 * (.storybook/preview.css), so this is the only place a hairline that is
 * actually one pixel of `bg-border` can be seen.
 *
 * A separator has no size of its own, so every story below wraps it in a box
 * that gives it something to span — a bare `<Separator />` on an empty canvas
 * renders as nothing at all, in Storybook exactly as in an app.
 *
 * Division of labour with separator.test.tsx: that file proves the markup and
 * the ARIA, this one proves the recipe PAINTS (PaintedByTheDesignTokens reads
 * computed styles, which the `browser` project cannot do because it compiles no
 * Tailwind).
 */

/*
 * `data-testid` is written into the JSX below rather than into `args`: an
 * arbitrary `data-*` key is legal on a JSX element but not inside an object
 * literal typed as `SeparatorProps`, so putting it in `args` is a type error.
 */
const meta = {
  title: "Components/Separator",
  component: Separator,
} satisfies Meta<typeof Separator>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The default: a full-width rule between two stacked blocks of content. */
export const Horizontal: Story = {
  render: (args) => (
    <div className="w-64">
      <p className="pb-2 text-sm">Account</p>
      <Separator data-testid="separator" {...args} />
      <p className="pt-2 text-sm text-muted-foreground">Manage your profile and password.</p>
    </div>
  ),
};

/** The vertical rule, dividing items laid out in a row. */
export const Vertical: Story = {
  args: { orientation: "vertical" },
  render: (args) => (
    <div className="flex h-8 items-center gap-3 text-sm">
      <span>Profile</span>
      <Separator data-testid="separator" {...args} />
      <span>Billing</span>
      <Separator orientation="vertical" />
      <span>Team</span>
    </div>
  ),
};

/** Composed onto the semantic `<hr>` through Base UI's `render` prop. */
export const AsHorizontalRule: Story = {
  args: { render: <hr /> },
  render: (args) => (
    <div className="w-64">
      <p className="pb-2 text-sm">Above</p>
      <Separator data-testid="separator" {...args} />
      <p className="pt-2 text-sm">Below</p>
    </div>
  ),
};

/** A caller override: the rule takes the accent colour instead of the border. */
export const AccentColoured: Story = {
  args: { className: "bg-accent" },
  render: (args) => (
    <div className="w-64">
      <Separator data-testid="separator" {...args} />
    </div>
  ),
};

/**
 * The proof the recipe PAINTS. This is the assertion the `browser` project
 * cannot make: it compiles no Tailwind, so only here do `bg-border` and the
 * `data-[orientation=...]:` size rules become computed style. An empty stub
 * recipe fails every line below — an unstyled separator is a transparent,
 * zero-height box.
 */
export const PaintedByTheDesignTokens: Story = {
  render: (args) => (
    <div className="flex h-8 w-64 items-center gap-3">
      <Separator data-testid="painted-horizontal" {...args} />
      <Separator orientation="vertical" data-testid="painted-vertical" />
    </div>
  ),
  play: async ({ canvas }) => {
    const horizontal = canvas.getByTestId("painted-horizontal");
    const horizontalStyle = getComputedStyle(horizontal);

    // `bg-border` plus `h-px w-full`: a hairline that spans its container.
    await expect(horizontalStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(horizontalStyle.height).toBe("1px");
    await expect(Number.parseFloat(horizontalStyle.width)).toBeGreaterThan(1);

    // `h-full w-px`: the same hairline turned ninety degrees.
    const verticalStyle = getComputedStyle(canvas.getByTestId("painted-vertical"));
    await expect(verticalStyle.width).toBe("1px");
    await expect(Number.parseFloat(verticalStyle.height)).toBeGreaterThan(1);
  },
};
