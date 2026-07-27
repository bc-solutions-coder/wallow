import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the popover. The class lists are not invented
 * here — popover.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set through the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * One recipe per part that renders a VISIBLE element, per the rule the Dialog
 * exemplar (Wallow-m5aq.3.1) established. `Root` renders no element, `Portal`
 * renders only the structural container Base UI appends to `<body>`, and
 * `Handle`/`createHandle` render no DOM at all, so none of them has a recipe
 * (see popover.tsx for why they are re-exported unwrapped).
 *
 * No recipe takes a cva VARIANT. A popover has no visual variant axis in this
 * catalog: open/closed, the entering/exiting transition phases and the resolved
 * side/alignment are all STATES, and Base UI publishes states as `data-*`
 * attributes, so they belong in the base string as `data-[starting-style]:` /
 * `data-[ending-style]:` / `data-[side=…]:` modifiers rather than as cva
 * variants nobody would pass by hand. The `VariantProps` types are still
 * exported so each part's props keep the catalog-wide shape and a later variant
 * axis stays a non-breaking addition.
 */

/**
 * The button that opens the popover — Base UI's `Popover.Trigger`, a `<button>`.
 * Deliberately COLOURLESS, for the same reason as `dialogTriggerRecipe`: a
 * trigger is routinely composed onto a real `Button` through Base UI's `render`
 * prop, and a `bg-*` here would be merged away by tailwind-merge and silently
 * beat the Button's own background.
 */
export const popoverTriggerRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium transition-colors data-[disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `PopoverTriggerProps`. */
export type PopoverTriggerRecipeProps = VariantProps<typeof popoverTriggerRecipe>;

/**
 * The optional scrim behind an open popover — Base UI's `Popover.Backdrop`, a
 * `<div>`. Measured: Base UI gives it only `user-select: none` inline, so this
 * recipe owns its size and stacking outright.
 *
 * Two deliberate differences from `dialogBackdropRecipe`: `/20` rather than
 * `/50`, because a popover is non-modal chrome and not a page-blocking scrim,
 * and `z-40` rather than `z-50`, so the backdrop always sits UNDER the `z-50`
 * positioner it dims behind. The two `data-[…-style]:opacity-0` modifiers are
 * the only place the enter and exit phases are expressed: Base UI sets
 * `data-starting-style` / `data-ending-style` for the transition and removes
 * them again.
 */
export const popoverBackdropRecipe = cva(
  "fixed inset-0 z-40 bg-foreground/20 transition-opacity duration-150 data-[starting-style]:opacity-0 data-[ending-style]:opacity-0",
);

/** The backdrop recipe's variant props, mixed into `PopoverBackdropProps`. */
export type PopoverBackdropRecipeProps = VariantProps<typeof popoverBackdropRecipe>;

/**
 * The anchored wrapper Base UI positions against the trigger — `Popover.Positioner`,
 * a `<div>`.
 *
 * Stacking and focus ONLY, exactly like `selectPositionerRecipe`. Measured: Base
 * UI writes this element's `position`, `left`, `top` and the `--positioner-*` /
 * `--available-*` / `--anchor-*` custom properties INLINE and rewrites them on
 * every scroll and resize, so any layout utility here would fight the
 * positioning engine.
 */
export const popoverPositionerRecipe = cva("z-50 outline-none");

/** The positioner recipe's variant props, mixed into `PopoverPositionerProps`. */
export type PopoverPositionerRecipeProps = VariantProps<typeof popoverPositionerRecipe>;

/**
 * The popover card itself — Base UI's `Popover.Popup`, a `<div role="dialog">`.
 *
 * Paint and box ONLY: no `fixed`, no `z-*`, no translate. The positioner above
 * carries the placement, so this is the concrete case `dialogPopupRecipe` warns
 * about — an anchored popup must NOT copy the dialog's centring utilities.
 */
export const popoverPopupRecipe = cva(
  "min-w-56 max-w-sm rounded-lg border border-border bg-popover p-4 text-popover-foreground shadow-lg outline-none transition-all duration-150 data-[starting-style]:scale-95 data-[starting-style]:opacity-0 data-[ending-style]:scale-95 data-[ending-style]:opacity-0",
);

/** The popup recipe's variant props, mixed into `PopoverPopupProps`. */
export type PopoverPopupRecipeProps = VariantProps<typeof popoverPopupRecipe>;

/**
 * The little pointer aimed at the anchor — Base UI's `Popover.Arrow`, a `<div>`.
 *
 * Size and paint ONLY: measured, Base UI positions the arrow inline on whichever
 * axis the resolved side needs (`left` for `side=bottom`, `top` for
 * `side=right`), so placement here is forbidden for the same reason as on the
 * positioner. A square rotated into a diamond, wearing the POPUP's own surface
 * and border tokens rather than any hardcoded colour, so the arrow reads as part
 * of the card in every fork's theme. `rounded-sm` because `--radius-sm/md/lg`
 * are the three radii `@bc-solutions-coder/styles` declares.
 */
export const popoverArrowRecipe = cva(
  "h-2.5 w-2.5 rotate-45 rounded-sm border border-border bg-popover",
);

/** The arrow recipe's variant props, mixed into `PopoverArrowProps`. */
export type PopoverArrowRecipeProps = VariantProps<typeof popoverArrowRecipe>;

/**
 * The container that cross-fades content when one popup serves several triggers
 * — Base UI's `Popover.Viewport`, a `<div>`.
 *
 * NOT the dialog's viewport: `Dialog.Viewport` is an optional SCROLL region
 * around the popup, while this one lives INSIDE the popup and cross-fades its
 * content. Base UI absolutely positions the outgoing copy within it, so it needs
 * a positioning context and a clip, and nothing else.
 */
export const popoverViewportRecipe = cva("relative overflow-hidden");

/** The viewport recipe's variant props, mixed into `PopoverViewportProps`. */
export type PopoverViewportRecipeProps = VariantProps<typeof popoverViewportRecipe>;

/**
 * The heading that names the popover — Base UI's `Popover.Title`, an `<h2>`.
 * COLOURLESS on purpose, a deliberate divergence from `dialogTitleRecipe`'s
 * `text-foreground`: the popup already establishes `text-popover-foreground`,
 * and restating a page-level colour here would break any fork whose popover
 * foreground differs. `text-sm` rather than `text-lg` because a popover is small
 * chrome.
 */
export const popoverTitleRecipe = cva("text-sm font-semibold");

/** The title recipe's variant props, mixed into `PopoverTitleProps`. */
export type PopoverTitleRecipeProps = VariantProps<typeof popoverTitleRecipe>;

/**
 * The supporting copy under the title — Base UI's `Popover.Description`, a `<p>`.
 * `mt-1` rather than the dialog's `mt-2`: a tighter stack inside `p-4` chrome.
 */
export const popoverDescriptionRecipe = cva("mt-1 text-sm text-muted-foreground");

/** The description recipe's variant props, mixed into `PopoverDescriptionProps`. */
export type PopoverDescriptionRecipeProps = VariantProps<typeof popoverDescriptionRecipe>;

/**
 * The button that dismisses the popover — Base UI's `Popover.Close`, a
 * `<button>`. Identical to `dialogCloseRecipe`: no absolute corner positioning,
 * so a caller is free to put the close in a footer row or in the popup's corner
 * without unpicking the recipe first.
 */
export const popoverCloseRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium text-muted-foreground transition-colors hover:text-foreground data-[disabled]:opacity-50",
);

/** The close recipe's variant props, mixed into `PopoverCloseProps`. */
export type PopoverCloseRecipeProps = VariantProps<typeof popoverCloseRecipe>;
