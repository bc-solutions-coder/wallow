import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the preview card. The class lists are not
 * invented here — preview-card.test.tsx declares each part's exact utility set
 * as a top-of-file `*_CLASSES` constant and asserts it as an order-free set
 * through the rendered component, so that spec is the source of truth for
 * everything below.
 *
 * One recipe per part that renders a VISIBLE element, per the rule the Dialog
 * exemplar (Wallow-m5aq.3.1) established. `Root` renders no element, `Portal`
 * renders only the structural container Base UI appends to `<body>`, and
 * `Handle`/`createHandle` render no DOM at all, so none of them has a recipe
 * (see preview-card.tsx for why they are re-exported unwrapped).
 *
 * No recipe takes a cva VARIANT. A preview card has no visual variant axis in
 * this catalog: open/closed, the entering/exiting transition phases and the
 * resolved side/alignment are all STATES, and Base UI publishes states as
 * `data-*` attributes, so they belong in the base string as
 * `data-[starting-style]:` / `data-[ending-style]:` / `data-[popup-open]:`
 * modifiers rather than as cva variants nobody would pass by hand. The
 * `VariantProps` types are still exported so each part's props keep the
 * catalog-wide shape and a later variant axis stays a non-breaking addition.
 */

/**
 * The link that reveals the card — Base UI's `PreviewCard.Trigger`, an `<a>`.
 *
 * A deliberate DIVERGENCE from every other trigger in this catalog: the dialog,
 * popover and tooltip triggers are `inline-flex items-center justify-center`
 * buttons, but a preview-card trigger is a LINK INSIDE RUNNING PROSE (measured:
 * Base UI renders an `<a>`, and without an `href` the element is not focusable
 * at all). `inline-flex` would pull it out of the line box it belongs to, so
 * this recipe stays inline and styles the underline instead.
 *
 * Colourless for the same reason as the other triggers: the surrounding prose
 * owns the link colour, and a `text-*` here would be merged away by
 * tailwind-merge and silently beat it. `data-[popup-open]:decoration-solid` is
 * the one state Base UI publishes on this part (`data-popup-open` is the only
 * member of `PreviewCardTriggerDataAttributes`), so the dotted underline firms
 * up while the card it opens is on screen.
 */
export const previewCardTriggerRecipe = cva(
  "rounded-sm underline decoration-dotted underline-offset-4 transition-colors data-[popup-open]:decoration-solid",
);

/** The trigger recipe's variant props, mixed into `PreviewCardTriggerProps`. */
export type PreviewCardTriggerRecipeProps = VariantProps<typeof previewCardTriggerRecipe>;

/**
 * The optional scrim behind an open card — `PreviewCard.Backdrop`, a `<div>`.
 *
 * Measured: Base UI gives this element `pointer-events: none` INLINE (unlike
 * `Popover.Backdrop`, which only gets `user-select: none`), so it can never
 * catch a press and is purely a dimmer. It follows that this recipe owns its
 * size and stacking outright, and that outside-press coverage cannot be driven
 * through it.
 *
 * `/10` rather than the popover's `/20` and the dialog's `/50`: a preview card
 * is the lightest chrome in the catalog — it appears on hover and vanishes on
 * unhover, so anything heavier reads as a page-blocking modal. `z-40` keeps it
 * under the `z-50` positioner it dims behind. The two
 * `data-[…-style]:opacity-0` modifiers are the only place the enter and exit
 * phases are expressed.
 */
export const previewCardBackdropRecipe = cva(
  "fixed inset-0 z-40 bg-foreground/10 transition-opacity duration-150 data-[starting-style]:opacity-0 data-[ending-style]:opacity-0",
);

/** The backdrop recipe's variant props, mixed into `PreviewCardBackdropProps`. */
export type PreviewCardBackdropRecipeProps = VariantProps<typeof previewCardBackdropRecipe>;

/**
 * The anchored wrapper Base UI positions against the trigger —
 * `PreviewCard.Positioner`, a `<div role="presentation">`.
 *
 * Stacking and focus ONLY, exactly like `selectPositionerRecipe` and
 * `popoverPositionerRecipe`. Measured: Base UI writes this element's `position`,
 * `left`, `top` and the `--positioner-*` / `--available-*` / `--anchor-*` /
 * `--transform-origin` custom properties INLINE and rewrites them on every
 * scroll and resize, so any layout utility here would fight the positioning
 * engine.
 */
export const previewCardPositionerRecipe = cva("z-50 outline-none");

/** The positioner recipe's variant props, mixed into `PreviewCardPositionerProps`. */
export type PreviewCardPositionerRecipeProps = VariantProps<typeof previewCardPositionerRecipe>;

/**
 * The card itself — Base UI's `PreviewCard.Popup`, a `<div tabindex="-1">`.
 *
 * Paint and box ONLY: no `fixed`, no `z-*`, no translate. The positioner above
 * carries the placement (measured: the popup's only inline styles are
 * `--popup-width` / `--popup-height`), so this is the concrete case
 * `dialogPopupRecipe` warns anchored overlays about.
 *
 * `w-72` is a fixed width rather than the popover's `min-w-56 max-w-sm`: a
 * preview card previews a known thing — a profile, a repository, a document —
 * so its width is part of the format, and a card that resizes with its content
 * flickers as the content loads. `outline-none` matters here specifically,
 * because Base UI makes the popup programmatically focusable.
 */
export const previewCardPopupRecipe = cva(
  "w-72 rounded-lg border border-border bg-popover p-4 text-popover-foreground shadow-lg outline-none transition-all duration-150 data-[starting-style]:scale-95 data-[starting-style]:opacity-0 data-[ending-style]:scale-95 data-[ending-style]:opacity-0",
);

/** The popup recipe's variant props, mixed into `PreviewCardPopupProps`. */
export type PreviewCardPopupRecipeProps = VariantProps<typeof previewCardPopupRecipe>;

/**
 * The little pointer aimed at the anchor — `PreviewCard.Arrow`, a `<div>`.
 *
 * Size and paint ONLY: measured, Base UI positions the arrow inline on whichever
 * axis the resolved side needs, so placement here is forbidden for the same
 * reason as on the positioner. A square rotated into a diamond, wearing the
 * POPUP's own surface and border tokens rather than any hardcoded colour, so the
 * arrow reads as part of the card in every fork's theme. `rounded-sm` because
 * `--radius-sm/md/lg` are the three radii `@bc-solutions-coder/styles` declares.
 */
export const previewCardArrowRecipe = cva(
  "h-2.5 w-2.5 rotate-45 rounded-sm border border-border bg-popover",
);

/** The arrow recipe's variant props, mixed into `PreviewCardArrowProps`. */
export type PreviewCardArrowRecipeProps = VariantProps<typeof previewCardArrowRecipe>;

/**
 * The container that cross-fades content when one card serves several triggers —
 * `PreviewCard.Viewport`, a `<div>`.
 *
 * Measured: Base UI wraps the viewport's children in its own
 * `<div data-current="true">` and absolutely positions the outgoing copy during
 * a transition, so the viewport needs a positioning context and a clip, and
 * nothing else.
 */
export const previewCardViewportRecipe = cva("relative overflow-hidden");

/** The viewport recipe's variant props, mixed into `PreviewCardViewportProps`. */
export type PreviewCardViewportRecipeProps = VariantProps<typeof previewCardViewportRecipe>;
