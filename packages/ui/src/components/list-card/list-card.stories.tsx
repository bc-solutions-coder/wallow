import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";

import { ListRow } from "../list-row/list-row";
import { expectScheme } from "../../../.storybook/scheme-assertions";
import { darkScheme, lightScheme } from "../../../.storybook/scheme-decorators";
import { ListCard } from "./list-card";

/*
 * Wallow-lrlm.3.5 — ListCard stories. Each export becomes a Vitest test case in
 * the same headless Chromium the `browser` project uses, but with the real
 * Tailwind pipeline and the fork's real theme attached (.storybook/preview.css +
 * preview.tsx). Since the `browser` project loads no Tailwind, this is the only
 * place the card's clipped corners, its row hairlines and the rows' hover
 * treatment can actually be seen — and the only place dark-mode correctness is
 * checkable, which matters here: the row hover moved off `bg-background/50` onto
 * the `muted` token precisely so it reads correctly in both schemes.
 *
 * ListCard is not interactive beyond its scheme, so the only `play` a story
 * carries is `expectScheme`, which measures that the scheme it claims is the
 * scheme it paints. The assertions about markup and class strings that a
 * screenshot cannot make live in `list-card.test.tsx`.
 *
 * The scheme comes from the shared `lightScheme`/`darkScheme` decorators, which
 * stamp the class on `document.documentElement` and remove it again on unmount.
 * A wrapper `<div className="dark">` cannot select a scheme at all — see
 * `.storybook/scheme-decorators.tsx` for why, and never reintroduce one.
 */

/** One organization row's cells, spelled with the tokens the app uses today. */
function OrganizationCells({ name, members }: { name: string; members: number }): ReactElement {
  return (
    <>
      <span className="text-sm font-medium text-card-foreground">{name}</span>
      <span className="inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full">
        {members}
      </span>
    </>
  );
}

const meta = {
  title: "Components/ListCard",
  component: ListCard,
  args: {
    name: "organizations",
    children: (
      <>
        <ListRow name="organization">
          <OrganizationCells name="Acme Corporation" members={12} />
        </ListRow>
        <ListRow name="organization">
          <OrganizationCells name="Globex" members={4} />
        </ListRow>
        <ListRow name="organization">
          <OrganizationCells name="Initech" members={31} />
        </ListRow>
      </>
    ),
  },
} satisfies Meta<typeof ListCard>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The shipped organizations list: a clipped card of hairline-divided rows. */
export const Default: Story = { decorators: [lightScheme], play: expectScheme("light") };

/** The same list in the dark scheme — the hairlines must stay visible. */
export const Dark: Story = { decorators: [darkScheme], play: expectScheme("dark") };

/** A single row: the divider must not draw above the first or below the last. */
export const SingleRow: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: {
    children: (
      <ListRow name="organization">
        <OrganizationCells name="Acme Corporation" members={12} />
      </ListRow>
    ),
  },
};

/** Rows composed onto anchors — the navigable list F4.T1 builds. */
export const NavigableRows: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: {
    children: (
      <>
        <ListRow name="organization" render={<a href="#acme" />}>
          <OrganizationCells name="Acme Corporation" members={12} />
        </ListRow>
        <ListRow name="organization" render={<a href="#globex" />}>
          <OrganizationCells name="Globex" members={4} />
        </ListRow>
      </>
    ),
  },
};

/** The apps list: the same shape under a different name. */
export const AppsList: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: {
    name: "apps",
    children: (
      <>
        <ListRow name="app">
          <span className="text-sm font-medium text-card-foreground">Wallow Web</span>
          <span className="inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full">
            Public
          </span>
        </ListRow>
        <ListRow name="app">
          <span className="text-sm font-medium text-card-foreground">Wallow CLI</span>
          <span className="inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full">
            Confidential
          </span>
        </ListRow>
      </>
    ),
  },
};
