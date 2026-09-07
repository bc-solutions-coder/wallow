import type { Meta, StoryObj } from "@storybook/react-vite";

import { QuietLink } from "./quiet-link";

/*
 * The visual half of QuietLink's spec. Hover is the whole affordance here, so
 * the stories exist to be hovered in the explorer; the colour SWAP itself is
 * asserted as a class set in quiet-link.test.tsx rather than measured, because
 * `:hover` is not a state a story's play function can hold open for
 * getComputedStyle.
 */

const meta = {
  title: "Components/QuietLink",
  component: QuietLink,
  args: {
    href: "/login",
    children: "Back to sign in",
  },
} satisfies Meta<typeof QuietLink>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The bare recipe — 9 of the 13 sourced call sites. */
export const Default: Story = {};

/** InvitationScreen's footer: a centred block beneath the card body. */
export const CenteredBlock: Story = {
  args: { className: "block text-center" },
};

export const InlineBackLink: Story = {
  args: { className: "inline-block mb-4", children: "← Back to inquiries" },
};

/** The "Forgot password?" site, where the link sits inline beside a field label. */
export const BesideALabel: Story = {
  args: { children: "Forgot password?" },
};
