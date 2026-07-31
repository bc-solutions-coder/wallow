import { cva, type VariantProps } from "class-variance-authority";

import { buttonRecipe } from "../button/button.styles";

/*
 * One recipe per styled part of the alert dialog. The class lists are not
 * invented here — alert-dialog.test.tsx declares each part's exact utility set
 * as a top-of-file `*_CLASSES` constant and asserts it as an order-free set
 * through the rendered component, so that spec is the source of truth for
 * everything below.
 *
 * One recipe per part that renders a VISIBLE element. `Root` renders no element,
 * `Portal` renders only the structural container Base UI appends to `<body>`,
 * and `Handle`/`createHandle` render no DOM at all, so none of them has a recipe
 * (see alert-dialog.tsx for why they are re-exported unwrapped).
 *
 * Trigger, backdrop, viewport, title and description are byte-identical to the
 * Dialog exemplar's recipes — same tokens, same reasoning. They are duplicated
 * rather than imported from ../dialog/dialog.styles deliberately: the recipe
 * layer is exactly what a fork restyles, so an alert has to stay independently
 * themable from a dialog even though the two run the same Base UI code
 * underneath. The popup and the close are where the two genuinely diverge.
 *
 * Only the close recipe takes a cva VARIANT. Open/closed and the entering and
 * exiting transition phases are STATES, and Base UI publishes states as `data-*`
 * attributes, so they belong in the base strings as `data-[starting-style]:` /
 * `data-[ending-style]:` / `data-[disabled]:` modifiers rather than as cva
 * variants nobody would pass by hand. The `VariantProps` types are still
 * exported for every part so each one keeps the catalog-wide prop shape and a
 * later variant axis stays a non-breaking addition.
 */

/**
 * The button that opens the alert dialog — Base UI's `AlertDialog.Trigger`, a
 * `<button>`. Deliberately COLOURLESS, on the Dialog exemplar's reasoning: a
 * trigger is routinely composed onto a real `Button` through Base UI's `render`
 * prop, and a `bg-*` here would be merged away by tailwind-merge and silently
 * beat the Button's own background. Note the asymmetry with the close recipe
 * below, which has no other element to compose onto and so is the part that
 * carries the button styling.
 */
