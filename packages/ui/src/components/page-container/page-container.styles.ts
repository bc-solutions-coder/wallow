import { cva, type VariantProps } from "class-variance-authority";

/*
 * The page container's class recipe. Style decisions live here and nowhere else
 * — this file holds no JSX and imports no React. Every utility is a semantic
 * token class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * One part, one public component: the centred column a page body sits in. The
 * outer shell (nav, main column, padding) belongs to the app's layout route, so
 * this recipe carries width and centring alone.
 */

/**
 * The centred column. `5xl` is the width a list page with a table needs; a
 * narrower column forces per-page overrides, which is the drift this recipe
 * exists to remove.
 */
export const pageContainerRecipe = cva("max-w-5xl mx-auto");

/** The recipe's variant props, mixed into `PageContainerProps`. */
export type PageContainerRecipeProps = VariantProps<typeof pageContainerRecipe>;
