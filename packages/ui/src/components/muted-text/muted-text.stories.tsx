import type { Meta, StoryObj } from "@storybook/react-vite";

import { MutedText } from "./muted-text";

/*
 * The visual half of MutedText's spec (Wallow-m5aq.2.13). Not interactive, so
 * no `play`. The stories cover the two shapes the component ships in today: an
 * inline hint under a field, and the centred loading line five wallow-web lists
 * render while a query is in flight.
 */

const meta = {
  title: "Components/MutedText",
  component: MutedText,
  args: {
    children: "We will never share your email.",
  },
} satisfies Meta<typeof MutedText>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The bare recipe, 41x across wallow-auth. */
export const Default: Story = {};

/** The wallow-web loading line: centred with vertical padding. */
export const CenteredLoading: Story = {
  args: { className: "text-center py-12", children: "Loading organizations…" },
};

/** A caller className overriding the muted colour — the refit's `cn()` at work. */
export const OverriddenColour: Story = {
  args: { className: "text-destructive", children: "That code has expired." },
};
