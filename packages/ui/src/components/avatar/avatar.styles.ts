import { cva } from "class-variance-authority";

/** Avatar recipes provide the shared frame, image sizing, and fallback presentation. */

/**
 * The circular frame. Sizes the whole avatar and clips whatever is inside it, so
 * a non-square image cannot escape the circle.
 */
export const avatarRootRecipe = cva(
  "relative inline-flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-muted select-none",
);

/** The photo, filling the frame without distorting its aspect ratio. */
export const avatarImageRecipe = cva("size-full object-cover");

/**
 * The initials (or icon) shown while there is no usable image. Fills the frame
 * so its background reads as the avatar itself rather than as a badge inside it.
 */
export const avatarFallbackRecipe = cva(
  "flex size-full items-center justify-center text-sm font-medium text-muted-foreground",
);
