import type { HTMLAttributes, ReactElement } from "react";

import { Text } from "../text/text";

/**
 * Paragraph attributes for small supporting copy. The element, text scale, and muted color are
 * fixed.
 */
export type MutedTextProps = HTMLAttributes<HTMLParagraphElement>;

/**
 * Renders a small muted paragraph for supporting copy. className can override its typography
 * and spacing.
 */
export function MutedText({ className, ...rest }: MutedTextProps): ReactElement {
  return <Text {...rest} as="p" variant="bodySm" color="muted" className={className} />;
}
