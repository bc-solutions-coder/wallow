import type { Meta, StoryObj } from "@storybook/react-vite";

import { BrandedHeader } from "./branded-header";

/*
 * The visual half of BrandedHeader's spec. Not interactive, so no `play`. The
 * stories cover the auth screens' page header (fork and client-branded shapes)
 * and the compact card variant the branding editor previews with.
 */

const meta = {
  title: "Components/BrandedHeader",
  component: BrandedHeader,
  args: {
    name: "Acme Dashboard",
  },
} satisfies Meta<typeof BrandedHeader>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The bare page header: a name and nothing else, the fork-with-no-tagline shape. */
export const Default: Story = {};

/** A third-party client's full header, attributed to its owning organization. */
export const ClientBranded: Story = {
  args: {
    tagline: "Ship faster",
    organizationName: "Acme Corp",
  },
};

/** The fork's own header: name and tagline, no organization attribution. */
export const ForkBranded: Story = {
  args: {
    name: "Wallow",
    tagline: "Wallow in it",
  },
};

/** The embeddable card shape: span heading, compact logo, caller-owned wrapper. */
export const CardVariant: Story = {
  args: {
    variant: "card",
    tagline: "Ship faster",
  },
};
