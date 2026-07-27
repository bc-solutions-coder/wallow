import { Tabs as BaseTabs } from "@base-ui/react/tabs";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  tabsIndicatorRecipe,
  type TabsIndicatorRecipeProps,
  tabsListRecipe,
  type TabsListRecipeProps,
  tabsPanelRecipe,
  type TabsPanelRecipeProps,
  tabsRootRecipe,
  type TabsRootRecipeProps,
  tabsTabRecipe,
  type TabsTabRecipeProps,
} from "./tabs.styles";

/**
 * The Tabs anatomy, on Base UI's `Tabs` parts.
 *
 * All five of Base UI's namespace members render a visible element, so all five
 * are wrapped here and the namespace keys mirror `@base-ui/react/tabs` 1:1 — a
 * caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable set is `Root > List(Tab…) + Panel…`; `Indicator` is the
 * opt-in sliding rule that tracks the active tab through the six
 * `--active-tab-*` custom properties Base UI writes onto it inline.
 *
 * Every part narrows `className` back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. The whole catalog makes this narrowing,
 * so a caller's `className` always means "utilities merged over the recipe,
 * last one wins".
 */

/**
 * The tabs container's props. Base UI's `Tabs.Root` renders a `div` that holds
 * both the list and the panels, and owns `value`/`defaultValue`,
 * `onValueChange` and `orientation`.
 */
export interface TabsRootProps
  extends Omit<ComponentProps<typeof BaseTabs.Root>, "className">, TabsRootRecipeProps {
  readonly className?: string;
}

/**
 * The tab strip's props. Base UI's `Tabs.List` renders a `div role="tablist"`
 * and adds the two composite-navigation knobs, `activateOnFocus` (arrow keys
 * activate as they move, rather than only moving focus) and `loopFocus`.
 */
export interface TabsListProps
  extends Omit<ComponentProps<typeof BaseTabs.List>, "className">, TabsListRecipeProps {
  readonly className?: string;
}

/** An individual tab's props. Base UI's `Tabs.Tab` renders a `button`. */
export interface TabsTabProps
  extends Omit<ComponentProps<typeof BaseTabs.Tab>, "className">, TabsTabRecipeProps {
  readonly className?: string;
}

/**
 * The sliding rule's props. Base UI's `Tabs.Indicator` renders a
 * `span role="presentation"` — and renders NOTHING at all while no tab is
 * active, so a recipe here styles a part that may legitimately be absent.
 */
export interface TabsIndicatorProps
  extends Omit<ComponentProps<typeof BaseTabs.Indicator>, "className">, TabsIndicatorRecipeProps {
  readonly className?: string;
}

/**
 * A panel's props. Base UI's `Tabs.Panel` renders a `div role="tabpanel"`, and
 * by default it is UNMOUNTED while its tab is inactive; `keepMounted` keeps it
 * in the DOM instead, `hidden` and `inert`.
 */
export interface TabsPanelProps
  extends Omit<ComponentProps<typeof BaseTabs.Panel>, "className">, TabsPanelRecipeProps {
  readonly className?: string;
}

function TabsRoot({ className, ...rest }: TabsRootProps): ReactElement {
  return <BaseTabs.Root className={cn(tabsRootRecipe(), className)} {...rest} />;
}

function TabsList({ className, ...rest }: TabsListProps): ReactElement {
  return <BaseTabs.List className={cn(tabsListRecipe(), className)} {...rest} />;
}

function TabsTab({ className, ...rest }: TabsTabProps): ReactElement {
  return <BaseTabs.Tab className={cn(tabsTabRecipe(), className)} {...rest} />;
}

function TabsIndicator({ className, ...rest }: TabsIndicatorProps): ReactElement {
  return <BaseTabs.Indicator className={cn(tabsIndicatorRecipe(), className)} {...rest} />;
}

function TabsPanel({ className, ...rest }: TabsPanelProps): ReactElement {
  return <BaseTabs.Panel className={cn(tabsPanelRecipe(), className)} {...rest} />;
}

/**
 * The catalog's tabs, as ONE namespace object whose keys mirror Base UI's five
 * namespace members 1:1 — the catalog-wide convention for multi-part
 * components.
 */
export const Tabs = {
  Root: TabsRoot,
  List: TabsList,
  Tab: TabsTab,
  Indicator: TabsIndicator,
  Panel: TabsPanel,
};
