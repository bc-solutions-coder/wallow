import type { Meta, StoryObj } from "@storybook/react-vite";

import { MutedText } from "./muted-text";

const meta = {
  title: "Components/MutedText",
  component: MutedText,
  args: {
    children: "We will never share your email.",
  },
} satisfies Meta<typeof MutedText>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};

export const CenteredLoading: Story = {
  args: { className: "text-center py-12", children: "Loading organizations…" },
};

/** A caller className overriding the muted colour — the refit's `cn()` at work. */
export const OverriddenColour: Story = {
  args: { className: "text-destructive", children: "That code has expired." },
};
