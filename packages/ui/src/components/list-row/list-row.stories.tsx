import type { Decorator, Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { expect } from "storybook/test";

import { ListCard } from "../list-card/list-card";
import { expectScheme } from "../../../.storybook/scheme-assertions";
import { darkScheme, lightScheme } from "../../../.storybook/scheme-decorators";
import { ListRow } from "./list-row";

/*
 * Wallow-lrlm.3.5 — ListRow stories. Each export becomes a Vitest test case in
 * the same headless Chromium the `browser` project uses, but with the real
 * Tailwind pipeline and the fork's real theme attached, which is the only place
 * the row's hover and focus treatments render at all.
 *
 * Every story is framed inside a real `ListCard`: a row is a `<li>`, and the
 * hairline dividers, the clipped corners and the full-bleed `px-6` cells only
 * mean anything against the surface they were extracted from.
 *
 * `AsLink` carries a `play` because the focus indicator is the one state a
 * static screenshot cannot reach, and it is the state that matters most here —
 * F4.T1 turns these rows into TanStack Router `Link`s, and a keyboard user has
 * to see which row they are on.
 *
 * The scheme comes from the shared `lightScheme`/`darkScheme` decorators, which
 * stamp the class on `document.documentElement` and remove it again on unmount.
 * A wrapper `<div className="dark">` cannot select a scheme at all — see
 * `.storybook/scheme-decorators.tsx` for why, and never reintroduce one.
 */

/** Frames the row in the surface it belongs to — a card-wrapped `<ul>`. */
const inListCard: Decorator = (Story) => (
  <ListCard name="organizations">
    <Story />
  </ListCard>
);

/** One organization row's cells, spelled with the tokens the app uses today. */
function OrganizationCells(): ReactElement {
  return (
    <>
      <span className="text-sm font-medium text-card-foreground">Acme Corporation</span>
      <span className="text-sm text-muted-foreground font-mono">acme.example</span>
      <span className="inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full">
        12
      </span>
    </>
  );
}

const meta = {
  title: "Components/ListRow",
  component: ListRow,
  decorators: [inListCard],
  args: {
    name: "organization",
    children: <OrganizationCells />,
  },
} satisfies Meta<typeof ListRow>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The shipped row: a full-bleed cell with its content pushed to both edges. */
export const Default: Story = { decorators: [lightScheme], play: expectScheme("light") };

/** The same row in the dark scheme. */
export const Dark: Story = { decorators: [darkScheme], play: expectScheme("dark") };

/**
 * The row composed onto an anchor — what F4.T1 does with a router `Link`. The
 * whole row becomes the navigation target, so the focus ring frames the row.
 */
export const AsLink: Story = {
  decorators: [lightScheme],
  args: { render: <a href="#acme" /> },
  play: async ({ canvas }) => {
    const row = canvas.getByTestId("organization-item");
    row.focus();

    await expect(row).toHaveFocus();
  },
};

/** The composed row in the dark scheme. */
export const AsLinkDark: Story = {
  decorators: [darkScheme],
  play: expectScheme("dark"),
  args: { render: <a href="#acme" /> },
};

/** A caller tightening the row's rhythm — `className` wins over the recipe. */
export const CompactPadding: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: { className: "py-2" },
};
