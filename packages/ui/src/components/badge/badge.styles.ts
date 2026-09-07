import { cva, type VariantProps } from "class-variance-authority";

/**
 * Pill classes with neutral, success, warning, or destructive background and foreground pairs.
 * Defaults to neutral.
 */
export const badgeRecipe = cva("inline-block text-xs font-medium px-2.5 py-0.5 rounded-full", {
  variants: {
    variant: {
      neutral: "bg-accent text-accent-foreground",
      success: "bg-success text-success-foreground",
      warning: "bg-warning text-warning-foreground",
      destructive: "bg-destructive text-destructive-foreground",
    },
  },
  defaultVariants: { variant: "neutral" },
});

/** The recipe's variant props, mixed into `BadgeProps`. */
export type BadgeRecipeProps = VariantProps<typeof badgeRecipe>;
