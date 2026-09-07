import { Collapsible as BaseCollapsible } from "@base-ui/react/collapsible";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  collapsiblePanelRecipe,
  type CollapsiblePanelRecipeProps,
  collapsibleRootRecipe,
  type CollapsibleRootRecipeProps,
  collapsibleTriggerRecipe,
  type CollapsibleTriggerRecipeProps,
} from "./collapsible.styles";

/**
 * The Collapsible anatomy, on Base UI's `Collapsible` parts — the standalone
 * show/hide primitive that `../accordion` is built out of internally.
 *
 * Every part narrows `className` back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. The whole catalog makes this narrowing,
 * so a caller's `className` always means "utilities merged over the recipe,
 * last one wins".
 */

/**
 * The wrapper's props. Base UI's `Collapsible.Root` renders a `div` — unlike
 * `Accordion.Root` it is NOT generic, since a collapsible has one boolean
 * `open` rather than a value list.
 */
export interface CollapsibleRootProps
  extends
    Omit<ComponentProps<typeof BaseCollapsible.Root>, "className">,
    CollapsibleRootRecipeProps {
  readonly className?: string;
}

/** The trigger's props. Base UI's `Collapsible.Trigger` renders a `button`. */
export interface CollapsibleTriggerProps
  extends
    Omit<ComponentProps<typeof BaseCollapsible.Trigger>, "className">,
    CollapsibleTriggerRecipeProps {
  readonly className?: string;
}

/**
 * The panel's props. Base UI's `Collapsible.Panel` renders a plain `div` — no
 * `role="region"` and no `aria-labelledby`, which is the anatomy difference from
 * `Accordion.Panel`. It also UNMOUNTS while closed by default; `keepMounted`
 * keeps it in the DOM behind a `hidden` attribute, and `hiddenUntilFound` (which
 * overrides `keepMounted`) uses `hidden="until-found"` so the browser's own
 * find-in-page can reveal it.
 */
export interface CollapsiblePanelProps
  extends
    Omit<ComponentProps<typeof BaseCollapsible.Panel>, "className">,
    CollapsiblePanelRecipeProps {
  readonly className?: string;
}

function CollapsibleRoot({ className, ...rest }: CollapsibleRootProps): ReactElement {
  return <BaseCollapsible.Root className={cn(collapsibleRootRecipe(), className)} {...rest} />;
}

function CollapsibleTrigger({ className, ...rest }: CollapsibleTriggerProps): ReactElement {
  return (
    <BaseCollapsible.Trigger className={cn(collapsibleTriggerRecipe(), className)} {...rest} />
  );
}

function CollapsiblePanel({ className, ...rest }: CollapsiblePanelProps): ReactElement {
  return <BaseCollapsible.Panel className={cn(collapsiblePanelRecipe(), className)} {...rest} />;
}

/**
 * A single expandable section. Root owns open state, Trigger toggles it, and Panel contains
 * the expandable content.
 */
export const Collapsible = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: CollapsibleRoot,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: CollapsibleTrigger,
  /**
   * Contains the content associated with the selected or expanded item.
   */
  Panel: CollapsiblePanel,
};
