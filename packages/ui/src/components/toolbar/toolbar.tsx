import { Toolbar as BaseToolbar } from "@base-ui/react/toolbar";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  toolbarButtonRecipe,
  type ToolbarButtonRecipeProps,
  toolbarGroupRecipe,
  type ToolbarGroupRecipeProps,
  toolbarInputRecipe,
  type ToolbarInputRecipeProps,
  toolbarLinkRecipe,
  type ToolbarLinkRecipeProps,
  toolbarRootRecipe,
  type ToolbarRootRecipeProps,
  toolbarSeparatorRecipe,
  type ToolbarSeparatorRecipeProps,
} from "./toolbar.styles";

/**
 * The Toolbar anatomy, on Base UI's `Toolbar` parts.
 *
 * All six of Base UI's namespace members render a visible element, so all six
 * are wrapped here and the namespace keys mirror `@base-ui/react/toolbar` 1:1 —
 * a caller who knows the Base UI docs already knows this API. (Base UI also
 * exports an `Orientation` TYPE from this subpath; it is a type alias for
 * `'horizontal' | 'vertical'`, not a part, so it is not a namespace key.)
 *
 * A minimal usable set is `Root > Button…`; `Group`, `Separator`, `Link` and
 * `Input` are the optional structure around them.
 *
 * `Toolbar.Separator` is NOT the catalog's standalone `Separator`. It is a
 * different Base UI part with its own recipe, and its orientation DEFAULTS TO
 * THE OPPOSITE of the toolbar's — a horizontal toolbar renders vertical rules.
 *
 * Every part narrows `className` back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. The whole catalog makes this narrowing,
 * so a caller's `className` always means "utilities merged over the recipe,
 * last one wins".
 */

/**
 * The strip's props. Base UI's `Toolbar.Root` renders a `div role="toolbar"`
 * that owns the roving tab stop, and adds `orientation`, `disabled` and
 * `loopFocus`.
 */
export interface ToolbarRootProps
  extends Omit<ComponentProps<typeof BaseToolbar.Root>, "className">, ToolbarRootRecipeProps {
  readonly className?: string;
}

/**
 * A cluster's props. Base UI's `Toolbar.Group` renders a `div role="group"` and
 * can disable every item inside it at once.
 */
export interface ToolbarGroupProps
  extends Omit<ComponentProps<typeof BaseToolbar.Group>, "className">, ToolbarGroupRecipeProps {
  readonly className?: string;
}

/**
 * A control's props. Base UI's `Toolbar.Button` renders a `button` and adds
 * `disabled` plus `focusableWhenDisabled` — which DEFAULTS TO TRUE, so a
 * disabled item stays in the arrow-key order and is marked `aria-disabled`
 * rather than natively `disabled`.
 */
export interface ToolbarButtonProps
  extends Omit<ComponentProps<typeof BaseToolbar.Button>, "className">, ToolbarButtonRecipeProps {
  readonly className?: string;
}

/** A navigational item's props. Base UI's `Toolbar.Link` renders an `a`. */
export interface ToolbarLinkProps
  extends Omit<ComponentProps<typeof BaseToolbar.Link>, "className">, ToolbarLinkRecipeProps {
  readonly className?: string;
}

/**
 * A field's props. Base UI's `Toolbar.Input` renders an `input` that joins the
 * strip's roving focus, with the same `disabled`/`focusableWhenDisabled` pair as
 * the button.
 */
export interface ToolbarInputProps
  extends Omit<ComponentProps<typeof BaseToolbar.Input>, "className">, ToolbarInputRecipeProps {
  readonly className?: string;
}

/**
 * A rule's props. Base UI's `Toolbar.Separator` renders a
 * `div role="separator"` whose `orientation` defaults to the opposite of the
 * toolbar's.
 */
export interface ToolbarSeparatorProps
  extends
    Omit<ComponentProps<typeof BaseToolbar.Separator>, "className">,
    ToolbarSeparatorRecipeProps {
  readonly className?: string;
}

function ToolbarRoot({ className, ...rest }: ToolbarRootProps): ReactElement {
  return <BaseToolbar.Root className={cn(toolbarRootRecipe(), className)} {...rest} />;
}

function ToolbarGroup({ className, ...rest }: ToolbarGroupProps): ReactElement {
  return <BaseToolbar.Group className={cn(toolbarGroupRecipe(), className)} {...rest} />;
}

function ToolbarButton({ className, ...rest }: ToolbarButtonProps): ReactElement {
  return <BaseToolbar.Button className={cn(toolbarButtonRecipe(), className)} {...rest} />;
}

function ToolbarLink({ className, ...rest }: ToolbarLinkProps): ReactElement {
  return <BaseToolbar.Link className={cn(toolbarLinkRecipe(), className)} {...rest} />;
}

function ToolbarInput({ className, ...rest }: ToolbarInputProps): ReactElement {
  return <BaseToolbar.Input className={cn(toolbarInputRecipe(), className)} {...rest} />;
}

function ToolbarSeparator({ className, ...rest }: ToolbarSeparatorProps): ReactElement {
  return <BaseToolbar.Separator className={cn(toolbarSeparatorRecipe(), className)} {...rest} />;
}

/**
 * The catalog's toolbar, as ONE namespace object whose keys mirror Base UI's six
 * namespace members 1:1 — the catalog-wide convention for multi-part components.
 */
export const Toolbar = {
  Root: ToolbarRoot,
  Group: ToolbarGroup,
  Button: ToolbarButton,
  Link: ToolbarLink,
  Input: ToolbarInput,
  Separator: ToolbarSeparator,
};
