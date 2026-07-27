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

/**
 * Four of Base UI's ten namespace members are re-exported UNWRAPPED, because
 * none of them can carry a recipe — the rule the Dialog exemplar
 * (Wallow-m5aq.3.1) set for every overlay in this catalog:
 *
 *   - `Root` renders no HTML element at all (it is the state container), and it
 *     is generic over the trigger payload type — wrapping it would either drop
 *     the generic or add an element the DOM does not want.
 *   - `Portal` renders only the structural `<div data-base-ui-portal>` Base UI
 *     appends to `<body>`. It accepts a `className`, but it has no visual role,
 *     and the caller's `className` still reaches it because the part is
 *     re-exported unchanged.
 *   - `Handle` is a class and `createHandle` a factory — the imperative
 *     open/close API for triggers that live outside the `Root`. Neither renders
 *     anything.
 *
 * Everything that renders a visible element is wrapped, so `Object.keys` still
 * mirrors `@base-ui/react/preview-card`'s namespace 1:1.
 *
 * Note what is NOT here: a preview card has no `Title`, `Description` or `Close`
 * part, and no `Provider`. It is opened by pointing at a link and closed by
 * pointing somewhere else, so Base UI wires it no ARIA at all (grepped: the only
 * `aria-*` in the whole subpath is the arrow's `aria-hidden`). Anything the card
 * needs to announce is the caller's own markup inside the popup.
 */

/** Every Base UI `PreviewCard.Root` prop, generic over the trigger payload type. */
export type PreviewCardRootProps<Payload = unknown> = Parameters<
  typeof BasePreviewCard.Root<Payload>
>[0];

/** Every Base UI `PreviewCard.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type PreviewCardPortalProps = ComponentProps<typeof BasePreviewCard.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

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
 * The catalog's preview card, as ONE namespace object whose keys mirror Base
 * UI's ten namespace members 1:1 — the catalog-wide convention for multi-part
 * components, so a caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable card is Root > Trigger plus a portalled
 * Positioner(Popup(…)). `Arrow` is the opt-in pointer aimed at the anchor,
 * `Backdrop` the opt-in dimmer, `Viewport` the opt-in cross-fade container for a
 * card shared by several triggers, and `Handle`/`createHandle` the opt-in
 * imperative API for triggers outside the Root.
 *
 * A preview card is the lightest overlay in this catalog. It is NOT modal —
 * there is no `modal` prop and Base UI renders no pointer blocker, so the page
 * keeps scrolling and the card's own links stay clickable. It opens by pointing
 * at or focusing the trigger, after that trigger's `delay` (600 ms by default)
 * has elapsed, and closes on unhover, blur, Escape or an outside press. The
 * popup is never put in the tab order: tabbing off the trigger dismisses the
 * card rather than entering it, so anything the card offers must also be
 * reachable somewhere else.
 */
export const PreviewCard = {
  Root: BasePreviewCard.Root,
  Trigger: PreviewCardTrigger,
  Portal: BasePreviewCard.Portal,
  Backdrop: PreviewCardBackdrop,
  Positioner: PreviewCardPositioner,
  Popup: PreviewCardPopup,
  Arrow: PreviewCardArrow,
  Viewport: PreviewCardViewport,
  Handle: BasePreviewCard.Handle,
  createHandle: BasePreviewCard.createHandle,
};
