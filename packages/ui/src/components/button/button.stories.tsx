import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Button } from "./button";

/*
 * EXEMPLAR STORIES (Wallow-m5aq.2.1), extended by Wallow-lrlm.3.1. Stories in
 * this package are not a side-car explorer: `@storybook/addon-vitest` turns each
 * export below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, and the preview decorator feeds them packages/styles/branding.json's
 * real tokens. So a story is the VISUAL half of a component's spec — one per
 * variant and per state that a reviewer needs to eyeball — while
 * `button.test.tsx` holds the assertions about markup that a screenshot cannot
 * make.
 *
 * Interactive components add a `play` function that drives the component and
 * asserts the outcome, with `fn()` from `storybook/test` for callback spies
 * (never `vi.fn()` here — a Storybook spy is what the addon can display in the
 * Interactions panel).
 *
 * The recipe now has four variant groups (variant, size, width, shape). The
 * single-prop stories below cover each option on its own; `VariantSizeMatrix`
 * covers the product, which is the grid a reviewer actually reads to spot a
 * variant whose padding or hover treatment is out of step with the rest.
 */

const VARIANTS = ["primary", "secondary", "destructive", "outline", "ghost", "link"] as const;
const SIZES = ["sm", "md", "lg"] as const;

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

export const Outline: Story = {
  args: { variant: "outline", children: "Choose a file" },
};

export const Ghost: Story = {
  args: { variant: "ghost", children: "Dismiss" },
};

export const Link: Story = {
  args: { variant: "link", children: "Read the docs" },
};

export const SizeSmall: Story = {
  args: { size: "sm", width: "auto", children: "Small" },
};

export const SizeMedium: Story = {
  args: { size: "md", width: "auto", children: "Medium" },
};

export const SizeLarge: Story = {
  args: { size: "lg", width: "auto", children: "Large" },
};

/** The square target: no label, so the glyph must sit centred in its own box. */
export const SizeIcon: Story = {
  args: { size: "icon", width: "auto", "aria-label": "Close", children: "×" },
};

export const WidthAuto: Story = {
  args: { width: "auto", children: "Hugs its label" },
};

/** The default, and what all 11 pre-existing call sites render. */
export const WidthFull: Story = {
  args: { width: "full", children: "Fills its container" },
};

export const ShapePill: Story = {
  args: { shape: "pill", width: "auto", children: "Pill" },
};

export const Disabled: Story = {
  args: { disabled: true },
};

/** Every variant against every text size — the grid that catches an odd one out. */
export const VariantSizeMatrix: Story = {
  render: (args) => (
    <div className="flex flex-col gap-4">
      {VARIANTS.map((variant) => (
        <div key={variant} className="flex items-center gap-3">
          {SIZES.map((size) => (
            <Button {...args} key={size} variant={variant} size={size} width="auto">
              {variant}/{size}
            </Button>
          ))}
        </div>
      ))}
    </div>
  ),
};

/** The two shapes beside each other, since the radius only reads by comparison. */
export const ShapeComparison: Story = {
  render: (args) => (
    <div className="flex items-center gap-3">
      <Button {...args} shape="rounded" width="auto">
        Rounded
      </Button>
      <Button {...args} shape="pill" width="auto">
        Pill
      </Button>
    </div>
  ),
};

/** The interaction half: a click reaches the caller's handler through Base UI. */
export const ClickHandling: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByRole("button"));

    await expect(args.onClick).toHaveBeenCalledTimes(1);
  },
};

/** Hover is a recipe state, so the story drives it rather than describing it. */
export const HoverTreatment: Story = {
  args: { width: "auto" },
  play: async ({ canvas }) => {
    const button = canvas.getByRole("button");

    await userEvent.hover(button);

    await expect(button).toBeVisible();
  },
};

/**
 * The `surface` axis (Wallow-lrlm.6.4) — which surface the button is composed
 * ONTO, in the one place it can be judged: on an actual `bg-sidebar` rail.
 *
 * Left column is `surface="page"`, the default every existing call site gets;
 * right is `surface="sidebar"`. The pair reads as one picture — the page arm's
 * chips sit on the rail as blocks of the wrong palette, while the sidebar arm's
 * take the rail's own rest/hover treatment. `secondary` is the row that matters
 * most: `ThemeToggle` hard-codes it, so that is the button the dashboard rail
 * actually renders.
 */
export const SurfaceOnSidebar: Story = {
  render: (args) => (
    <div className="flex flex-col gap-3 bg-sidebar p-6">
      {VARIANTS.map((variant) => (
        <div key={variant} className="flex items-center gap-3">
          <Button {...args} variant={variant} width="auto">
            {variant}/page
          </Button>
          <Button {...args} variant={variant} surface="sidebar" width="auto">
            {variant}/sidebar
          </Button>
        </div>
      ))}
    </div>
  ),
};

/** The sidebar arm's hover state, driven rather than described. */
export const SurfaceOnSidebarHover: Story = {
  args: { variant: "secondary", surface: "sidebar", width: "auto", children: "System" },
  decorators: [
    (Story) => (
      <div className="bg-sidebar p-6">
        <Story />
      </div>
    ),
  ],
  play: async ({ canvas }) => {
    const button = canvas.getByRole("button");

    await userEvent.hover(button);

    await expect(button).toBeVisible();
  },
};