export const alertDialogTriggerRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium transition-colors data-[disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `AlertDialogTriggerProps`. */
export type AlertDialogTriggerRecipeProps = VariantProps<typeof alertDialogTriggerRecipe>;

/**
 * The dimming scrim behind an open alert dialog — `AlertDialog.Backdrop`, a
 * `<div>`. The two `data-[…-style]:opacity-0` modifiers are the ONLY place the
 * enter and exit phases are expressed: Base UI sets `data-starting-style` and
 * `data-ending-style` for the duration of the transition and removes them again.
 *
 * Unlike a dialog's, this backdrop is inert to presses — `AlertDialog.Root`
 * forces `disablePointerDismissal` — so it is a scrim and nothing more.
 */
export const alertDialogBackdropRecipe = cva(
  "fixed inset-0 z-50 bg-foreground/50 transition-opacity duration-150 data-[starting-style]:opacity-0 data-[ending-style]:opacity-0",
);

/** The backdrop recipe's variant props, mixed into `AlertDialogBackdropProps`. */
export type AlertDialogBackdropRecipeProps = VariantProps<typeof alertDialogBackdropRecipe>;

/**
 * The optional scroll container around the popup — `AlertDialog.Viewport`, a
 * `<div>`. Because the part is OPTIONAL (the popup positions itself, so an alert
 * dialog works with or without a viewport), this recipe may only add the scroll
 * region and stacking. Layout or centring here would fight the popup's own fixed
 * centring in the anatomy that omits the viewport.
 */
export const alertDialogViewportRecipe = cva("fixed inset-0 z-50 overflow-y-auto outline-none");

/** The viewport recipe's variant props, mixed into `AlertDialogViewportProps`. */
export type AlertDialogViewportRecipeProps = VariantProps<typeof alertDialogViewportRecipe>;

/**
 * The alert card itself — `AlertDialog.Popup`, a `<div role="alertdialog">`.
 *
 * Base UI positions NOTHING for a dialog of either kind (measured: the popup's
 * only inline style is `--nested-dialogs`), so this recipe owns the centring
 * outright. That is the OPPOSITE of every anchored overlay in this catalog —
 * `Popover.Positioner`, `Menu.Positioner`, `Tooltip.Positioner` and friends
 * carry Base UI's inline `position`/`transform`, so their recipes must restrict
 * themselves to stacking and surface. DO NOT COPY the centring utilities below
 * onto an anchored popup.
 *
 * `max-w-md` is the one deliberate divergence from `dialogPopupRecipe`'s
 * `max-w-lg`: an alert dialog holds one question and two buttons, never a form,
 * so it is narrower than a general-purpose dialog.
 */
export const alertDialogPopupRecipe = cva(
  "fixed top-1/2 left-1/2 z-50 w-full max-w-md -translate-x-1/2 -translate-y-1/2 rounded-lg border border-border bg-popover p-6 text-popover-foreground shadow-lg outline-none transition-all duration-150 data-[starting-style]:scale-95 data-[starting-style]:opacity-0 data-[ending-style]:scale-95 data-[ending-style]:opacity-0",
);

/** The popup recipe's variant props, mixed into `AlertDialogPopupProps`. */
export type AlertDialogPopupRecipeProps = VariantProps<typeof alertDialogPopupRecipe>;

/** The heading that names the alert — `AlertDialog.Title`, an `<h2>`. */
export const alertDialogTitleRecipe = cva("text-xl font-semibold text-foreground");

/** The title recipe's variant props, mixed into `AlertDialogTitleProps`. */
export type AlertDialogTitleRecipeProps = VariantProps<typeof alertDialogTitleRecipe>;

/** The supporting copy under the title — `AlertDialog.Description`, a `<p>`. */
export const alertDialogDescriptionRecipe = cva("mt-2 text-sm text-muted-foreground");

/** The description recipe's variant props, mixed into `AlertDialogDescriptionProps`. */
export type AlertDialogDescriptionRecipeProps = VariantProps<typeof alertDialogDescriptionRecipe>;

/*
 * The alert dialog's ACTION BUTTONS — `AlertDialog.Close`. Base UI ships no
 * Action/Cancel part: in an alert dialog every button in the footer is a `Close`
 * (the confirm one just carries the caller's `onClick` as well), so this is the
 * part that has to look like a button, and the variant axis is what tells the
 * confirm apart from the cancel.
 *
 * Each variant is BUILT FROM `buttonRecipe` rather than restating the button's
 * utilities — the one deliberate cross-component import in this catalog. The
 * composition is a template literal, following the existing precedent in
 * field.styles.ts: a styles file composes recipes as strings and leaves the
 * `cn()`/tailwind-merge pass to the component.
 *
 * `w-auto` is appended because `buttonRecipe`'s base sets `w-full` — the Button
 * component is app-form-shaped, and two buttons share an alert's footer row, so
 * full width is wrong here. The pair collapses through `cn()` in
 * alert-dialog.tsx, leaving `w-auto` and dropping `w-full`; the
 * "builds the close recipe from the button's own recipe" spec asserts that
 * against `buttonRecipe`'s LIVE output, so the two cannot drift apart.
 *
 * `defaultVariants` is `secondary`, not the button's `primary`: the unmarked
 * button in an alert's footer is the cancel.
 */
export const alertDialogCloseRecipe = cva("", {
  variants: {
    variant: {
      primary: `${buttonRecipe({ variant: "primary" })} w-auto`,
      secondary: `${buttonRecipe({ variant: "secondary" })} w-auto`,
      destructive: `${buttonRecipe({ variant: "destructive" })} w-auto`,
    },
  },
  defaultVariants: { variant: "secondary" },
});

/** The close recipe's variant props, mixed into `AlertDialogCloseProps`. */
export type AlertDialogCloseRecipeProps = VariantProps<typeof alertDialogCloseRecipe>;
