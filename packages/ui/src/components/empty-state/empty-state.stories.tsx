import type { Decorator, Meta, StoryObj } from "@storybook/react-vite";

import { Button } from "../button";
import { EmptyState } from "./empty-state";

/*
 * Wallow-lrlm.3.3 — EmptyState stories. Each export becomes a Vitest test case
 * in the same headless Chromium the `browser` project uses, but with the real
 * Tailwind pipeline and the fork's real theme attached (.storybook/preview.css +
 * preview.tsx). Since the `browser` project loads no Tailwind at all, this is the
 * ONLY place the card surface, the centred column and the muted description can
 * actually be seen — and the only place dark-mode correctness is checkable, which
 * matters here because the surface is `bg-card` over `bg-background`.
 *
 * The three shapes the bead names — message only, message + icon, message + icon
 * + action — are each rendered TWICE, once per scheme. EmptyState is not
 * interactive, so no story carries a `play`; the markup and class-string
 * assertions live in `empty-state.test.tsx`.
 *
 * The `.dark`/`.light` wrappers scope a scheme to the story's own subtree rather
 * than stamping `document.documentElement`: stories share one document, so a
 * story that flipped the real root class would leak into every story after it.
 */

/** Renders the story inside the fork's light scheme, scoped to this subtree. */
const lightScheme: Decorator = (Story) => (
  <div className="light bg-background text-foreground p-6">
    <Story />
  </div>
);

/** Renders the story inside the fork's dark scheme, scoped to this subtree. */
const darkScheme: Decorator = (Story) => (
  <div className="dark bg-background text-foreground p-6">
    <Story />
  </div>
);

const meta = {
  title: "Components/EmptyState",
  component: EmptyState,
  args: { message: "No organizations yet." },
} satisfies Meta<typeof EmptyState>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The bare minimum: one sentence saying what is missing. */
export const MessageOnly: Story = { decorators: [lightScheme] };

/** The same, in the dark scheme. */
export const MessageOnlyDark: Story = { decorators: [darkScheme] };

/**
 * Message plus supporting copy — the second sentence rendered through `Text`'s
 * muted colour, replacing the `text-foreground/60` wallow-web hand-rolls.
 */
export const WithDescription: Story = {
  decorators: [lightScheme],
  args: { description: "Nothing belongs here yet. Get started by creating your first one." },
};

/** The organizations empty state as wallow-web ships it today, icon and all. */
export const WithIcon: Story = {
  decorators: [lightScheme],
  args: {
    icon: "🏢",
    description: "Nothing belongs here yet. Get started by creating your first organization.",
  },
};

/** The same block in the dark scheme — the card must stay legible on `bg-background`. */
export const WithIconDark: Story = {
  decorators: [darkScheme],
  args: {
    icon: "🏢",
    description: "Nothing belongs here yet. Get started by creating your first organization.",
  },
};

/** The full shape: icon, message, description and the call to action under them. */
export const WithAction: Story = {
  decorators: [lightScheme],
  args: {
    icon: "🐷",
    message: "No apps yet.",
    description: "Nothing has been registered here.",
    action: <Button width="auto">Register your first app</Button>,
  },
};

/** The full shape in the dark scheme. */
export const WithActionDark: Story = {
  decorators: [darkScheme],
  args: {
    icon: "🐷",
    message: "No apps yet.",
    description: "Nothing has been registered here.",
    action: <Button width="auto">Register your first app</Button>,
  },
};
