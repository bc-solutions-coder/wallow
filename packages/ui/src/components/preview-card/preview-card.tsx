import { PreviewCard as BasePreviewCard } from "@base-ui/react/preview-card";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  previewCardArrowRecipe,
  type PreviewCardArrowRecipeProps,
  previewCardBackdropRecipe,
  type PreviewCardBackdropRecipeProps,
  previewCardPopupRecipe,
  type PreviewCardPopupRecipeProps,
  previewCardPositionerRecipe,
  type PreviewCardPositionerRecipeProps,
  previewCardTriggerRecipe,
  type PreviewCardTriggerRecipeProps,
  previewCardViewportRecipe,
  type PreviewCardViewportRecipeProps,
} from "./preview-card.styles";

/** Every Base UI `PreviewCard.Root` prop, generic over the trigger payload type. */
export type PreviewCardRootProps<Payload = unknown> = Parameters<
  typeof BasePreviewCard.Root<Payload>
>[0];

/** Every Base UI `PreviewCard.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type PreviewCardPortalProps = ComponentProps<typeof BasePreviewCard.Portal>;

/**
 * Every Base UI `PreviewCard.Trigger` prop, with `className` narrowed to
 * `string`. This is where `delay` and `closeDelay` live — on the TRIGGER, not on
 * the `Root` and not on a provider, which is the one API shape a reader coming
 * from `Tooltip` will get wrong.
 */
export interface PreviewCardTriggerProps<Payload = unknown>
  extends
    Omit<Parameters<typeof BasePreviewCard.Trigger<Payload>>[0], "className">,
    PreviewCardTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `PreviewCard.Backdrop` prop, with `className` narrowed to `string`. */
export interface PreviewCardBackdropProps
  extends
    Omit<ComponentProps<typeof BasePreviewCard.Backdrop>, "className">,
    PreviewCardBackdropRecipeProps {
  readonly className?: string;
}

/** Every Base UI `PreviewCard.Positioner` prop, with `className` narrowed to `string`. */
export interface PreviewCardPositionerProps
  extends
    Omit<ComponentProps<typeof BasePreviewCard.Positioner>, "className">,
    PreviewCardPositionerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `PreviewCard.Popup` prop, with `className` narrowed to `string`. */
export interface PreviewCardPopupProps
  extends
    Omit<ComponentProps<typeof BasePreviewCard.Popup>, "className">,
    PreviewCardPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `PreviewCard.Arrow` prop, with `className` narrowed to `string`. */
export interface PreviewCardArrowProps
  extends
    Omit<ComponentProps<typeof BasePreviewCard.Arrow>, "className">,
    PreviewCardArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `PreviewCard.Viewport` prop, with `className` narrowed to `string`. */
export interface PreviewCardViewportProps
  extends
    Omit<ComponentProps<typeof BasePreviewCard.Viewport>, "className">,
    PreviewCardViewportRecipeProps {
  readonly className?: string;
}

function PreviewCardTrigger<Payload>({
  className,
  ...rest
}: PreviewCardTriggerProps<Payload>): ReactElement {
  return (
    <BasePreviewCard.Trigger className={cn(previewCardTriggerRecipe(), className)} {...rest} />
  );
}

function PreviewCardBackdrop({ className, ...rest }: PreviewCardBackdropProps): ReactElement {
  return (
    <BasePreviewCard.Backdrop className={cn(previewCardBackdropRecipe(), className)} {...rest} />
  );
}

function PreviewCardPositioner({ className, ...rest }: PreviewCardPositionerProps): ReactElement {
  return (
    <BasePreviewCard.Positioner
      className={cn(previewCardPositionerRecipe(), className)}
      {...rest}
    />
  );
}

function PreviewCardPopup({ className, ...rest }: PreviewCardPopupProps): ReactElement {
  return <BasePreviewCard.Popup className={cn(previewCardPopupRecipe(), className)} {...rest} />;
}

function PreviewCardArrow({ className, ...rest }: PreviewCardArrowProps): ReactElement {
  return <BasePreviewCard.Arrow className={cn(previewCardArrowRecipe(), className)} {...rest} />;
}

function PreviewCardViewport({ className, ...rest }: PreviewCardViewportProps): ReactElement {
  return (
    <BasePreviewCard.Viewport className={cn(previewCardViewportRecipe(), className)} {...rest} />
  );
}

/**
 * A supplementary preview anchored to a trigger. Compose Root with Trigger and Portal >
 * Positioner > Popup; keep essential information available outside the preview.
 */
export const PreviewCard = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: BasePreviewCard.Root,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: PreviewCardTrigger,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: BasePreviewCard.Portal,
  /**
   * Covers the surrounding page behind the popup.
   */
  Backdrop: PreviewCardBackdrop,
  /**
   * Positions the popup relative to its anchor; render inside Portal.
   */
  Positioner: PreviewCardPositioner,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: PreviewCardPopup,
  /**
   * Draws the popup's pointer toward its anchor.
   */
  Arrow: PreviewCardArrow,
  /**
   * Contains the visible popup region and its layout.
   */
  Viewport: PreviewCardViewport,
  /**
   * Handle class for sharing popup state with detached triggers.
   */
  Handle: BasePreviewCard.Handle,
  /**
   * Creates a handle that connects a popup to triggers outside its Root.
   */
  createHandle: BasePreviewCard.createHandle,
};
