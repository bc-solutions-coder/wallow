import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per part of the tabs anatomy. Base UI's `tabs` subpath publishes
 * FIVE members and every one of them renders a visible element, so unlike the
 * dialog family there is nothing here to re-export unwrapped: Root, List, Tab,
 * Indicator and Panel each get a wrapper and a recipe.
 *
 * The class lists are not invented in this file — tabs.test.tsx declares each
 * part's exact utility set as a top-of-file `*_CLASSES` constant and asserts it
 * as an order-free set through the rendered component, so that spec is the
 * source of truth for every base string below.
 *
 * No recipe takes a cva VARIANT. Orientation is a real Base UI prop that drives
 * the composite's arrow-key axis and `aria-orientation`, and Base UI publishes
 * it back as `data-orientation` on all five parts, so the vertical layout is a
 * `data-[orientation=vertical]:` modifier rather than a cva variant a caller
 * would have to keep in step with the prop (the same call `menubarRecipe` makes,
 * for the same reason). Selected/disabled/hidden are likewise Base UI STATES.
 * The `VariantProps` types are still exported so each part's props keep the
 * catalog-wide shape and a later variant axis stays a non-breaking addition.
 */

/** The container: the strip stacked over the panels, side-by-side when vertical. */
export const tabsRootRecipe = cva(
  "flex flex-col gap-3 data-[orientation=vertical]:flex-row data-[orientation=vertical]:gap-4",
);

/** The root recipe's variant props, mixed into `TabsRootProps`. */
export type TabsRootRecipeProps = VariantProps<typeof tabsRootRecipe>;

/** The strip: a bottom-bordered rail, `relative` so the Indicator can hang off it. */
export const tabsListRecipe = cva(
  "relative flex items-center gap-1 border-b border-border data-[orientation=vertical]:flex-col data-[orientation=vertical]:items-stretch data-[orientation=vertical]:border-b-0 data-[orientation=vertical]:border-r",
);

/** The list recipe's variant props, mixed into `TabsListProps`. */
export type TabsListRecipeProps = VariantProps<typeof tabsListRecipe>;

/** An individual tab. Active state is a COLOUR change only — the Indicator carries the geometry. */
export const tabsTabRecipe = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-t-md px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:text-foreground data-[active]:text-foreground data-[disabled]:opacity-50",
);

/** The tab recipe's variant props, mixed into `TabsTabProps`. */
export type TabsTabRecipeProps = VariantProps<typeof tabsTabRecipe>;

/**
 * The sliding rule. Every dimension and offset comes from the six `--active-tab-*`
 * custom properties Base UI writes inline, so this recipe only paints and animates;
 * vertical flips the same measurements into a right-hand rule.
 */
export const tabsIndicatorRecipe = cva(
  "absolute bottom-0 left-0 h-0.5 w-[var(--active-tab-width)] translate-x-[var(--active-tab-left)] rounded-full bg-primary transition-all duration-150 data-[orientation=vertical]:top-0 data-[orientation=vertical]:right-0 data-[orientation=vertical]:bottom-auto data-[orientation=vertical]:left-auto data-[orientation=vertical]:h-[var(--active-tab-height)] data-[orientation=vertical]:w-0.5 data-[orientation=vertical]:translate-x-0 data-[orientation=vertical]:translate-y-[var(--active-tab-top)]",
);

/** The indicator recipe's variant props, mixed into `TabsIndicatorProps`. */
export type TabsIndicatorRecipeProps = VariantProps<typeof tabsIndicatorRecipe>;

/**
 * A panel. This recipe must NEVER set an unprefixed `display` utility: a
 * `keepMounted` inactive panel stays in the DOM with `hidden`, and any
 * unprefixed display here would beat the UA rule and paint it.
 */
export const tabsPanelRecipe = cva("text-sm text-foreground outline-none data-[hidden]:hidden");

/** The panel recipe's variant props, mixed into `TabsPanelProps`. */
export type TabsPanelRecipeProps = VariantProps<typeof tabsPanelRecipe>;
