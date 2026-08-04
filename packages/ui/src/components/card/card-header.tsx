import type { HTMLAttributes, ReactElement, ReactNode } from "react";

import { cn } from "../../core/cn";
import { MutedText } from "../muted-text/muted-text";
import { CardTitle } from "./card";

/**
 * `title` is a real `HTMLAttributes` member, so it is omitted from the
 * passthrough rather than shadowed: a spread of `rest` would otherwise stamp the
 * heading text onto the wrapper as a tooltip.
 */
export interface CardHeaderProps extends Omit<HTMLAttributes<HTMLDivElement>, "title"> {
  /** The card's heading. Rendered as the surface's `<h2>` by `CardTitle`. */
  readonly title: ReactNode;
  /** Optional supporting line beneath the title. Omitted entirely when absent. */
  readonly description?: ReactNode;
  /**
   * A testid for the `<h2>` itself, distinct from the wrapper's own
   * `data-testid` (which `rest` carries).
   *
   * Both exist because every wallow-auth `{screen}-heading` id names the heading
   * ELEMENT, and the wrapper also holds the description — so a text assertion
   * made against it would pass on copy the heading does not contain.
   */
  readonly titleTestId?: string;
}

/**
 * The card's title-and-description pair. Sourced from 11 local `CardHeading`
 * functions across wallow-auth, all of which rebuilt the same stack.
 *
 * Owning the `<h2>` here is the point: screens stop spelling out
 * `<Text as="h2" variant="subheading">`, so the card-heading step is guaranteed
 * by construction rather than by `wallow/text-heading-variant` catching a call
 * site. The description is omitted rather than emptied when absent — an empty
 * `<p>` would leave a rhythm gap under the screens that ship a bare heading.
 *
 * A caller `className` merges over the `space-y-1` rhythm (RegisterForm centres
 * its heading), and `data-testid` passes through to the wrapper.
 */
export function CardHeader({
  title,
  description,
  titleTestId,
  className,
  ...rest
}: CardHeaderProps): ReactElement {
  return (
    <div className={cn("space-y-1", className)} {...rest}>
      <CardTitle data-testid={titleTestId}>{title}</CardTitle>
      {description === undefined ? null : <MutedText>{description}</MutedText>}
    </div>
  );
}
