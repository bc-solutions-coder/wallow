import { Tooltip as BaseTooltip } from "@base-ui/react/tooltip";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  tooltipArrowRecipe,
  type TooltipArrowRecipeProps,
  tooltipPopupRecipe,
  type TooltipPopupRecipeProps,
  tooltipPositionerRecipe,
  type TooltipPositionerRecipeProps,
  tooltipTriggerRecipe,
  type TooltipTriggerRecipeProps,
  tooltipViewportRecipe,
  type TooltipViewportRecipeProps,
} from "./tooltip.styles";

/**
 * Five of Base UI's ten namespace members are re-exported UNWRAPPED, because
 * none of them can carry a recipe:
 *
 *   - `Provider` renders no HTML element at all. It supplies the shared
 *     open/close delay to every tooltip beneath it (Base UI's
 *     `FloatingDelayGroup`), so once one tooltip in the group is showing, its
 *     neighbours open instantly. Re-exporting it unwrapped is what keeps its
 *     `delay` / `closeDelay` / `timeout` props reaching Base UI untouched.
 *   - `Root` renders no HTML element either (it is the state container), and it
 *     is generic over the trigger payload type — wrapping it would drop the
 *     generic or add an element the DOM does not want.
 *   - `Portal` renders only the structural `<div data-base-ui-portal>` Base UI
 *     appends to `<body>`. It accepts a `className` (measured: the class lands
 *     on that div), but it has no visual role, so a recipe here would put a
 *     styled box between the positioner and the document. The caller's
 *     `className` still reaches the element, because the part is re-exported
 *     unchanged.
 *   - `Handle` is a class and `createHandle` a factory — the imperative API for
 *     triggers that live outside the Root. Neither renders anything.
 *
 * This is the rule Wallow-m5aq.3.1 (Dialog) established for every overlay: a
 * part gets a wrapper plus a recipe only if it renders a VISIBLE element; parts
 * that render no element, a structural container, or no DOM at all are
 * re-exported as-is, so the namespace keys still mirror Base UI 1:1.
 */

/**
 * Every Base UI `Tooltip.Provider` prop — `delay`, `closeDelay` and `timeout`,
 * all in milliseconds. Surfaced as a named type so a consumer can hold a
 * fork-wide delay policy in one typed object rather than repeating literals.
 */
export type TooltipProviderProps = ComponentProps<typeof BaseTooltip.Provider>;

/** Every Base UI `Tooltip.Root` prop, generic over the trigger payload type. */
export type TooltipRootProps<Payload = unknown> = Parameters<typeof BaseTooltip.Root<Payload>>[0];

/** Every Base UI `Tooltip.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type TooltipPortalProps = ComponentProps<typeof BaseTooltip.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

/** Every Base UI `Tooltip.Trigger` prop, with `className` narrowed to `string`. */
export interface TooltipTriggerProps<Payload = unknown>
  extends
    Omit<Parameters<typeof BaseTooltip.Trigger<Payload>>[0], "className">,
    TooltipTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Tooltip.Positioner` prop, with `className` narrowed to `string`. */
export interface TooltipPositionerProps
  extends
    Omit<ComponentProps<typeof BaseTooltip.Positioner>, "className">,
    TooltipPositionerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Tooltip.Popup` prop, with `className` narrowed to `string`. */
export interface TooltipPopupProps
  extends Omit<ComponentProps<typeof BaseTooltip.Popup>, "className">, TooltipPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Tooltip.Arrow` prop, with `className` narrowed to `string`. */
export interface TooltipArrowProps
  extends Omit<ComponentProps<typeof BaseTooltip.Arrow>, "className">, TooltipArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Tooltip.Viewport` prop, with `className` narrowed to `string`. */
export interface TooltipViewportProps
  extends
    Omit<ComponentProps<typeof BaseTooltip.Viewport>, "className">,
    TooltipViewportRecipeProps {
  readonly className?: string;
}

function TooltipTrigger<Payload>({
  className,
  ...rest
}: TooltipTriggerProps<Payload>): ReactElement {
  return <BaseTooltip.Trigger className={cn(tooltipTriggerRecipe(), className)} {...rest} />;
}

function TooltipPositioner({ className, ...rest }: TooltipPositionerProps): ReactElement {
  return <BaseTooltip.Positioner className={cn(tooltipPositionerRecipe(), className)} {...rest} />;
}

function TooltipPopup({ className, ...rest }: TooltipPopupProps): ReactElement {
  return <BaseTooltip.Popup className={cn(tooltipPopupRecipe(), className)} {...rest} />;
}

function TooltipArrow({ className, ...rest }: TooltipArrowProps): ReactElement {
  return <BaseTooltip.Arrow className={cn(tooltipArrowRecipe(), className)} {...rest} />;
}

function TooltipViewport({ className, ...rest }: TooltipViewportProps): ReactElement {
  return <BaseTooltip.Viewport className={cn(tooltipViewportRecipe(), className)} {...rest} />;
}

/**
 * The catalog's tooltip, as ONE namespace object whose keys mirror Base UI's ten
 * namespace members 1:1 — the catalog-wide convention for multi-part components,
 * so a caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable tooltip is Root > Trigger plus a portalled
 * Positioner(Popup); `Arrow` is the opt-in pointer triangle, `Viewport` the
 * opt-in transition container for a popup shared by several triggers, and
 * `Provider` the opt-in delay group that makes neighbouring tooltips open
 * instantly once one of them is showing.
 *
 * ACCESSIBILITY, measured against @base-ui/react 1.6.0 rather than assumed:
 * Base UI wires NO aria on a tooltip. The trigger gets no `aria-describedby`
 * and the popup gets no `role="tooltip"` — the only attributes are the
 * `data-base-ui-tooltip-trigger` identifier and `data-popup-open`. A tooltip
 * here is therefore SUPPLEMENTARY: the trigger must carry its own accessible
 * name (its text, or an `aria-label`), and the popup must not be the only place
 * a piece of information appears.
 */
export const Tooltip = {
  Provider: BaseTooltip.Provider,
  Root: BaseTooltip.Root,
  Trigger: TooltipTrigger,
  Portal: BaseTooltip.Portal,
  Positioner: TooltipPositioner,
  Popup: TooltipPopup,
  Arrow: TooltipArrow,
  Viewport: TooltipViewport,
  Handle: BaseTooltip.Handle,
  createHandle: BaseTooltip.createHandle,
};
