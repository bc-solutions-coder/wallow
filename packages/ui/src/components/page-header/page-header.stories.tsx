import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";

import { expectScheme } from "../../../.storybook/scheme-assertions";
import { darkScheme, lightScheme } from "../../../.storybook/scheme-decorators";
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
 * PageHeader is not interactive beyond its scheme, so the only `play` a story
 * carries is `expectScheme`, which measures that the scheme it claims is the
 * scheme it paints. The assertions about markup and class strings that a
 * screenshot cannot make live in `page-header.test.tsx`.
 *
 * The scheme comes from the shared `lightScheme`/`darkScheme` decorators, which
 * stamp the class on `document.documentElement` and remove it again on unmount.
 * A wrapper `<div className="dark">` cannot select a scheme at all — see
 * `.storybook/scheme-decorators.tsx` for why, and never reintroduce one.
 */

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
export const TitleOnly: Story = { decorators: [lightScheme], play: expectScheme("light") };

/** Title only, dark scheme. */
export const TitleOnlyDark: Story = { decorators: [darkScheme], play: expectScheme("dark") };

/** Title plus the muted supporting line under it. */
export const WithDescription: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: { description: "Every application you have registered with this tenant." },
};

/** Title plus description, dark scheme — the description must stay legible. */
export const WithDescriptionDark: Story = {
  decorators: [darkScheme],
  play: expectScheme("dark"),
  args: { description: "Every application you have registered with this tenant." },
};

/** Title plus a trailing-edge action — the wallow-web apps route's shape today. */
export const WithActions: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: { actions: <RegisterAppLink /> },
};

/** All three parts at once: title, description and the trailing action. */
export const WithDescriptionAndActions: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: {
    description: "Every application you have registered with this tenant.",
    actions: <RegisterAppLink />,
  },
};

/** All three parts in the dark scheme. */
export const WithDescriptionAndActionsDark: Story = {
  decorators: [darkScheme],
  play: expectScheme("dark"),
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
  play: expectScheme("light"),
  args: {
    title: "Organizations you administer across every region",
    description: "Membership, roles and billing contacts for each one.",
    actions: <RegisterAppLink />,
    className: "max-w-2xl",
  },
};
