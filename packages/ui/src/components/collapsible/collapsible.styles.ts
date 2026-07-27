import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per part of the collapsible. The class lists are not invented here —
 * collapsible.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set THROUGH the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * All three Base UI parts render a visible element (`div` / `button` / `div`), so
 * all three get a recipe.
 *
 * No recipe takes a cva VARIANT, for the same reason as `../accordion`:
 * open/closed, disabled and the enter/exit transition phases are STATES that Base
 * UI publishes as `data-*` attributes, so they belong in the base string as
 * `data-[disabled]:` / `data-[starting-style]:` / `data-[ending-style]:`
 * modifiers rather than as cva variants nobody would pass by hand.
 */

/**
 * The wrapper — Base UI's `Collapsible.Root`, a `<div>`. Deliberately layout-only:
 * a collapsible is embedded in whatever frame the caller already has, so a border
 * here would fight it (unlike `Accordion.Root`, which owns its own frame).
 */
export const collapsibleRootRecipe = cva("w-full");

/** The root recipe's variant props, mixed into `CollapsibleRootProps`. */
export type CollapsibleRootRecipeProps = VariantProps<typeof collapsibleRootRecipe>;

/**
 * The button that opens the panel — Base UI's `Collapsible.Trigger`, a `<button>`.
 * Same treatment as the accordion's trigger minus the full-width row: a standalone
 * collapsible's trigger sits inline next to other content.
 *
 * As on the accordion's trigger, `data-[disabled]:` is the only state modifier —
 * the open state is published as `data-panel-open`, never `data-open`, so a
 * `data-[open]:` modifier here would silently never fire.
 */
export const collapsibleTriggerRecipe = cva(
  "inline-flex items-center justify-between gap-2 rounded-md px-3 py-2 text-sm font-medium text-foreground outline-none transition-colors hover:bg-accent hover:text-accent-foreground data-[disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `CollapsibleTriggerProps`. */
export type CollapsibleTriggerRecipeProps = VariantProps<typeof collapsibleTriggerRecipe>;

/**
 * The collapsing body — Base UI's `Collapsible.Panel`, a plain `<div>`. Base UI
 * publishes the measured height as the `--collapsible-panel-height` custom
 * property — NOT `--accordion-panel-height`, which is the easiest silent mistake
 * to make since the two panel recipes are otherwise the same shape — so the
 * collapse is a plain `height` transition from `0` in the starting/ending phases
 * to that variable, clipped by `overflow-hidden`.
 *
 * No padding, for the same reason as the accordion's panel: the height variable is
 * measured off THIS element, so padding here would be animated too and the panel
 * would never fully close.
 */
export const collapsiblePanelRecipe = cva(
  "h-[var(--collapsible-panel-height)] overflow-hidden text-sm text-muted-foreground transition-[height] duration-150 data-[starting-style]:h-0 data-[ending-style]:h-0",
);

/** The panel recipe's variant props, mixed into `CollapsiblePanelProps`. */
export type CollapsiblePanelRecipeProps = VariantProps<typeof collapsiblePanelRecipe>;
