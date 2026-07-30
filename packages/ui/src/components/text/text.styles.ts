import { cva, type VariantProps } from "class-variance-authority";

/*
 * The text primitive's class recipe. Style decisions live here and nowhere else
 * — this file holds no JSX and imports no React. Every utility is a semantic
 * token class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * Two constraints this recipe must keep satisfying:
 *
 *   1. NO `/NN` opacity suffix on any colour utility. Erasing the apps'
 *      hand-rolled `text-foreground/60` is the entire point of the component,
 *      so every `color` value maps onto exactly one semantic token.
 *   2. The base string stays EMPTY and `bodySm` stays `text-sm` alone, so
 *      `bodySm` + `muted` resolves to exactly `text-sm text-muted-foreground` —
 *      MutedText's byte-exact contract, which Wallow-lrlm.2.2 reroutes onto Text.
 */

/**
 * The type scale, the semantic colour, and the optional weight/alignment
 * overrides. `weight` and `align` are declared after `variant` so their
 * utilities land later in the class string and tailwind-merge collapses the
 * scale's own `font-*` in their favour.
 */
export const textRecipe = cva("", {
  variants: {
    variant: {
      display: "text-4xl font-bold tracking-tight",
      title: "text-3xl font-bold tracking-tight",
      heading: "text-2xl font-semibold",
      subheading: "text-xl font-semibold",
      body: "text-base",
      bodySm: "text-sm",
      caption: "text-xs",
      overline: "text-xs font-semibold uppercase tracking-wider",
      code: "font-mono text-sm",
    },
    color: {
      default: "text-foreground",
      muted: "text-muted-foreground",
      primary: "text-primary",
      accent: "text-accent-foreground",
      destructive: "text-destructive",
      success: "text-success",
      onSidebar: "text-sidebar-foreground",
      onCard: "text-card-foreground",
      onPrimary: "text-primary-foreground",
    },
    weight: {
      normal: "font-normal",
      medium: "font-medium",
      semibold: "font-semibold",
      bold: "font-bold",
    },
    align: {
      left: "text-left",
      center: "text-center",
      right: "text-right",
    },
  },
  defaultVariants: { variant: "body", color: "default" },
});

/** The recipe's variant props, mixed into `TextProps`. */
export type TextRecipeProps = VariantProps<typeof textRecipe>;
