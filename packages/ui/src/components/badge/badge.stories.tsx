import type { Meta, StoryObj } from "@storybook/react-vite";

import { Badge } from "./badge";

/*
 * The visual half of Badge's spec (Wallow-lrlm.3.4). Not interactive, so no
 * `play`. The preview decorator feeds these packages/styles/branding.json's real tokens,
 * which is the only place the state colours can actually be judged: the
 * `browser` project loads no Tailwind, so `badge.test.tsx` can assert the class
 * names but not that success reads as green against the fork's palette.
 *
 * One story per variant, plus the two grids a reviewer reads to spot a variant
 * out of step — the four surfaces side by side, and the MFA row the success
 * variant exists to unblock.
 */

const VARIANTS = ["neutral", "success", "warning", "destructive"] as const;

const meta = {
  title: "Components/Badge",
  component: Badge,
  args: {
    children: "Member",
  },
} satisfies Meta<typeof Badge>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The default, and the chip six wallow-web surfaces hand-roll today. */
export const Neutral: Story = {};

/** The new capability: the state colour Wallow-lrlm.1.1's success token added. */
export const Success: Story = {
  args: { variant: "success", children: "Enabled" },
};

/** Amber, borrowed from the fork's primary — the theme has no warning token. */
export const Warning: Story = {
  args: { variant: "warning", children: "Pending" },
};

export const Destructive: Story = {
  args: { variant: "destructive", children: "Revoked" },
};

/** All four surfaces in a row — the comparison that catches a mismatched pill. */
export const VariantRow: Story = {
  render: (args) => (
    <div className="flex items-center gap-3">
      {VARIANTS.map((variant) => (
        <Badge {...args} key={variant} variant={variant}>
          {variant}
        </Badge>
      ))}
    </div>
  ),
};

/**
 * The call site the success variant unblocks: MfaSettingsSection's status chip,
 * which stayed state-independent only because the theme had no success token.
 */
export const MfaStatusRow: Story = {
  render: (args) => (
    <div className="flex items-center gap-3">
      <Badge {...args} variant="success">
        Enabled
      </Badge>
      <Badge {...args} variant="neutral">
        Disabled
      </Badge>
    </div>
  ),
};

/** A caller className overriding the surface — `cn()` over the recipe at work. */
export const OverriddenSurface: Story = {
  args: { className: "bg-secondary text-secondary-foreground", children: "Custom" },
};
