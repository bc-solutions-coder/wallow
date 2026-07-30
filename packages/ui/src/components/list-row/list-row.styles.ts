import { cva, type VariantProps } from "class-variance-authority";

/*
 * The list row's class recipe. Style decisions live here and nowhere else —
 * this file holds no JSX and imports no React. Every utility is a semantic token
 * class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * Two deliberate departures from the string `OrganizationList` and `AppList`
 * hand-roll today:
 *
 *   1. `hover:bg-background/50` becomes `hover:bg-muted`. An opacity suffix on a
 *      colour is exactly the spelling this epic erases — `muted` is the semantic
 *      token that already means "subtle surface", and unlike a half-transparent
 *      page background it stays correct in dark mode.
 *   2. The row gains the catalog focus indicator (`outline-none` plus
 *      `focus-visible:ring-2 focus-visible:ring-ring`, the form Button and
 *      Toolbar carry). A row is about to become focusable: composing a router
 *      `Link` onto it through `render` makes the whole row a tab stop, and a
 *      keyboard user has to see which row they are on.
 *
 * `motion-safe:` gates the colour transition on the reduced-motion preference,
 * so the hover treatment costs nothing to a reader who asked for stillness.
 */
export const listRowRecipe = cva(
  "flex items-center justify-between px-6 py-4 outline-none motion-safe:transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring",
);

/** The row recipe's variant props, mixed into `ListRowProps`. */
export type ListRowRecipeProps = VariantProps<typeof listRowRecipe>;
