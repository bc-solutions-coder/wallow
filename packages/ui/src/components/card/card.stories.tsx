import type { Meta, StoryObj } from "@storybook/react-vite";

import { Card, CardTitle } from "./card";

/*
 * The visual half of Card's spec (Wallow-m5aq.2.13). card.test.tsx makes the
 * markup assertions a screenshot cannot; these stories cover the states a
 * reviewer needs to eyeball — the default rhythm, each measured `spacing`
 * outlier, and the heading in place. Card is not interactive, so no `play`.
 */

const meta = {
  title: "Components/Card",
  component: Card,
  args: {
    children: (
      <>
        <CardTitle>Sign in</CardTitle>
        <p className="text-sm text-muted-foreground">Use your work account to continue.</p>
      </>
    ),
  },
} satisfies Meta<typeof Card>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The dominant recipe: `p-6 space-y-6`, 14x across wallow-auth. */
export const Default: Story = {};

/** The LoginScreen outlier: a tighter vertical rhythm. */
export const TightSpacing: Story = {
  args: { spacing: "p-6 space-y-4" },
};

/** The RegisterForm outlier: padding only, no rhythm block. */
export const PaddingOnly: Story = {
  args: { spacing: "p-6" },
};

/** The wallow-web call site: a roomier `spacing` plus an additive className. */
export const RoomyWithShadow: Story = {
  args: { spacing: "p-8 space-y-6", className: "shadow-sm" },
};

/** A caller className overriding a recipe utility — the refit's `cn()` at work. */
export const SquareCorners: Story = {
  args: { className: "rounded-none" },
};

/** The heading on its own, so its recipe can be reviewed in isolation. */
export const TitleOnly: Story = {
  args: { children: <CardTitle>Account settings</CardTitle> },
};
