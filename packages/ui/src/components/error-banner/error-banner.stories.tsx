import type { Meta, StoryObj } from "@storybook/react-vite";

import { ErrorBanner } from "./error-banner";

const meta = {
  title: "Components/ErrorBanner",
  component: ErrorBanner,
  args: {
    children: "Invalid credentials.",
  },
} satisfies Meta<typeof ErrorBanner>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};

/** A message long enough to wrap, so the padding can be judged on two lines. */
export const LongMessage: Story = {
  args: {
    children:
      "We could not sign you in. Check your email and password, then try again — after five failed attempts the account locks for 15 minutes.",
  },
};

/** A caller className overriding the wrapper padding — the refit's `cn()` at work. */
export const RoomyPadding: Story = {
  args: { className: "p-6" },
};

/**
 * Both arms render on the rail here, which is the only comparison that says
 * anything — the page arm looks correct anywhere else.
 */
export const OnTheSidebarSurface: Story = {
  render: (args) => (
    <div className="flex flex-col gap-3 bg-sidebar p-6">
      <ErrorBanner {...args}>Sign out failed (surface=&quot;page&quot;).</ErrorBanner>
      <ErrorBanner {...args} surface="sidebar">
        Sign out failed (surface=&quot;sidebar&quot;).
      </ErrorBanner>
    </div>
  ),
};
