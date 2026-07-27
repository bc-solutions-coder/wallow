import { ScrollArea as BaseScrollArea } from "@base-ui/react/scroll-area";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  scrollAreaContentRecipe,
  type ScrollAreaContentRecipeProps,
  scrollAreaCornerRecipe,
  type ScrollAreaCornerRecipeProps,
  scrollAreaRootRecipe,
  type ScrollAreaRootRecipeProps,
  scrollAreaScrollbarRecipe,
  type ScrollAreaScrollbarRecipeProps,
  scrollAreaThumbRecipe,
  type ScrollAreaThumbRecipeProps,
  scrollAreaViewportRecipe,
  type ScrollAreaViewportRecipeProps,
} from "./scroll-area.styles";

/**
 * The Scroll Area anatomy, on Base UI's `ScrollArea` parts.
 *
 * All six of Base UI's namespace members render a visible element, so all six
 * are wrapped here and the namespace keys mirror `@base-ui/react/scroll-area`
 * 1:1 — a caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable set is `Root > Viewport(Content) + Scrollbar(Thumb)`.
 * `Content` and `Corner` are optional: `Content` adds the `min-width: fit-content`
 * wrapper that makes horizontal overflow behave, and `Corner` fills the square
 * where the two tracks meet.
 *
 * Every part narrows `className` back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. The whole catalog makes this narrowing,
 * so a caller's `className` always means "utilities merged over the recipe,
 * last one wins".
 *
 * ONE part is unlike every other component in the catalog: `Viewport` receives a
 * class from Base UI ITSELF (`base-ui-disable-scrollbar`, which hides the native
 * scrollbars). Base UI appends that to whatever this file passes down, so the
 * recipe ends up merged ON TOP of it rather than replacing it and the viewport's
 * class set is Base UI's class PLUS the recipe.
 */

/**
 * The scroll area container's props. Base UI's `ScrollArea.Root` renders a
 * `div role="presentation"` positioned `relative` inline, and owns
 * `overflowEdgeThreshold`.
 */
export interface ScrollAreaRootProps
  extends Omit<ComponentProps<typeof BaseScrollArea.Root>, "className">, ScrollAreaRootRecipeProps {
  readonly className?: string;
}

/**
 * The scrollable box's props. Base UI's `ScrollArea.Viewport` renders a
 * `div role="presentation"` with `overflow: scroll` inline, and is the element
 * that actually scrolls.
 */
export interface ScrollAreaViewportProps
  extends
    Omit<ComponentProps<typeof BaseScrollArea.Viewport>, "className">,
    ScrollAreaViewportRecipeProps {
  readonly className?: string;
}

/**
 * The content wrapper's props. Base UI's `ScrollArea.Content` renders a
 * `div role="presentation"` with `min-width: fit-content` inline.
 */
export interface ScrollAreaContentProps
  extends
    Omit<ComponentProps<typeof BaseScrollArea.Content>, "className">,
    ScrollAreaContentRecipeProps {
  readonly className?: string;
}

/**
 * A track's props. Base UI's `ScrollArea.Scrollbar` renders an absolutely
 * positioned `div`, and owns `orientation` plus `keepMounted`.
 */
export interface ScrollAreaScrollbarProps
  extends
    Omit<ComponentProps<typeof BaseScrollArea.Scrollbar>, "className">,
    ScrollAreaScrollbarRecipeProps {
  readonly className?: string;
}

/**
 * A handle's props. Base UI's `ScrollArea.Thumb` renders a `div` whose extent
 * along the scroll axis comes from the track's `--scroll-area-thumb-*` custom
 * property and whose offset comes from an inline `transform`.
 */
export interface ScrollAreaThumbProps
  extends
    Omit<ComponentProps<typeof BaseScrollArea.Thumb>, "className">,
    ScrollAreaThumbRecipeProps {
  readonly className?: string;
}

/**
 * The corner's props. Base UI's `ScrollArea.Corner` renders a `div` sized inline
 * from the two tracks, and collapses to 0x0 unless both axes overflow.
 */
export interface ScrollAreaCornerProps
  extends
    Omit<ComponentProps<typeof BaseScrollArea.Corner>, "className">,
    ScrollAreaCornerRecipeProps {
  readonly className?: string;
}

function ScrollAreaRoot({ className, ...rest }: ScrollAreaRootProps): ReactElement {
  return <BaseScrollArea.Root className={cn(scrollAreaRootRecipe(), className)} {...rest} />;
}

function ScrollAreaViewport({ className, ...rest }: ScrollAreaViewportProps): ReactElement {
  return (
    <BaseScrollArea.Viewport className={cn(scrollAreaViewportRecipe(), className)} {...rest} />
  );
}

function ScrollAreaContent({ className, ...rest }: ScrollAreaContentProps): ReactElement {
  return <BaseScrollArea.Content className={cn(scrollAreaContentRecipe(), className)} {...rest} />;
}

function ScrollAreaScrollbar({ className, ...rest }: ScrollAreaScrollbarProps): ReactElement {
  return (
    <BaseScrollArea.Scrollbar className={cn(scrollAreaScrollbarRecipe(), className)} {...rest} />
  );
}

function ScrollAreaThumb({ className, ...rest }: ScrollAreaThumbProps): ReactElement {
  return <BaseScrollArea.Thumb className={cn(scrollAreaThumbRecipe(), className)} {...rest} />;
}

function ScrollAreaCorner({ className, ...rest }: ScrollAreaCornerProps): ReactElement {
  return <BaseScrollArea.Corner className={cn(scrollAreaCornerRecipe(), className)} {...rest} />;
}

/**
 * The catalog's scroll area, as ONE namespace object whose keys mirror Base UI's
 * six namespace members 1:1 — the catalog-wide convention for multi-part
 * components.
 */
export const ScrollArea = {
  Root: ScrollAreaRoot,
  Viewport: ScrollAreaViewport,
  Content: ScrollAreaContent,
  Scrollbar: ScrollAreaScrollbar,
  Thumb: ScrollAreaThumb,
  Corner: ScrollAreaCorner,
};
