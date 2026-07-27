import type { Meta, StoryObj } from "@storybook/react-vite";

import { Card, CardTitle } from "../card/card";
import { MutedText } from "../muted-text/muted-text";
import { CenteredCardLayout } from "./centered-card-layout";

/*
 * The visual half of CenteredCardLayout's spec (Wallow-m5aq.2.13). The layout
 * has nothing to look at without something inside it, so the stories fill the
 * column with the auth-screen content it was generalized from. Not interactive,
 * so no `play`.
 */

const meta = {
  title: "Components/CenteredCardLayout",
  component: CenteredCardLayout,
  args: {
    children: (
      <Card>
        <CardTitle>Sign in</CardTitle>
        <MutedText>Use your work account to continue.</MutedText>
      </Card>
    ),
  },
} satisfies Meta<typeof CenteredCardLayout>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The auth-screen shell: a 420px column centred in the viewport. */
export const Default: Story = {};

/** A wider column, via the caller className the refit now honours. */
export const WideColumn: Story = {
  args: { className: "max-w-2xl" },
};

/** Stacked content, to show the column does not constrain height. */
export const StackedContent: Story = {
  args: {
    children: (
      <>
        <Card>
          <CardTitle>Check your email</CardTitle>
          <MutedText>We sent a six-digit code to you@example.com.</MutedText>
        </Card>
        <MutedText className="mt-4 text-center">Didn’t get it? Resend in 30s.</MutedText>
      </>
    ),
  },
};
