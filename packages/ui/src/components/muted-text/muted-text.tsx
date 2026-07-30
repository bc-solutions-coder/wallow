import type { HTMLAttributes, ReactElement } from "react";

import { Text } from "../text/text";

/**
 * The shared muted paragraph. Sourced from 41x the muted small-body recipe in
 * wallow-auth — the strongest single recipe in the inventory. It is `Text` at
 * the small-body scale in the muted colour, and composes over it so ONE recipe
 * backs both; children and data-testid pass through, and a caller `className`
 * is merged over the recipe by `Text`.
 *
 * The passthrough spreads FIRST so the element, the scale and the colour stay
 * this component's decisions: `HTMLAttributes` carries the non-standard `color`
 * string attribute, which would otherwise land on `Text`'s semantic colour.
 */
export type MutedTextProps = HTMLAttributes<HTMLParagraphElement>;

export function MutedText({ className, ...rest }: MutedTextProps): ReactElement {
  return <Text {...rest} as="p" variant="bodySm" color="muted" className={className} />;
}
