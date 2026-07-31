import { cva, type VariantProps } from "class-variance-authority";

/*
 * The card's class recipes. Style decisions live here and nowhere else — this
 * file holds no JSX and imports no React. Every utility is a semantic token
 * class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * Neither part takes a cva VARIANT. The card's one axis of variation, the
 * padding/vertical-rhythm block, stays the free-form `spacing` PROP it already
 * is (call sites pass `p-6 space-y-4`, `p-6`, `p-8 space-y-6`), which a cva
 * variant enum cannot express without breaking those callers. `spacing` is
 * merged over the recipe by `cn()`, so it keeps winning over any padding the
 * recipe holds. The `VariantProps` types are still exported so the parts keep
 * the catalog-wide shape and a later variant axis is a non-breaking addition.
 */

/** The card surface — the outer `<div>`, minus the `spacing` block. */
export const cardRecipe = cva("rounded-lg border border-border bg-card");

/** The surface recipe's variant props, mixed into `CardProps`. */
export type CardRecipeProps = VariantProps<typeof cardRecipe>;

/** The card heading — the `<h2>` rendered by `CardTitle`. */
export const cardTitleRecipe = cva("text-xl font-semibold text-card-foreground");

/** The heading recipe's variant props, mixed into `CardTitleProps`. */
export type CardTitleRecipeProps = VariantProps<typeof cardTitleRecipe>;
