import type { Decorator, Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";

import { ListRow } from "../list-row/list-row";
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
 * ListCard is not interactive, so no story carries a `play`. The assertions
 * about markup and class strings that a screenshot cannot make live in
 * `list-card.test.tsx`.
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
export const Default: Story = { decorators: [lightScheme] };

/** The same list in the dark scheme — the hairlines must stay visible. */
export const Dark: Story = { decorators: [darkScheme] };

/** A single row: the divider must not draw above the first or below the last. */
export const SingleRow: Story = {
  decorators: [lightScheme],
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
