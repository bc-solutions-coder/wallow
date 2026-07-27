import { cva } from "class-variance-authority";

/**
 * The avatar's class recipes — one per Base UI part, no JSX and no React import,
 * so the styling can be read and diffed without the component around it.
 *
 * Every utility must be a semantic token class from `@bc-solutions-coder/styles`
 * (`bg-muted`, `text-muted-foreground`); no raw colour values.
 *
 * Unlike most of this catalog these recipes hang off NO `data-*` state selector:
 * measured against @base-ui/react 1.6.0, an avatar publishes its loading status
 * only through React state and `onLoadingStatusChange`, never as an attribute —
 * the parts MOUNT and UNMOUNT instead (see avatar.test.tsx). There is nothing
 * for a `data-[...]:` modifier to hook.
 */

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
