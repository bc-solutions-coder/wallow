import type { Meta, StoryObj } from "@storybook/react-vite";

import { Button } from "../button";
import { expectScheme } from "../../../.storybook/scheme-assertions";
import { darkScheme, lightScheme } from "../../../.storybook/scheme-decorators";
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
 * interactive beyond its scheme, so the only `play` a story carries is
 * `expectScheme`, which measures that the scheme it claims is the scheme it
 * paints; the markup and class-string assertions live in `empty-state.test.tsx`.
 *
 * The scheme comes from the shared `lightScheme`/`darkScheme` decorators, which
 * stamp the class on `document.documentElement` and remove it again on unmount.
 * A wrapper `<div className="dark">` cannot select a scheme at all — see
 * `.storybook/scheme-decorators.tsx` for why, and never reintroduce one.
 */

const meta = {
  title: "Components/EmptyState",
  component: EmptyState,
  args: { message: "No organizations yet." },
} satisfies Meta<typeof EmptyState>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The bare minimum: one sentence saying what is missing. */
export const MessageOnly: Story = { decorators: [lightScheme], play: expectScheme("light") };

/** The same, in the dark scheme. */
export const MessageOnlyDark: Story = { decorators: [darkScheme], play: expectScheme("dark") };

/**
 * Message plus supporting copy — the second sentence rendered through `Text`'s
 * muted colour, replacing the `text-foreground/60` wallow-web hand-rolls.
 */
export const WithDescription: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: { description: "Nothing belongs here yet. Get started by creating your first one." },
};

/** The organizations empty state as wallow-web ships it today, icon and all. */
export const WithIcon: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: {
    icon: "🏢",
    description: "Nothing belongs here yet. Get started by creating your first organization.",
  },
};

/** The same block in the dark scheme — the card must stay legible on `bg-background`. */
export const WithIconDark: Story = {
  decorators: [darkScheme],
  play: expectScheme("dark"),
  args: {
    icon: "🏢",
    description: "Nothing belongs here yet. Get started by creating your first organization.",
  },
};

/** The full shape: icon, message, description and the call to action under them. */
export const WithAction: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
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
  play: expectScheme("dark"),
  args: {
    icon: "🐷",
    message: "No apps yet.",
    description: "Nothing has been registered here.",
    action: <Button width="auto">Register your first app</Button>,
  },
};
