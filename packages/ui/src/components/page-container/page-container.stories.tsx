import type { Meta, StoryObj } from "@storybook/react-vite";

import { ListCard } from "../list-card/list-card";
import { MutedText } from "../muted-text/muted-text";
import { PageHeader } from "../page-header/page-header";
import { PageContainer } from "./page-container";

/*
 * The visual half of PageContainer's coverage. A width rule has nothing to look
 * at on its own, so the stories fill the column with the header-plus-body shape
 * every dashboard page has. Not interactive, so no `play`.
 */

const meta = {
  title: "Components/PageContainer",
  component: PageContainer,
  args: {
    children: (
      <>
        <PageHeader title="My Apps" description="Applications registered against this tenant." />
        <ListCard name="page-container-demo">
          <MutedText>Nothing registered yet.</MutedText>
        </ListCard>
      </>
    ),
  },
} satisfies Meta<typeof PageContainer>;

export default meta;

type Story = StoryObj<typeof meta>;

/** A dashboard page body: the shared column, centred in whatever holds it. */
export const Default: Story = {};

/** A caller className overriding the width — the recipe's only escape hatch. */
export const NarrowColumn: Story = {
  args: { className: "max-w-2xl" },
};
