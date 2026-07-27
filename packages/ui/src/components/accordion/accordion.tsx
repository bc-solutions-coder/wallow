import { Accordion as BaseAccordion } from "@base-ui/react/accordion";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  accordionHeaderRecipe,
  type AccordionHeaderRecipeProps,
  accordionItemRecipe,
  type AccordionItemRecipeProps,
  accordionPanelRecipe,
  type AccordionPanelRecipeProps,
  accordionRootRecipe,
  type AccordionRootRecipeProps,
  accordionTriggerRecipe,
  type AccordionTriggerRecipeProps,
} from "./accordion.styles";

/**
 * The Accordion anatomy, on Base UI's `Accordion` parts.
 *
 * Every part narrows `className` back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. The whole catalog makes this narrowing,
 * so a caller's `className` always means "utilities merged over the recipe,
 * last one wins".
 */

/**
 * The list wrapper's props. `Accordion.Root` is Base UI's one GENERIC part
 * (`<Value>`), so its props are read off the call signature with `Parameters`
 * rather than `ComponentProps`, which cannot see through a generic component —
 * the same treatment `Dialog.Root` gets. The generic collapses to its `any`
 * default, which is what `value`/`defaultValue` are typed as anyway.
 */
export interface AccordionRootProps
  extends Omit<Parameters<typeof BaseAccordion.Root>[0], "className">, AccordionRootRecipeProps {
  readonly className?: string;
}

/** One header/panel pair's props. Base UI's `Accordion.Item` renders a `div`. */
export interface AccordionItemProps
  extends Omit<ComponentProps<typeof BaseAccordion.Item>, "className">, AccordionItemRecipeProps {
  readonly className?: string;
}

/** The heading's props. Base UI's `Accordion.Header` renders an `h3`. */
export interface AccordionHeaderProps
  extends
    Omit<ComponentProps<typeof BaseAccordion.Header>, "className">,
    AccordionHeaderRecipeProps {
  readonly className?: string;
}

/** The trigger's props. Base UI's `Accordion.Trigger` renders a `button`. */
export interface AccordionTriggerProps
  extends
    Omit<ComponentProps<typeof BaseAccordion.Trigger>, "className">,
    AccordionTriggerRecipeProps {
  readonly className?: string;
}

/**
 * The panel's props. Base UI's `Accordion.Panel` renders a `div role="region"`,
 * and by default UNMOUNTS while closed — `keepMounted` keeps it in the DOM
 * behind a `hidden` attribute, and `hiddenUntilFound` (which overrides
 * `keepMounted`) uses `hidden="until-found"` so the browser's own find-in-page
 * can reveal it.
 */
export interface AccordionPanelProps
  extends Omit<ComponentProps<typeof BaseAccordion.Panel>, "className">, AccordionPanelRecipeProps {
  readonly className?: string;
}

function AccordionRoot({ className, ...rest }: AccordionRootProps): ReactElement {
  return <BaseAccordion.Root className={cn(accordionRootRecipe(), className)} {...rest} />;
}

function AccordionItem({ className, ...rest }: AccordionItemProps): ReactElement {
  return <BaseAccordion.Item className={cn(accordionItemRecipe(), className)} {...rest} />;
}

function AccordionHeader({ className, ...rest }: AccordionHeaderProps): ReactElement {
  return <BaseAccordion.Header className={cn(accordionHeaderRecipe(), className)} {...rest} />;
}

function AccordionTrigger({ className, ...rest }: AccordionTriggerProps): ReactElement {
  return <BaseAccordion.Trigger className={cn(accordionTriggerRecipe(), className)} {...rest} />;
}

function AccordionPanel({ className, ...rest }: AccordionPanelProps): ReactElement {
  return <BaseAccordion.Panel className={cn(accordionPanelRecipe(), className)} {...rest} />;
}

/**
 * The catalog's accordion, as ONE namespace object whose keys mirror Base UI's
 * five namespace members 1:1 — the catalog-wide convention for multi-part
 * components, so a caller who knows the Base UI docs already knows this API.
 *
 * A complete accordion is Root > Item > (Header > Trigger, Panel). The root is
 * single-select by default; pass `multiple` to let several panels stay open.
 */
export const Accordion = {
  Root: AccordionRoot,
  Item: AccordionItem,
  Header: AccordionHeader,
  Trigger: AccordionTrigger,
  Panel: AccordionPanel,
};
