import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect } from "storybook/test";

import { MutedText } from "../muted-text/muted-text";
import { Text } from "../text/text";
import { Card, CardTitle } from "./card";
import { CardHeader } from "./card-header";

/*
 * The visual half of Card's spec (Wallow-m5aq.2.13). card.test.tsx makes the
 * markup assertions a screenshot cannot; these stories cover the states a
 * reviewer needs to eyeball — the default rhythm, each measured `spacing`
 * outlier, and the heading in place. Card is not interactive, so the only story
 * carrying a `play` is `HeadingScale`, which MEASURES rather than shows.
 */

const meta = {
  title: "Components/Card",
  component: Card,
  args: {
    children: (
      <>
        <CardTitle>Sign in</CardTitle>
        <p className="text-sm text-muted-foreground">Use your work account to continue.</p>
      </>
    ),
  },
} satisfies Meta<typeof Card>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The dominant recipe: `p-6 space-y-6`, 14x across wallow-auth. */
export const Default: Story = {};

/** The LoginScreen outlier: a tighter vertical rhythm. */
export const TightSpacing: Story = {
  args: { spacing: "p-6 space-y-4" },
};

/** The RegisterForm outlier: padding only, no rhythm block. */
export const PaddingOnly: Story = {
  args: { spacing: "p-6" },
};

/** The wallow-web call site: a roomier `spacing` plus an additive className. */
export const RoomyWithShadow: Story = {
  args: { spacing: "p-8 space-y-6", className: "shadow-sm" },
};

/** A caller className overriding a recipe utility — the refit's `cn()` at work. */
export const SquareCorners: Story = {
  args: { className: "rounded-none" },
};

/** The heading on its own, so its recipe can be reviewed in isolation. */
export const TitleOnly: Story = {
  args: { children: <CardTitle>Account settings</CardTitle> },
};

/**
 * The shape 11 wallow-auth screens open with, now one component: the heading
 * and its supporting line at the `space-y-1` rhythm, above the card's body.
 */
export const WithHeader: Story = {
  args: {
    children: (
      <>
        <CardHeader title="Create an account" description="Enter your details to get started." />
        <MutedText>Form fields go here.</MutedText>
      </>
    ),
  },
};

/**
 * The description omitted — ErrorPage and the MFA screens ship a bare heading.
 * Worth eyeballing because the fix is an absent `<p>`, not an empty one: an
 * empty paragraph would leave the card's rhythm gap visible here.
 */
export const HeaderWithoutDescription: Story = {
  args: { children: <CardHeader title="Something went wrong" /> },
};

/** RegisterForm's centred heading — a caller className over the rhythm. */
export const CenteredHeader: Story = {
  args: {
    children: (
      <CardHeader
        title="Create an account"
        description="Enter your details to get started."
        className="text-center"
      />
    ),
  },
};

/**
 * The MEASURED pin on the catalog-wide heading standard (Wallow-io5f).
 *
 * THE CLAIM. `CardTitle` and `Text`'s `subheading` step are the two ways this
 * catalog spells a card heading, and they are now ONE size: 20px, the `text-xl`
 * step. Before this bead `cardTitleRecipe` hard-coded `text-lg` (18px) while
 * `subheading` sat at `text-xl` (20px), so the same slot rendered at two sizes
 * depending on which part a call site reached for.
 *
 * WHY 20px RATHER THAN 16px. 16px is the browser's default body size, so a 16px
 * heading computes the SAME size as the copy beneath it and the hierarchy rests
 * entirely on weight and colour. `text-xl` keeps a heading one real step above
 * body text. The relation — not the number — is what is asserted below.
 *
 * WHY THIS IS A STORY AND NOT `card.test.tsx`. This has to read a COMPUTED
 * font-size, and in `packages/ui` only the `storybook` Vitest project has the
 * Tailwind pipeline and the fork theme attached; the `browser` project loads no
 * CSS at all, so every probe there would compute the same inherited size and
 * every assertion below would pass for the wrong reason.
 *
 * WHY MEASURED AND NOT `toHaveClass("text-xl")`. The live class list is the
 * `twMerge` of the recipe with whatever `className` a call site passed, so a
 * caller utility can win the size axis the recipe thought it owned while a
 * class-string assertion stays green over the wrong box. That blindness is the
 * one this epic kept paying for.
 *
 * WHY PROBES RATHER THAN A `20px` LITERAL. Asserting the number would bake
 * Tailwind's current `text-xl` into the catalog's own spec and go quietly wrong
 * for a fork that retunes its scale. Each claim is a comparison against a
 * sibling carrying the utility the heading is supposed to land on, and the
 * probes double as the vacuity guard.
 */
export const HeadingScale: Story = {
  args: {
    children: (
      <>
        <CardTitle data-testid="scale-card-title">Account settings</CardTitle>
        <Text as="h2" variant="subheading" color="onCard" data-testid="scale-subheading">
          Account settings
        </Text>
        <div data-testid="scale-probe-base" className="text-base">
          probe
        </div>
        <div data-testid="scale-probe-lg" className="text-lg">
          probe
        </div>
        <div data-testid="scale-probe-xl" className="text-xl">
          probe
        </div>
      </>
    ),
  },
  play: async ({ canvasElement }) => {
    const base: string = fontSize(canvasElement, "scale-probe-base");
    const lg: string = fontSize(canvasElement, "scale-probe-lg");
    const xl: string = fontSize(canvasElement, "scale-probe-xl");

    // The vacuity guard for everything below. With no stylesheet all three
    // probes compute the inherited size, and "the heading matches text-xl"
    // would also be "the heading matches text-lg".
    await expect(
      new Set([base, lg, xl]),
      "the Tailwind pipeline resolved all three steps",
    ).toHaveProperty("size", 3);

    const title: string = fontSize(canvasElement, "scale-card-title");
    const subheading: string = fontSize(canvasElement, "scale-subheading");

    // The premise the 20px standard rests on, pinned rather than assumed: the
    // `subheading` step is ALREADY `text-xl`, which is why adopting 20px moves
    // no `Text` consumer at all. If this ever stops holding, the standard has
    // silently changed underneath every consumer that spells it that way.
    await expect(subheading, "Text's subheading step is the text-xl step").toBe(xl);

    // The bead's actual claim, in both directions: the card heading lands on
    // that same step, and the two spellings therefore agree.
    await expect(title, "CardTitle renders at the text-xl step").toBe(xl);
    await expect(title, "CardTitle and Text's subheading are one size").toBe(subheading);

    // Stated as an explicit absence because it is the regression, not a
    // restatement: `text-lg` is where `cardTitleRecipe` used to sit.
    await expect(title, "no card heading left at text-lg").not.toBe(lg);
  },
};

/**
 * The computed `font-size` of the story element carrying `testId`.
 *
 * Throws rather than returning a default when the element is missing: a probe
 * that failed to render would otherwise make its comparison trivially true.
 */
function fontSize(canvasElement: HTMLElement, testId: string): string {
  const element: HTMLElement | null = canvasElement.querySelector<HTMLElement>(
    `[data-testid="${testId}"]`,
  );

  if (element === null) {
    throw new Error(`the story did not render [data-testid="${testId}"]`);
  }

  return globalThis.getComputedStyle(element).getPropertyValue("font-size");
}
