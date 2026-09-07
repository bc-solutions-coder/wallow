import { cva, type VariantProps } from "class-variance-authority";

/* Recipes style visible component parts. State transitions use Base UI data attributes; state-only roots and structural portals have no recipe. */

/** The wrapper that stacks label, value and track — Base UI's `Progress.Root`. */
export const progressRootRecipe = cva("flex w-full flex-col gap-2");

/** The recipe's variant props, mixed into `ProgressRootProps`. */
export type ProgressRootRecipeProps = VariantProps<typeof progressRootRecipe>;

/** The bar's accessible name — Base UI's `Progress.Label`. */
export const progressLabelRecipe = cva("text-sm font-medium text-foreground");

/** The recipe's variant props, mixed into `ProgressLabelProps`. */
export type ProgressLabelRecipeProps = VariantProps<typeof progressLabelRecipe>;

/** The readout of the current value — Base UI's `Progress.Value`. */
export const progressValueRecipe = cva("text-sm tabular-nums text-muted-foreground");

/** The recipe's variant props, mixed into `ProgressValueProps`. */
export type ProgressValueRecipeProps = VariantProps<typeof progressValueRecipe>;

/**
 * The full-length rail the indicator fills — Base UI's `Progress.Track`.
 *
 * `h-2` is load-bearing, not decoration: Base UI sizes the indicator with an
 * inline `height: inherit`, so the bar's whole height comes from this recipe.
 * Drop it and the bar collapses to 0px while every markup assertion still passes.
 */
export const progressTrackRecipe = cva("h-2 w-full overflow-hidden rounded-full bg-input");

/** The recipe's variant props, mixed into `ProgressTrackProps`. */
export type ProgressTrackRecipeProps = VariantProps<typeof progressTrackRecipe>;

/**
 * The filled portion of the rail — Base UI's `Progress.Indicator`.
 *
 * This recipe PAINTS ONLY. The width is an inline style Base UI computes from
 * `value`/`min`/`max`, so a width utility here would be dead weight — except
 * while INDETERMINATE, where Base UI writes no inline style at all and the
 * `data-[indeterminate]:` pair is the only thing sizing and animating the bar.
 */
export const progressIndicatorRecipe = cva(
  "rounded-full bg-primary transition-[width] duration-200 ease-out data-[indeterminate]:w-full data-[indeterminate]:animate-pulse",
);

/** The recipe's variant props, mixed into `ProgressIndicatorProps`. */
export type ProgressIndicatorRecipeProps = VariantProps<typeof progressIndicatorRecipe>;
