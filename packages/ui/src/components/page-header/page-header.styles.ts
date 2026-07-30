import { cva, type VariantProps } from "class-variance-authority";

/*
 * The page header's class recipes. Style decisions live here and nowhere else —
 * this file holds no JSX and imports no React. Every utility is a semantic token
 * class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * Three parts, one public component: the header row, the title/description
 * column at its leading edge, and the actions slot at its trailing edge. Only
 * the row is caller-styleable; the two inner recipes stay sealed.
 *
 * No type-scale or colour utility appears here: the title and the description
 * are rendered through `Text`, so the scale and the semantic colour stay that
 * component's decision. These recipes carry layout alone.
 */

/**
 * The header row — the outer `<div>` the caller's props land on. `items-start`
 * keeps the actions aligned to the top of a title that wraps to two lines, and
 * `mb-8` is the page rhythm both wallow-web list routes hand-roll today.
 */
export const pageHeaderRecipe = cva("flex items-start justify-between gap-4 mb-8");

/** The row recipe's variant props, mixed into `PageHeaderProps`. */
export type PageHeaderRecipeProps = VariantProps<typeof pageHeaderRecipe>;

/** The title/description column at the header's leading edge. */
export const pageHeaderTitleGroupRecipe = cva("flex flex-col gap-1");

/** The title-group recipe's variant props. */
export type PageHeaderTitleGroupRecipeProps = VariantProps<typeof pageHeaderTitleGroupRecipe>;

/**
 * The actions slot at the header's trailing edge. `shrink-0` is load-bearing:
 * a long title must wrap the leading column rather than squeeze the CTA.
 */
export const pageHeaderActionsRecipe = cva("flex items-center gap-3 shrink-0");

/** The actions recipe's variant props. */
export type PageHeaderActionsRecipeProps = VariantProps<typeof pageHeaderActionsRecipe>;
