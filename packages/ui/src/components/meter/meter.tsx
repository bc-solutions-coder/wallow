import { Meter as BaseMeter } from "@base-ui/react/meter";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  meterIndicatorRecipe,
  type MeterIndicatorRecipeProps,
  meterLabelRecipe,
  type MeterLabelRecipeProps,
  meterRootRecipe,
  type MeterRootRecipeProps,
  meterTrackRecipe,
  type MeterTrackRecipeProps,
  meterValueRecipe,
  type MeterValueRecipeProps,
} from "./meter.styles";

/**
 * The Meter anatomy, on Base UI's `Meter` parts.
 *
 * All five of Base UI's namespace members render a visible element, so all five
 * are wrapped here and the namespace keys mirror `@base-ui/react/meter` 1:1 — a
 * caller who knows the Base UI docs already knows this API.
 *
 * A meter is NOT a progress bar and the two are not interchangeable: `role`
 * is `meter`, the reading is a static quantity within a known range (disk used,
 * quota consumed, score) rather than the completion of a task, so there is no
 * indeterminate state and no status attribute at all. Reach for `Progress` when
 * something is finishing.
 *
 * Every part narrows `className` back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. The whole catalog makes this narrowing,
 * so a caller's `className` always means "utilities merged over the recipe,
 * last one wins".
 *
 */

/**
 * The meter's props. Base UI's `Meter.Root` renders a `div role="meter"` and
 * owns the numeric contract: `value` (REQUIRED, and unlike Progress it may not
 * be `null`), `min`/`max`, plus the `format` / `locale` / `getAriaValueText`
 * knobs. A value outside the range is CLAMPED rather than rejected.
 */
export interface MeterRootProps
  extends Omit<ComponentProps<typeof BaseMeter.Root>, "className">, MeterRootRecipeProps {
  readonly className?: string;
}

/**
 * The label's props. Base UI's `Meter.Label` renders a
 * `span role="presentation"` and registers its own `id` as the root's
 * `aria-labelledby`, so the meter is named without a `for`/`id` pair of the
 * caller's own.
 */
export interface MeterLabelProps
  extends Omit<ComponentProps<typeof BaseMeter.Label>, "className">, MeterLabelRecipeProps {
  readonly className?: string;
}

/**
 * The readout's props. Base UI's `Meter.Value` renders a
 * `span aria-hidden="true"` (the root's `aria-valuetext` is the accessible
 * readout, so the visible one must not be announced twice). Its `children` is
 * not a node but a RENDER FUNCTION `(formattedValue, value) => ReactNode`, and
 * the `formattedValue` it receives is the value's POSITION IN THE RANGE as a
 * percent — not the raw number, which is where Meter and Progress diverge.
 */
export interface MeterValueProps
  extends Omit<ComponentProps<typeof BaseMeter.Value>, "className">, MeterValueRecipeProps {
  readonly className?: string;
}

/** The range's props. Base UI's `Meter.Track` renders a plain `div`. */
export interface MeterTrackProps
  extends Omit<ComponentProps<typeof BaseMeter.Track>, "className">, MeterTrackRecipeProps {
  readonly className?: string;
}

/**
 * The fill's props. Base UI's `Meter.Indicator` renders a `div` whose `width`
 * is an INLINE STYLE Base UI computes from `value`/`min`/`max`
 * (`insetInlineStart: 0; height: inherit; width: <n>%`), so the recipe may
 * paint it but must never size it. Unlike Progress's indicator this style is
 * ALWAYS present — a meter has no indeterminate state to omit it for.
 */
export interface MeterIndicatorProps
  extends Omit<ComponentProps<typeof BaseMeter.Indicator>, "className">, MeterIndicatorRecipeProps {
  readonly className?: string;
}

function MeterRoot({ className, ...rest }: MeterRootProps): ReactElement {
  return <BaseMeter.Root className={cn(meterRootRecipe(), className)} {...rest} />;
}

function MeterLabel({ className, ...rest }: MeterLabelProps): ReactElement {
  return <BaseMeter.Label className={cn(meterLabelRecipe(), className)} {...rest} />;
}

function MeterValue({ className, ...rest }: MeterValueProps): ReactElement {
  return <BaseMeter.Value className={cn(meterValueRecipe(), className)} {...rest} />;
}

function MeterTrack({ className, ...rest }: MeterTrackProps): ReactElement {
  return <BaseMeter.Track className={cn(meterTrackRecipe(), className)} {...rest} />;
}

function MeterIndicator({ className, ...rest }: MeterIndicatorProps): ReactElement {
  return <BaseMeter.Indicator className={cn(meterIndicatorRecipe(), className)} {...rest} />;
}

/**
 * The catalog's meter, as ONE namespace object whose keys mirror Base UI's five
 * namespace members 1:1 — the catalog-wide convention for multi-part
 * components.
 */
export const Meter = {
  Root: MeterRoot,
  Label: MeterLabel,
  Value: MeterValue,
  Track: MeterTrack,
  Indicator: MeterIndicator,
};
