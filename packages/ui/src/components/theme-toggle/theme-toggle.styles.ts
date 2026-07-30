import { cva, type VariantProps } from "class-variance-authority";

/**
 * The theme toggle's class recipe. Style decisions live here and nowhere else —
 * this file holds no JSX and imports no React, so a recipe can be read (and
 * diffed) without the component around it.
 *
 * Every utility is a semantic token class from `@bc-solutions-coder/styles`; no
 * raw colour values. The toggle rides on `../button`'s `secondary` variant for
 * its box and colours, so this recipe only contributes what the button does not:
 * `w-auto`, which NARROWS the button recipe's `w-full` (a full-bleed control
 * would be wrong in both a nav rail and an auth card), plus the gap and
 * no-wrap the three-state label needs.
 */
export const themeToggleRecipe = cva("w-auto gap-2 whitespace-nowrap");

/** The recipe's variant props, mixed into `ThemeToggleProps`. */
export type ThemeToggleRecipeProps = VariantProps<typeof themeToggleRecipe>;
