import { cva, type VariantProps } from "class-variance-authority";

/** Text controls fit their label; icon controls use a square touch target. */
export const themeToggleRecipe = cva("gap-2 whitespace-nowrap", {
  variants: {
    presentation: {
      text: "w-auto",
      icon: "size-11 p-0",
    },
  },
  defaultVariants: { presentation: "text" },
});

/** The recipe's variant props, mixed into `ThemeToggleProps`. */
export type ThemeToggleRecipeProps = VariantProps<typeof themeToggleRecipe>;
