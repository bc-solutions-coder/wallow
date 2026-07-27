import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the tooltip. The class lists are not invented
 * here — tooltip.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set through the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * One recipe per part that renders a VISIBLE element. `Provider` and `Root`
 * render no element, `Portal` renders only the structural container Base UI
 * appends to `<body>`, and `Handle`/`createHandle` are not components at all, so
 * none of them has a recipe (see tooltip.tsx for why they are re-exported
 * unwrapped).
 *
 * No recipe takes a cva VARIANT. A tooltip has no visual variant axis in this
 * catalog: open/closed, the entering/exiting transition phases, the resolved
 * side/align and the disabled trigger are all STATES, and Base UI publishes
 * states as `data-*` attributes, so they belong in the base string as
 * `data-[starting-style]:` / `data-[ending-style]:` / `data-[trigger-disabled]:`
 * modifiers rather than as cva variants nobody would pass by hand. The
 * `VariantProps` types are still exported so each part's props keep the
 * catalog-wide shape and a later variant axis stays a non-breaking addition.
 */

/**
 * The element the tooltip is attached to — Base UI's `Tooltip.Trigger`, a
 * `<button>`. Deliberately COLOURLESS for the same reason as the dialog trigger:
 * a tooltip trigger is routinely composed onto a real `Button` through Base UI's
 * `render` prop, and a `bg-*` here would be merged away by tailwind-merge and
 * silently beat the Button's own background.
 *
 * The disabled modifier is `data-[trigger-disabled]:`, NOT `data-[disabled]:` —
 * measured: Base UI stamps `data-trigger-disabled` on a tooltip trigger, so a
 * `data-[disabled]:` modifier copied from the dialog would match nothing.
 */
export const tooltipTriggerRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium transition-colors data-[trigger-disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `TooltipTriggerProps`. */
export type TooltipTriggerRecipeProps = VariantProps<typeof tooltipTriggerRecipe>;

/**
 * The anchored wrapper Base UI positions — `Tooltip.Positioner`, a `<div>`.
 *
 * Base UI owns this element's `position`, `left`, `top` and `transform` as INLINE
 * styles, so the recipe may only add stacking and focus concerns; any layout
 * utility here is dead weight the positioning engine overrides. This is
 * `Select.Positioner`'s rule, not `Dialog.Popup`'s — do NOT copy the dialog's
 * centring utilities onto an anchored overlay.
 */
export const tooltipPositionerRecipe = cva("z-50 outline-none");

/** The positioner recipe's variant props, mixed into `TooltipPositionerProps`. */
export type TooltipPositionerRecipeProps = VariantProps<typeof tooltipPositionerRecipe>;

/**
 * The bubble itself — Base UI's `Tooltip.Popup`, a `<div>`. Smaller and lighter
 * than the dialog's popup (`text-xs`, `px-3 py-1.5`, `shadow-md`) because a
 * tooltip is a label rather than a surface, and it carries NO positioning at all:
 * the positioner above already placed it.
 *
 * The four `data-[…-style]:` modifiers are the ONLY place the enter and exit
 * phases are expressed — Base UI sets `data-starting-style` and
 * `data-ending-style` for the duration of the transition and removes them again.
 */
export const tooltipPopupRecipe = cva(
  "rounded-md border border-border bg-popover px-3 py-1.5 text-xs text-popover-foreground shadow-md transition-all duration-150 data-[starting-style]:scale-95 data-[starting-style]:opacity-0 data-[ending-style]:scale-95 data-[ending-style]:opacity-0",
);

/** The popup recipe's variant props, mixed into `TooltipPopupProps`. */
export type TooltipPopupRecipeProps = VariantProps<typeof tooltipPopupRecipe>;

/**
 * The pointer triangle — Base UI's `Tooltip.Arrow`, an `aria-hidden` `<div>`.
 * Base UI sets the arrow's `position` and offset inline exactly as it does for
 * the positioner, so this recipe adds colour and layout for the caller's glyph
 * only.
 */
export const tooltipArrowRecipe = cva("flex text-popover-foreground");

/** The arrow recipe's variant props, mixed into `TooltipArrowProps`. */
export type TooltipArrowRecipeProps = VariantProps<typeof tooltipArrowRecipe>;

/**
 * The content-transition container — Base UI's `Tooltip.Viewport`, a `<div>`. It
 * crossfades the popup's contents when one popup is shared by several triggers,
 * so it needs a positioning context for the outgoing copy and clipping while the
 * two overlap — and nothing else, since it must not impose a box on a popup that
 * has no viewport at all.
 */
export const tooltipViewportRecipe = cva("relative overflow-hidden");

/** The viewport recipe's variant props, mixed into `TooltipViewportProps`. */
export type TooltipViewportRecipeProps = VariantProps<typeof tooltipViewportRecipe>;
