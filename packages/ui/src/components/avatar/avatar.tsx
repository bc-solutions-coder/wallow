import { Avatar as BaseAvatar } from "@base-ui/react/avatar";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { avatarFallbackRecipe, avatarImageRecipe, avatarRootRecipe } from "./avatar.styles";

/**
 * Every Base UI `Avatar.Root` prop (`render` plus the native span attributes).
 *
 * `className` is deliberately narrowed back to `string`, as everywhere in this
 * catalog: Base UI widens it to `string | ((state) => string | undefined)` and
 * the callback form cannot be merged with a recipe through `cn()`.
 */
export interface AvatarRootProps extends Omit<ComponentProps<typeof BaseAvatar.Root>, "className"> {
  readonly className?: string;
}

/**
 * Every Base UI `Avatar.Image` prop — `src`/`alt` and the rest of the native
 * `<img>` attributes, plus `onLoadingStatusChange`, which reports
 * `'idle' | 'loading' | 'loaded' | 'error'` as the browser fetches the photo.
 * `className` is narrowed the same way.
 */
export interface AvatarImageProps extends Omit<
  ComponentProps<typeof BaseAvatar.Image>,
  "className"
> {
  readonly className?: string;
}

/**
 * Every Base UI `Avatar.Fallback` prop, including `delay` (milliseconds to wait
 * before mounting, so a fast-loading photo never flashes its initials first).
 * `className` is narrowed the same way.
 */
export interface AvatarFallbackProps extends Omit<
  ComponentProps<typeof BaseAvatar.Fallback>,
  "className"
> {
  readonly className?: string;
}

function AvatarRoot({ className, ...rest }: AvatarRootProps): ReactElement {
  return <BaseAvatar.Root className={cn(avatarRootRecipe(), className)} {...rest} />;
}

function AvatarImage({ className, ...rest }: AvatarImageProps): ReactElement {
  return <BaseAvatar.Image className={cn(avatarImageRecipe(), className)} {...rest} />;
}

function AvatarFallback({ className, ...rest }: AvatarFallbackProps): ReactElement {
  return <BaseAvatar.Fallback className={cn(avatarFallbackRecipe(), className)} {...rest} />;
}

/**
 * The catalog's avatar. Multi-part components ship a single namespace object
 * whose keys mirror Base UI's part names 1:1 — `Avatar.Root` is the frame,
 * `Avatar.Image` the photo and `Avatar.Fallback` the initials shown when there
 * is no photo or it fails to load.
 *
 * The two children are mutually exclusive at runtime and Base UI owns the
 * switch: it mounts the image only once the browser has decoded it, and mounts
 * the fallback whenever it has not. Callers render both and never branch.
 */
export const Avatar = {
  Root: AvatarRoot,
  Image: AvatarImage,
  Fallback: AvatarFallback,
};
