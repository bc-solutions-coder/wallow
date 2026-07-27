import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per part of the accordion. The class lists are not invented here —
 * accordion.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set THROUGH the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * All five Base UI parts render a visible element (`div` / `div` / `h3` /
 * `button` / `div`), so all five get a recipe.
 *
 * No recipe takes a cva VARIANT. An accordion has no visual variant axis in this
 * catalog: open/closed, disabled and the enter/exit transition phases are all
 * STATES, which Base UI publishes as `data-*` attributes, so they belong in the
 * base string as `data-[disabled]:` / `data-[starting-style]:` /
 * `data-[ending-style]:` modifiers rather than as cva variants nobody would pass
 * by hand. The `VariantProps` aliases are still exported so each part's props
 * keep the catalog-wide shape and a later variant stays a non-breaking addition.
 */

/**
 * The list wrapper — Base UI's `Accordion.Root`, a `<div>`. Owns the frame; the
 * items own the rules between them.
 */
export const accordionRootRecipe = cva("w-full overflow-hidden rounded-md border border-border");

/** The root recipe's variant props, mixed into `AccordionRootProps`. */
export type AccordionRootRecipeProps = VariantProps<typeof accordionRootRecipe>;

/**
 * One header/panel pair — Base UI's `Accordion.Item`, a `<div>`. Separated from
 * the next by a rule, which the last item drops so it never doubles the root's
 * own bottom border.
 */
export const accordionItemRecipe = cva("border-b border-border last:border-b-0");

/** The item recipe's variant props, mixed into `AccordionItemProps`. */
export type AccordionItemRecipeProps = VariantProps<typeof accordionItemRecipe>;

/**
 * The heading that labels a panel — Base UI's `Accordion.Header`, an `<h3>`. A
 * flex row so the trigger inside it can stretch to full width.
 */
export const accordionHeaderRecipe = cva("flex");

/** The header recipe's variant props, mixed into `AccordionHeaderProps`. */
export type AccordionHeaderRecipeProps = VariantProps<typeof accordionHeaderRecipe>;

/**
 * The button that opens a panel — Base UI's `Accordion.Trigger`, a `<button>`.
 *
 * `data-[disabled]:` is the only state modifier here. The trigger's OPEN state is
 * published as `data-panel-open`, never `data-open` (measured against Base UI
 * 1.6.0), so a `data-[open]:` modifier on this recipe would silently never fire —
 * and the open affordance belongs to the chevron a caller renders inside anyway.
 */
export const accordionTriggerRecipe = cva(
  "flex w-full items-center justify-between gap-2 px-4 py-3 text-left text-sm font-medium text-foreground outline-none transition-colors hover:bg-accent hover:text-accent-foreground data-[disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `AccordionTriggerProps`. */
export type AccordionTriggerRecipeProps = VariantProps<typeof accordionTriggerRecipe>;

/**
 * The collapsing body — Base UI's `Accordion.Panel`, a `<div role="region">`.
 * This is the only recipe in the pair that does real work: Base UI publishes the
 * measured height as the `--accordion-panel-height` custom property, so the
 * collapse is a plain `height` transition from `0` in the starting/ending phases
 * to that variable, with `overflow-hidden` clipping the content on the way.
 *
 * Deliberately NO padding: the height variable is measured off THIS element, so
 * padding here would be animated too and the panel would never fully close. The
 * padding belongs on a wrapper inside the panel.
 *
 * The custom property is `--accordion-panel-height`; `../collapsible` publishes
 * its own `--collapsible-panel-height`, and the two recipes are otherwise the
 * same shape, so do not copy one variable onto the other.
 */
export const accordionPanelRecipe = cva(
  "h-[var(--accordion-panel-height)] overflow-hidden text-sm text-muted-foreground transition-[height] duration-150 data-[starting-style]:h-0 data-[ending-style]:h-0",
);

/** The panel recipe's variant props, mixed into `AccordionPanelProps`. */
export type AccordionPanelRecipeProps = VariantProps<typeof accordionPanelRecipe>;
