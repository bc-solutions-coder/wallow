import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";

import { expectScheme } from "../../../.storybook/scheme-assertions";
import { darkScheme, lightScheme } from "../../../.storybook/scheme-decorators";
import { Text, type TextAs, type TextProps } from "./text";

/*
 * Wallow-lrlm.2.1 — Text stories. Each export becomes a Vitest test case in the
 * same headless Chromium the `browser` project uses, but with the real Tailwind
 * pipeline and the fork's real theme attached (.storybook/preview.css +
 * preview.tsx). Since the `browser` project loads no Tailwind at all, this is the
 * ONLY place Text's type scale and its semantic colours can actually be seen —
 * and the only place dark-mode colour correctness is checkable. Every axis is
 * therefore rendered TWICE, once per scheme.
 *
 * Text is not interactive beyond its scheme, so the only `play` a story carries
 * is `expectScheme`, which measures that the scheme it claims is the scheme it
 * paints. The assertions about markup and class strings that a screenshot cannot
 * make live in `text.test.tsx`.
 *
 * The scheme comes from the shared `lightScheme`/`darkScheme` decorators, which
 * stamp the class on `document.documentElement` and remove it again on unmount.
 * A wrapper `<div className="dark">` cannot select a scheme at all — see
 * `.storybook/scheme-decorators.tsx` for why, and never reintroduce one.
 */

/** Every type scale, with the sample text naming the scale it renders. */
const VARIANTS: NonNullable<TextProps["variant"]>[] = [
  "display",
  "title",
  "heading",
  "subheading",
  "body",
  "bodySm",
  "caption",
  "overline",
  "code",
];

/**
 * Every semantic colour paired with the surface it is named for. The `on*`
 * colours are meaningless floating on the page background — `onPrimary` is only
 * legible on `bg-primary` — so each is framed by its own surface here.
 */
const COLOURS: [NonNullable<TextProps["color"]>, string][] = [
  ["default", ""],
  ["muted", ""],
  ["primary", ""],
  ["accent", ""],
  ["destructive", ""],
  ["success", ""],
  ["onSidebar", "bg-sidebar rounded-md px-3 py-2"],
  ["onCard", "bg-card rounded-md px-3 py-2"],
  ["onPrimary", "bg-primary rounded-md px-3 py-2"],
];

/** Every element `as` accepts, beside the scale it derives on its own. */
const ELEMENTS: TextAs[] = [
  "h1",
  "h2",
  "h3",
  "h4",
  "h5",
  "h6",
  "p",
  "span",
  "div",
  "label",
  "legend",
  "code",
];

function TypeScaleSheet(): ReactElement {
  return (
    <div className="flex flex-col gap-3">
      {VARIANTS.map((variant) => (
        <Text key={variant} variant={variant}>
          {variant} — Wallow ships a single text primitive
        </Text>
      ))}
    </div>
  );
}

function ColourSheet(): ReactElement {
  return (
    <div className="flex flex-col gap-2">
      {COLOURS.map(([color, surface]) => (
        <div key={color} className={surface}>
          <Text color={color}>{color} — the quick brown fox jumps over the lazy dog</Text>
        </div>
      ))}
    </div>
  );
}

function ElementSheet(): ReactElement {
  return (
    <div className="flex flex-col gap-3">
      {ELEMENTS.map((as) => (
        <Text key={as} as={as}>
          {`<${as}> renders its own default scale`}
        </Text>
      ))}
    </div>
  );
}

const meta = {
  title: "Components/Text",
  component: Text,
  args: { children: "Wallow ships a single text primitive." },
} satisfies Meta<typeof Text>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The bare default: a `<p>` at the body scale in the foreground colour. */
export const Default: Story = { decorators: [lightScheme], play: expectScheme("light") };

/** The same default in the dark scheme. */
export const DefaultDark: Story = { decorators: [darkScheme], play: expectScheme("dark") };

/** Every type scale, top to bottom. */
export const TypeScale: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  render: () => <TypeScaleSheet />,
};

/** Every type scale in the dark scheme — the scales must not shift. */
export const TypeScaleDark: Story = {
  decorators: [darkScheme],
  play: expectScheme("dark"),
  render: () => <TypeScaleSheet />,
};

/** Every semantic colour on the surface it is named for. */
export const Colours: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  render: () => <ColourSheet />,
};

/**
 * The same colours in the dark scheme. This is the story that makes dark mode
 * checkable: every value must stay legible on its own surface without any
 * app-level override.
 */
export const ColoursDark: Story = {
  decorators: [darkScheme],
  play: expectScheme("dark"),
  render: () => <ColourSheet />,
};

/** The as-derived defaults: `as` alone picks both the element and its scale. */
export const SemanticElements: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  render: () => <ElementSheet />,
};

/** The as-derived defaults in the dark scheme. */
export const SemanticElementsDark: Story = {
  decorators: [darkScheme],
  play: expectScheme("dark"),
  render: () => <ElementSheet />,
};

/** A heading level decoupled from its visual weight — the `variant` override. */
export const HeadingAsBodyCopy: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: { as: "h2", variant: "body", children: "An <h2> that reads as body copy." },
};

/** The optional weight override, applied over the display scale. */
export const WeightOverrides: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  render: () => (
    <div className="flex flex-col gap-2">
      <Text variant="display" weight="normal">
        display / normal
      </Text>
      <Text variant="display" weight="medium">
        display / medium
      </Text>
      <Text variant="display" weight="semibold">
        display / semibold
      </Text>
      <Text variant="display" weight="bold">
        display / bold
      </Text>
    </div>
  ),
};

/** The optional alignment prop. */
export const Alignments: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  render: () => (
    <div className="flex w-96 flex-col gap-2">
      <Text align="left">left</Text>
      <Text align="center">center</Text>
      <Text align="right">right</Text>
    </div>
  ),
};

/**
 * The overline treatment two wallow-web settings sections hand-roll today as
 * `text-xs font-semibold text-foreground/70 uppercase tracking-wider mb-1` — here
 * with a real colour instead of the opacity, and the spacing left to the caller.
 */
export const Overline: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: { as: "span", variant: "overline", color: "muted", children: "Email address" },
};

/** A caller className overriding the recipe's colour — `cn()` at work. */
export const OverriddenColour: Story = {
  decorators: [lightScheme],
  play: expectScheme("light"),
  args: { color: "muted", className: "text-destructive", children: "That code has expired." },
};
