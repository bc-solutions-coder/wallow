import { cva, type VariantProps } from "class-variance-authority";

/**
 * The form's class recipe. Style decisions live here and nowhere else — this
 * file holds no JSX and imports no React, so a recipe can be read (and diffed)
 * without the component around it.
 *
 * A form is pure layout: it stacks the fields it wraps. `space-y-4` is the
 * spacing every hand-written `<form>` in wallow-auth already uses (and the same
 * rhythm `fieldsetRootRecipe` sets), so adopting `<Form>` at those call sites is
 * a drop-in rather than a visual change.
 *
 * Base UI's `Form` publishes no state of its own — its `State` is an empty
 * object and the rendered element carries no `data-*` attributes — so unlike the
 * rest of the catalog there is no state treatment to hang here. Field state
 * lives on the fields beneath it, which is where `field.styles.ts` styles it.
 */
export const formRecipe = cva("space-y-4");

/** The recipe's variant props, mixed into `FormProps`. */
export type FormRecipeProps = VariantProps<typeof formRecipe>;
