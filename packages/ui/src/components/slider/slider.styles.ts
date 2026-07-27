import { cva, type VariantProps } from "class-variance-authority";

/*
 * No part takes a cva VARIANT. A slider has no visual variant axis in this
 * catalog: everything it looks like (disabled, vertical, dragging) is a STATE
 * that Base UI publishes as a `data-*` attribute, so it belongs in the base
 * string as a `data-[disabled]:` / `data-[orientation=vertical]:` modifier
 * rather than as a cva variant nobody would pass by hand. The `VariantProps`
 * types are still exported so the prop interfaces keep the catalog-wide shape
 * and a later variant axis is a non-breaking addition.
 */

/**
 * The wrapper that stacks label, value and control — Base UI's `Slider.Root`, a
 * `<div role="group">`.
 *
 * `data-disabled` lands on all seven parts, so the dimming lives HERE and
 * nowhere else: repeating `opacity-50` on a descendant would compound with this
 * one and render the nested parts at 25%.
 */
export const sliderRootRecipe = cva("flex w-full flex-col gap-2 data-[disabled]:opacity-50");

/** The recipe's variant props, mixed into `SliderRootProps`. */
export type SliderRootRecipeProps = VariantProps<typeof sliderRootRecipe>;

/** The slider's accessible name — Base UI's `Slider.Label`, a `<div>`. */
export const sliderLabelRecipe = cva("text-sm font-medium text-foreground");

/** The recipe's variant props, mixed into `SliderLabelProps`. */
export type SliderLabelRecipeProps = VariantProps<typeof sliderLabelRecipe>;

/**
 * The live readout of the current value — Base UI's `Slider.Value`, an
 * `<output>`. `tabular-nums` keeps the row from twitching as the digit widths
 * change while the thumb moves.
 */
export const sliderValueRecipe = cva("text-sm tabular-nums text-muted-foreground");

/** The recipe's variant props, mixed into `SliderValueProps`. */
export type SliderValueRecipeProps = VariantProps<typeof sliderValueRecipe>;

/**
 * The interactive surface the track and thumbs sit on — Base UI's
 * `Slider.Control`, a `<div>`.
 *
 * `touch-none` is not decoration: without it a drag on a touch device scrolls
 * the page instead of moving the thumb. The `py-3` padding is the hit area —
 * the rail itself is only 6px tall, well under a comfortable touch target.
 */
export const sliderControlRecipe = cva(
  "flex w-full touch-none select-none items-center py-3 data-[orientation=vertical]:h-48 data-[orientation=vertical]:w-auto data-[orientation=vertical]:flex-col data-[orientation=vertical]:px-3",
);

/** The recipe's variant props, mixed into `SliderControlProps`. */
export type SliderControlRecipeProps = VariantProps<typeof sliderControlRecipe>;

/**
 * The full range rail — Base UI's `Slider.Track`, a `<div>`. Thin on its cross
 * axis and full-length on its main one, which swap when the orientation does.
 */
export const sliderTrackRecipe = cva(
  "h-1.5 w-full rounded-full bg-input data-[orientation=vertical]:h-full data-[orientation=vertical]:w-1.5",
);

/** The recipe's variant props, mixed into `SliderTrackProps`. */
export type SliderTrackRecipeProps = VariantProps<typeof sliderTrackRecipe>;

/**
 * The filled portion of the rail — Base UI's `Slider.Indicator`, a `<div>`.
 *
 * Colour and shape ONLY. Base UI writes the indicator's position and size as
 * inline styles derived from the current value, so any sizing utility here
 * would either be overridden or fight it.
 */
export const sliderIndicatorRecipe = cva("rounded-full bg-primary");

/** The recipe's variant props, mixed into `SliderIndicatorProps`. */
export type SliderIndicatorRecipeProps = VariantProps<typeof sliderIndicatorRecipe>;

/**
 * The draggable handle — Base UI's `Slider.Thumb`, a `<div>` wrapping a hidden
 * `<input type="range">`. It carries its own size because the rail it sits on
 * is deliberately thinner than a grabbable handle.
 */
export const sliderThumbRecipe = cva(
  "size-4 rounded-full border border-border bg-background shadow-sm",
);

/** The recipe's variant props, mixed into `SliderThumbProps`. */
export type SliderThumbRecipeProps = VariantProps<typeof sliderThumbRecipe>;
