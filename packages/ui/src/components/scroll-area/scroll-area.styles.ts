import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per part of the scroll area. The class lists are not invented here —
 * scroll-area.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set THROUGH the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * All six Base UI parts render a visible element, so all six get a recipe.
 *
 * No recipe takes a cva VARIANT. A scroll area has no visual variant axis in this
 * catalog: the scroll AXIS, the overflow edges, the hover and the in-flight
 * scroll are all STATES that Base UI publishes as `data-*` attributes, so they
 * belong in the base string as `data-[orientation=…]:` modifiers rather than as
 * cva variants a caller would have to keep in step with the `orientation` prop.
 * The `VariantProps` aliases are still exported so each part's props keep the
 * catalog-wide shape and a later variant stays a non-breaking addition.
 */

/**
 * The container that groups every part. Positioned, because Base UI places the
 * tracks and the corner absolutely inside it.
 */
export const scrollAreaRootRecipe = cva("relative overflow-hidden rounded-md bg-card");

/** The root recipe's variant props, mixed into `ScrollAreaRootProps`. */
export type ScrollAreaRootRecipeProps = VariantProps<typeof scrollAreaRootRecipe>;

/**
 * The scrollable box itself, and the only recipe in the catalog that is merged ON
 * TOP of a class Base UI supplies (`base-ui-disable-scrollbar`) rather than
 * standing alone.
 *
 * `size-full` is load-bearing: without it the viewport sizes to its CONTENT, so
 * nothing ever overflows the window onto it and Base UI mounts no track at all.
 * The focus ring lives here rather than on the root because the viewport is what
 * takes the tab stop whenever the region scrolls.
 */
export const scrollAreaViewportRecipe = cva(
  "size-full rounded-[inherit] outline-none focus-visible:ring-2 focus-visible:ring-ring",
);

/** The viewport recipe's variant props, mixed into `ScrollAreaViewportProps`. */
export type ScrollAreaViewportRecipeProps = VariantProps<typeof scrollAreaViewportRecipe>;

/**
 * The optional wrapper around the scrolled content. Carries the body text
 * treatment so the common case needs no class at the call site.
 */
export const scrollAreaContentRecipe = cva("text-sm text-foreground");

/** The content recipe's variant props, mixed into `ScrollAreaContentProps`. */
export type ScrollAreaContentRecipeProps = VariantProps<typeof scrollAreaContentRecipe>;

/**
 * One track, painted the same on both axes: the axis arrives as Base UI's
 * `data-orientation`, so the two thicknesses are modifiers inside the one recipe
 * rather than a cva variant a caller would have to pass alongside `orientation`.
 *
 * Deliberately NOT gated on `data-[hovering]:` — a track that only appears under
 * the pointer cannot be proven by a test and cannot be found by a user who is not
 * using one.
 */
export const scrollAreaScrollbarRecipe = cva(
  "flex rounded-full bg-muted transition-colors duration-150 data-[orientation=vertical]:w-2 data-[orientation=horizontal]:h-2",
);

/** The scrollbar recipe's variant props, mixed into `ScrollAreaScrollbarProps`. */
export type ScrollAreaScrollbarRecipeProps = VariantProps<typeof scrollAreaScrollbarRecipe>;

/**
 * The draggable handle inside a track. Only the CROSS-axis size belongs here:
 * Base UI sizes the thumb ALONG the scroll axis inline, from the track's
 * `--scroll-area-thumb-*` custom property, and that measurement is what makes the
 * handle reflect how much content there is. Drop the `*-full` pair and the handle
 * collapses to zero on the other axis.
 */
export const scrollAreaThumbRecipe = cva(
  "rounded-full bg-border transition-colors hover:bg-muted-foreground data-[orientation=vertical]:w-full data-[orientation=horizontal]:h-full",
);

/** The thumb recipe's variant props, mixed into `ScrollAreaThumbProps`. */
export type ScrollAreaThumbRecipeProps = VariantProps<typeof scrollAreaThumbRecipe>;

/**
 * The square where the two tracks meet. Base UI sizes it inline, and collapses it
 * to 0x0 unless both axes overflow, so the recipe only supplies the fill.
 */
export const scrollAreaCornerRecipe = cva("bg-muted");

/** The corner recipe's variant props, mixed into `ScrollAreaCornerProps`. */
export type ScrollAreaCornerRecipeProps = VariantProps<typeof scrollAreaCornerRecipe>;
