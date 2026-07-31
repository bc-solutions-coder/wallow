import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the dialog. The class lists are not invented
 * here — dialog.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set through the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * One recipe per part that renders a VISIBLE element. `Root` renders no element,
 * and `Portal` renders only the structural container Base UI appends to `<body>`,
 * so neither has a recipe (see dialog.tsx for why they are re-exported unwrapped).
 *
 * No recipe takes a cva VARIANT. A dialog has no visual variant axis in this
 * catalog: open/closed and the entering/exiting transition phases are all
 * STATES, and Base UI publishes states as `data-*` attributes, so they belong in
 * the base string as `data-[starting-style]:` / `data-[ending-style]:` /
 * `data-[disabled]:` modifiers rather than as cva variants nobody would pass by
 * hand. The `VariantProps` types are still exported so each part's props keep the
 * catalog-wide shape and a later variant axis stays a non-breaking addition.
 */

/**
 * The button that opens the dialog — Base UI's `Dialog.Trigger`, a `<button>`.
 * Deliberately COLOURLESS: a trigger is routinely composed onto a real `Button`
 * through Base UI's `render` prop, and a `bg-*` here would be merged away by
 * tailwind-merge and silently beat the Button's own background.
 */
export const dialogTriggerRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium transition-colors data-[disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `DialogTriggerProps`. */
export type DialogTriggerRecipeProps = VariantProps<typeof dialogTriggerRecipe>;

/**
 * The dimming scrim behind an open dialog — Base UI's `Dialog.Backdrop`, a
 * `<div>`. The two `data-[…-style]:opacity-0` modifiers are the ONLY place the
 * enter and exit phases are expressed: Base UI sets `data-starting-style` and
 * `data-ending-style` for the duration of the transition and removes them again.
 */
export const dialogBackdropRecipe = cva(
  "fixed inset-0 z-50 bg-foreground/50 transition-opacity duration-150 data-[starting-style]:opacity-0 data-[ending-style]:opacity-0",
);

/** The backdrop recipe's variant props, mixed into `DialogBackdropProps`. */
export type DialogBackdropRecipeProps = VariantProps<typeof dialogBackdropRecipe>;

/**
 * The optional scroll container around the popup — Base UI's `Dialog.Viewport`,
 * a `<div>`. Because the part is OPTIONAL (the popup positions itself, so a
 * dialog works with or without a viewport), this recipe may only add the scroll
 * region and stacking. Layout or centring here would fight the popup's own fixed
 * centring in the anatomy that omits the viewport.
 */
export const dialogViewportRecipe = cva("fixed inset-0 z-50 overflow-y-auto outline-none");

/** The viewport recipe's variant props, mixed into `DialogViewportProps`. */
export type DialogViewportRecipeProps = VariantProps<typeof dialogViewportRecipe>;

/**
 * The dialog card itself — Base UI's `Dialog.Popup`, a `<div role="dialog">`.
 *
 * Base UI positions NOTHING for a dialog (measured: the popup's only inline
 * style is `--nested-dialogs`), so this recipe owns the centring outright. That
 * is the OPPOSITE of every anchored overlay in this catalog — `Select.Positioner`
 * and friends carry Base UI's inline `position`/`transform`, so their recipes
 * must restrict themselves to stacking and focus. Do not copy the centring
 * utilities below onto an anchored popup.
 */
export const dialogPopupRecipe = cva(
  "fixed top-1/2 left-1/2 z-50 w-full max-w-lg -translate-x-1/2 -translate-y-1/2 rounded-lg border border-border bg-popover p-6 text-popover-foreground shadow-lg outline-none transition-all duration-150 data-[starting-style]:scale-95 data-[starting-style]:opacity-0 data-[ending-style]:scale-95 data-[ending-style]:opacity-0",
);

/** The popup recipe's variant props, mixed into `DialogPopupProps`. */
export type DialogPopupRecipeProps = VariantProps<typeof dialogPopupRecipe>;

/** The heading that names the dialog — Base UI's `Dialog.Title`, an `<h2>`. */
export const dialogTitleRecipe = cva("text-xl font-semibold text-foreground");

/** The title recipe's variant props, mixed into `DialogTitleProps`. */
export type DialogTitleRecipeProps = VariantProps<typeof dialogTitleRecipe>;

/** The supporting copy under the title — Base UI's `Dialog.Description`, a `<p>`. */
export const dialogDescriptionRecipe = cva("mt-2 text-sm text-muted-foreground");

/** The description recipe's variant props, mixed into `DialogDescriptionProps`. */
export type DialogDescriptionRecipeProps = VariantProps<typeof dialogDescriptionRecipe>;

/**
 * The button that dismisses the dialog — Base UI's `Dialog.Close`, a `<button>`.
 * No absolute corner positioning, so a caller is free to put the close in a
 * footer row or in the popup's corner without unpicking the recipe first.
 */
export const dialogCloseRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium text-muted-foreground transition-colors hover:text-foreground data-[disabled]:opacity-50",
);

/** The close recipe's variant props, mixed into `DialogCloseProps`. */
export type DialogCloseRecipeProps = VariantProps<typeof dialogCloseRecipe>;
