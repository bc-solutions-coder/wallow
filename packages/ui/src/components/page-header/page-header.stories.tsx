import type { Decorator, Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";

import { PageHeader } from "./page-header";

/*
 * Wallow-lrlm.3.2 — PageHeader stories. Each export becomes a Vitest test case
 * in the same headless Chromium the `browser` project uses, but with the real
 * Tailwind pipeline and the fork's real theme attached (.storybook/preview.css +
 * preview.tsx). Since the `browser` project loads no Tailwind, this is the only
 * place the header's type scale, its muted description and its trailing-edge
 * layout can actually be seen — and the only place dark-mode correctness is
 * checkable.
 *
 * PageHeader is not interactive, so no story carries a `play`. The assertions
 * about markup and class strings that a screenshot cannot make live in
 * `page-header.test.tsx`.
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

/**
 * The wallow-web apps route's CTA, spelled with the tokens it already uses. A
 * plain anchor rather than a catalog Button: the actions slot takes any node,
 * and the story frames the slot rather than the thing inside it.
 */
function RegisterAppLink(): ReactElement {
  return (
    <a
      className="bg-primary text-primary-foreground rounded-full px-6 py-2.5 text-sm font-medium no-underline"
      href="/dashboard/apps/register"
    >
      Register New App
    </a>
  );
}

const meta = {
  title: "Components/PageHeader",
  component: PageHeader,
  args: { title: "My Apps" },
} satisfies Meta<typeof PageHeader>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Title only — the bare heading block. */
export const TitleOnly: Story = { decorators: [lightScheme] };

/** Title only, dark scheme. */
export const TitleOnlyDark: Story = { decorators: [darkScheme] };

/** Title plus the muted supporting line under it. */
export const WithDescription: Story = {
  decorators: [lightScheme],
  args: { description: "Every application you have registered with this tenant." },
};

/** Title plus description, dark scheme — the description must stay legible. */
export const WithDescriptionDark: Story = {
  decorators: [darkScheme],
  args: { description: "Every application you have registered with this tenant." },
};

/** Title plus a trailing-edge action — the wallow-web apps route's shape today. */
export const WithActions: Story = {
  decorators: [lightScheme],
  args: { actions: <RegisterAppLink /> },
};

/** All three parts at once: title, description and the trailing action. */
export const WithDescriptionAndActions: Story = {
  decorators: [lightScheme],
  args: {
    description: "Every application you have registered with this tenant.",
    actions: <RegisterAppLink />,
  },
};

/** All three parts in the dark scheme. */
export const WithDescriptionAndActionsDark: Story = {
  decorators: [darkScheme],
  args: {
    description: "Every application you have registered with this tenant.",
    actions: <RegisterAppLink />,
  },
};

/**
 * A long title beside a short action: the leading column wraps and the actions
 * slot must not shrink with it.
 */
export const LongTitle: Story = {
  decorators: [lightScheme],
  args: {
    title: "Organizations you administer across every region",
    description: "Membership, roles and billing contacts for each one.",
    actions: <RegisterAppLink />,
    className: "max-w-2xl",
  },
};
