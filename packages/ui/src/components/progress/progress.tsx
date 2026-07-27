import { Progress as BaseProgress } from "@base-ui/react/progress";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  progressIndicatorRecipe,
  type ProgressIndicatorRecipeProps,
  progressLabelRecipe,
  type ProgressLabelRecipeProps,
  progressRootRecipe,
  type ProgressRootRecipeProps,
  progressTrackRecipe,
  type ProgressTrackRecipeProps,
  progressValueRecipe,
  type ProgressValueRecipeProps,
} from "./progress.styles";

/**
 * The Progress anatomy, on Base UI's `Progress` parts.
 *
 * All five of Base UI's namespace members render a visible element, so all five
 * are wrapped here and the namespace keys mirror `@base-ui/react/progress` 1:1 —
 * a caller who knows the Base UI docs already knows this API. (Base UI also
 * exports a `Status` TYPE from this subpath; it is a type, not a part, and is
 * re-exported below as `ProgressStatus` rather than made a namespace key.)
 *
 * A minimal usable set is `Root > Track > Indicator`; `Label` and `Value` are
 * the opt-in text parts, and `Label` is what wires `aria-labelledby` on the
 * root's `role="progressbar"`.
 *
 * Every part narrows `className` back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. The whole catalog makes this narrowing,
 * so a caller's `className` always means "utilities merged over the recipe,
 * last one wins".
 *
 */

/** Base UI's three completion states, published as `data-<status>` on every part. */
export type ProgressStatus = BaseProgress.Root.State["status"];

/**
 * The progress bar's props. Base UI's `Progress.Root` renders a
 * `div role="progressbar"` and owns the whole numeric contract: `value`
 * (REQUIRED, and `null` means indeterminate), `min`/`max`, plus the `format` /
 * `locale` / `getAriaValueText` knobs that turn the number into text.
 */
export interface ProgressRootProps
  extends Omit<ComponentProps<typeof BaseProgress.Root>, "className">, ProgressRootRecipeProps {
  readonly className?: string;
}

/**
 * The label's props. Base UI's `Progress.Label` renders a
 * `span role="presentation"` and registers its own `id` as the root's
 * `aria-labelledby`, so the bar is named without a `for`/`id` pair of the
 * caller's own.
 */
export interface ProgressLabelProps
  extends Omit<ComponentProps<typeof BaseProgress.Label>, "className">, ProgressLabelRecipeProps {
  readonly className?: string;
}

/**
 * The readout's props. Base UI's `Progress.Value` renders a
 * `span aria-hidden="true"` (the root's `aria-valuetext` is the accessible
 * readout, so the visible one must not be announced twice). Its `children` is
 * not a node but a RENDER FUNCTION `(formattedValue, value) => ReactNode`.
 */
export interface ProgressValueProps
  extends Omit<ComponentProps<typeof BaseProgress.Value>, "className">, ProgressValueRecipeProps {
  readonly className?: string;
}

/** The rail's props. Base UI's `Progress.Track` renders a plain `div`. */
export interface ProgressTrackProps
  extends Omit<ComponentProps<typeof BaseProgress.Track>, "className">, ProgressTrackRecipeProps {
  readonly className?: string;
}

/**
 * The fill's props. Base UI's `Progress.Indicator` renders a `div` whose
 * `width` is an INLINE STYLE Base UI computes from `value`/`min`/`max`
 * (`insetInlineStart: 0; height: inherit; width: <n>%`), so the recipe may
 * paint it but must never size it. While indeterminate it gets no inline style
 * at all, which is what leaves room for a `data-[indeterminate]:` width.
 */
export interface ProgressIndicatorProps
  extends
    Omit<ComponentProps<typeof BaseProgress.Indicator>, "className">,
    ProgressIndicatorRecipeProps {
  readonly className?: string;
}

function ProgressRoot({ className, ...rest }: ProgressRootProps): ReactElement {
  return <BaseProgress.Root className={cn(progressRootRecipe(), className)} {...rest} />;
}

function ProgressLabel({ className, ...rest }: ProgressLabelProps): ReactElement {
  return <BaseProgress.Label className={cn(progressLabelRecipe(), className)} {...rest} />;
}

function ProgressValue({ className, ...rest }: ProgressValueProps): ReactElement {
  return <BaseProgress.Value className={cn(progressValueRecipe(), className)} {...rest} />;
}

function ProgressTrack({ className, ...rest }: ProgressTrackProps): ReactElement {
  return <BaseProgress.Track className={cn(progressTrackRecipe(), className)} {...rest} />;
}

function ProgressIndicator({ className, ...rest }: ProgressIndicatorProps): ReactElement {
  return <BaseProgress.Indicator className={cn(progressIndicatorRecipe(), className)} {...rest} />;
}

/**
 * The catalog's progress bar, as ONE namespace object whose keys mirror Base
 * UI's five namespace members 1:1 — the catalog-wide convention for multi-part
 * components.
 */
export const Progress = {
  Root: ProgressRoot,
  Label: ProgressLabel,
  Value: ProgressValue,
  Track: ProgressTrack,
  Indicator: ProgressIndicator,
};
