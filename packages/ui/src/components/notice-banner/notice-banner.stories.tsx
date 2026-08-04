import type { Meta, StoryObj } from "@storybook/react-vite";

import { Text } from "../text";
import { NoticeBanner } from "./notice-banner";

/*
 * The visual half of NoticeBanner's spec (Wallow-86os). The banner is not
 * interactive, so no `play`; these stories exist so a reviewer can judge the two
 * tints against the fork's real palette, which is the one thing
 * `notice-banner.test.tsx` cannot do — the `browser` project loads no Tailwind,
 * so it can assert the class names but not that success reads as green.
 *
 * The children are composed with `Text` rather than passed as a bare string
 * because that is what every real call site does: this component owns no
 * typography, deliberately.
 */

const meta = {
  title: "Components/NoticeBanner",
  component: NoticeBanner,
  args: {
    children: <Text variant="bodySm">Your password has been reset. You can now sign in.</Text>,
  },
} satisfies Meta<typeof NoticeBanner>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The default tone, and five of the six measured call sites. */
export const Success: Story = {};

/** The sixth call site's tone — LoginScreen's MFA enrollment nudge. */
export const Warning: Story = {
  args: {
    tone: "warning",
    children: <Text variant="bodySm">MFA enrollment required.</Text>,
  },
};

/** Both tints side by side — the comparison that catches one out of step. */
export const ToneRow: Story = {
  render: (args) => (
    <div className="flex flex-col gap-3">
      <NoticeBanner {...args} tone="success">
        <Text variant="bodySm">You are now signed in.</Text>
      </NoticeBanner>
      <NoticeBanner {...args} tone="warning">
        <Text variant="bodySm">MFA enrollment required.</Text>
      </NoticeBanner>
    </div>
  ),
};

/**
 * The call site the missing inner `<p>` exists for: LoginScreen's MFA banner is
 * a heading, a body and an action link, and it supplies its own `space-y-2`
 * because the recipe deliberately owns no vertical rhythm. An `ErrorBanner`
 * shape — children sealed inside one styled paragraph — could not render this.
 */
export const HeadingAndAction: Story = {
  args: {
    tone: "warning",
    className: "space-y-2",
    children: (
      <>
        <Text variant="bodySm" weight="medium">
          MFA enrollment required
        </Text>
        <Text variant="bodySm" color="muted">
          Your organization requires two-factor authentication. Please set it up before 1 September
          2026.
        </Text>
        <a className="inline-block text-sm font-medium text-primary" href="#enrol">
          Set up now
        </a>
      </>
    ),
  },
};

/** A message long enough to wrap, so the padding can be judged on two lines. */
export const LongMessage: Story = {
  args: {
    children: (
      <Text variant="bodySm">
        We have sent a sign-in link to your email address. It expires in 15 minutes — if it does not
        arrive, check your spam folder before requesting another.
      </Text>
    ),
  },
};
