import { Slider as BaseSlider } from "@base-ui/react/slider";
import type { ComponentProps, ReactElement } from "react";
import { cn } from "../../core/cn";
import {
  sliderControlRecipe,
  sliderIndicatorRecipe,
  sliderLabelRecipe,
  sliderRootRecipe,
  sliderThumbRecipe,
  sliderTrackRecipe,
  sliderValueRecipe,
} from "./slider.styles";

/**
 * Every Base UI `Slider.Root` prop (`value`, `defaultValue`, `onValueChange`,
 * `onValueCommitted`, `min`, `max`, `step`, `largeStep`, `minStepsBetweenValues`,
 * `orientation`, `format`, `locale`, `disabled`, `name`, `form`,
 * `thumbAlignment`, `thumbCollisionBehavior`, `render`).
 *
 * Base UI's `Slider.Root` is generic over its value (`number` for a single
 * thumb, `readonly number[]` for a range). This interface pins that generic to
 * its default — the union of both — because the catalog's parts are plain
 * non-generic components; a caller who wants `onValueChange` narrowed to
 * `number` narrows it at the call site.
 */
export interface SliderRootProps extends Omit<ComponentProps<typeof BaseSlider.Root>, "className"> {
  readonly className?: string;
}

/** Every Base UI `Slider.Label` prop, with `className` narrowed the same way. */
export interface SliderLabelProps extends Omit<
  ComponentProps<typeof BaseSlider.Label>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `Slider.Value` prop, with `className` narrowed the same way. */
export interface SliderValueProps extends Omit<
  ComponentProps<typeof BaseSlider.Value>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `Slider.Control` prop, with `className` narrowed the same way. */
export interface SliderControlProps extends Omit<
  ComponentProps<typeof BaseSlider.Control>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `Slider.Track` prop, with `className` narrowed the same way. */
export interface SliderTrackProps extends Omit<
  ComponentProps<typeof BaseSlider.Track>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `Slider.Indicator` prop, with `className` narrowed the same way. */
export interface SliderIndicatorProps extends Omit<
  ComponentProps<typeof BaseSlider.Indicator>,
  "className"
> {
  readonly className?: string;
}

/**
 * Every Base UI `Slider.Thumb` prop (`index`, `inputRef`, `disabled`,
 * `getAriaLabel`, `getAriaValueText`, `onFocus`/`onBlur`/`onKeyDown` — which
 * Base UI forwards to the nested `<input type="range">`, not to the thumb
 * element), with `className` narrowed the same way.
 */
export interface SliderThumbProps extends Omit<
  ComponentProps<typeof BaseSlider.Thumb>,
  "className"
> {
  readonly className?: string;
}

/**
 * The wrapper that owns the value and hands it to every other part through
 * context. Renders a `<div role="group">`.
 */
function SliderRoot({ className, ...rest }: SliderRootProps): ReactElement {
  return <BaseSlider.Root className={cn(sliderRootRecipe(), className)} {...rest} />;
}

/**
 * The slider's accessible name. Base UI wires it to the thumbs' hidden inputs
 * by id, so this is the labelling path — a plain `<label>` cannot reach them.
 * Renders a `<div>`.
 */
function SliderLabel({ className, ...rest }: SliderLabelProps): ReactElement {
  return <BaseSlider.Label className={cn(sliderLabelRecipe(), className)} {...rest} />;
}

/**
 * The formatted readout of the current value. Renders an `<output>`; pass a
 * function child to format the values yourself.
 */
function SliderValue({ className, ...rest }: SliderValueProps): ReactElement {
  return <BaseSlider.Value className={cn(sliderValueRecipe(), className)} {...rest} />;
}

/**
 * The interactive surface: the box the user presses and drags within. Renders
 * a `<div>` and must contain the track.
 */
function SliderControl({ className, ...rest }: SliderControlProps): ReactElement {
  return <BaseSlider.Control className={cn(sliderControlRecipe(), className)} {...rest} />;
}

/** The rail spanning the whole `min`..`max` range. Renders a `<div>`. */
function SliderTrack({ className, ...rest }: SliderTrackProps): ReactElement {
  return <BaseSlider.Track className={cn(sliderTrackRecipe(), className)} {...rest} />;
}

/**
 * The filled part of the rail. Base UI positions and sizes it inline from the
 * current value, so the recipe may only carry colour and shape.
 */
function SliderIndicator({ className, ...rest }: SliderIndicatorProps): ReactElement {
  return <BaseSlider.Indicator className={cn(sliderIndicatorRecipe(), className)} {...rest} />;
}

/**
 * The draggable handle. Renders a `<div>` wrapping a visually hidden
 * `<input type="range">` that carries the value into form submissions and is
 * the element keyboard interaction actually happens on. A range slider renders
 * one per value, each with its own `index`.
 */
function SliderThumb({ className, ...rest }: SliderThumbProps): ReactElement {
  return <BaseSlider.Thumb className={cn(sliderThumbRecipe(), className)} {...rest} />;
}

/**
 * A slider for a number or range. Root owns values and bounds; Control contains Track,
 * Indicator, and one Thumb per value. Label and Value provide the name and displayed reading.
 */
export const Slider = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: SliderRoot,
  /**
   * Provides the control's accessible label.
   */
  Label: SliderLabel,
  /**
   * Displays the current value using the part's children or formatting options.
   */
  Value: SliderValue,
  /**
   * The interactive control connected to Root state.
   */
  Control: SliderControl,
  /**
   * Provides the full range or scroll track behind the indicator.
   */
  Track: SliderTrack,
  /**
   * Displays the control's current state or selected extent.
   */
  Indicator: SliderIndicator,
  /**
   * The movable or state-positioned part of the control.
   */
  Thumb: SliderThumb,
};
