import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the meter. The class lists are not invented
 * here — meter.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set THROUGH the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * No part takes a cva VARIANT, and unlike its sibling Progress, Meter has no
 * `data-[…]:` modifiers either: Base UI's `MeterRootState` is EMPTY and the
 * meter publishes NO state attribute on any part (measured, not assumed). A
 * meter is a static reading, so every recipe here is an unconditional string.
 * The `VariantProps` types are still exported so the prop interfaces keep the
 * catalog-wide shape and a later variant axis stays a non-breaking addition.
 *
 * These recipes deliberately do NOT import Progress's: the two components look
 * alike today but answer to different Base UI surfaces, and coupling them would
 * make any future progress-only state modifier leak onto a meter.
 */

/** The wrapper that stacks label, value and track — Base UI's `Meter.Root`. */
export const meterRootRecipe = cva("flex w-full flex-col gap-2");

/** The recipe's variant props, mixed into `MeterRootProps`. */
export type MeterRootRecipeProps = VariantProps<typeof meterRootRecipe>;

/** The meter's accessible name — Base UI's `Meter.Label`. */
export const meterLabelRecipe = cva("text-sm font-medium text-foreground");

/** The recipe's variant props, mixed into `MeterLabelProps`. */
export type MeterLabelRecipeProps = VariantProps<typeof meterLabelRecipe>;

/** The readout of the current reading — Base UI's `Meter.Value`. */
export const meterValueRecipe = cva("text-sm tabular-nums text-muted-foreground");

/** The recipe's variant props, mixed into `MeterValueProps`. */
export type MeterValueRecipeProps = VariantProps<typeof meterValueRecipe>;

/**
 * The full range of the meter — Base UI's `Meter.Track`.
 *
 * `h-2` is load-bearing, not decoration: Base UI sizes the indicator with an
 * inline `height: inherit`, so the bar's whole height comes from this recipe.
 * Drop it and the bar collapses to 0px while every markup assertion still passes.
 */
export const meterTrackRecipe = cva("h-2 w-full overflow-hidden rounded-full bg-input");

/** The recipe's variant props, mixed into `MeterTrackProps`. */
export type MeterTrackRecipeProps = VariantProps<typeof meterTrackRecipe>;

/**
 * The filled portion of the range — Base UI's `Meter.Indicator`.
 *
 * This recipe PAINTS ONLY: the width is an inline style Base UI computes from
 * `value`/`min`/`max`, and unlike Progress's indicator that style is ALWAYS
 * present, so a width utility here could never win.
 */
export const meterIndicatorRecipe = cva(
  "rounded-full bg-primary transition-[width] duration-200 ease-out",
);

/** The recipe's variant props, mixed into `MeterIndicatorProps`. */
export type MeterIndicatorRecipeProps = VariantProps<typeof meterIndicatorRecipe>;
