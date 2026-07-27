import { cva, type VariantProps } from "class-variance-authority";

/**
 * The button's class recipe. Style decisions live here and nowhere else — this
 * file holds no JSX and imports no React, so a recipe can be read (and diffed)
 * without the component around it.
 *
 * Every utility is a semantic token class from `@bc-solutions-coder/styles`; no
 * raw colour values. The disabled treatment hangs off Base UI's `data-disabled`
 * state attribute rather than the `:disabled` pseudo-class, so it still applies
 * when the caller composes the button onto a non-button element via `render`.
 */
export const buttonRecipe = cva(
  "inline-flex w-full items-center justify-center rounded-md px-3 py-2 text-sm font-medium data-[disabled]:opacity-50",
  {
    variants: {
      variant: {
        primary: "bg-primary text-primary-foreground",
        secondary: "bg-secondary text-secondary-foreground",
        destructive: "bg-destructive text-destructive-foreground",
      },
    },
    defaultVariants: { variant: "primary" },
  },
);

/** The recipe's variant props, mixed into `ButtonProps`. */
export type ButtonRecipeProps = VariantProps<typeof buttonRecipe>;
