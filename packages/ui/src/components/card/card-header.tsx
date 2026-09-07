import type { HTMLAttributes, ReactElement, ReactNode } from "react";

import { cn } from "../../core/cn";
import { MutedText } from "../muted-text/muted-text";
import { CardTitle } from "./card";

/** Div attributes and heading content for CardHeader. title supplies the heading, not a native tooltip. */
export interface CardHeaderProps extends Omit<HTMLAttributes<HTMLDivElement>, "title"> {
  /** The card's heading. Rendered as the surface's `<h2>` by `CardTitle`. */
  readonly title: ReactNode;
  /** Optional supporting line beneath the title. Omitted entirely when absent. */
  readonly description?: ReactNode;
  /**
   * A testid for the `<h2>` itself, distinct from the wrapper's own
   * `data-testid` (which `rest` carries).
   */
  readonly titleTestId?: string;
}

/** Renders a card h2 and an optional supporting paragraph. className styles the wrapper; titleTestId targets the heading. */
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
