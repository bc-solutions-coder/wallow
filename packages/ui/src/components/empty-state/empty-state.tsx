import type { HTMLAttributes, ReactElement, ReactNode } from "react";

import { Card } from "../card/card";
import { Text } from "../text/text";
import {
  emptyStateActionRecipe,
  emptyStateIconRecipe,
  emptyStateRecipe,
} from "./empty-state.styles";

/**
 * The "nothing here yet" card every list route needs: a decorative icon, the
 * sentence that says what is missing, optional supporting copy, and an optional
 * action that creates the first item.
 *
 * `children` is omitted from the passthrough — every slot is a named prop, so
 * there is exactly one way to put content in the card.
 */
export type EmptyStateProps = Omit<HTMLAttributes<HTMLDivElement>, "children"> & {
  /** The decorative icon or emoji above the message. */
  readonly icon?: ReactNode;
  /** What is missing, rendered through `Text` as the card's `<h2>`. */
  readonly message: ReactNode;
  /** Optional supporting copy under the message. */
  readonly description?: ReactNode;
  /** Optional call to action under the copy — typically a `Button`. */
  readonly action?: ReactNode;
  /** Merged over the card recipe and the spacing block, last value winning. */
  readonly className?: string;
  /**
   * App-owned test id for the card. The inner slots derive theirs from it
   * (`-icon`, `-message`, `-description`, `-action`), so an app names the block
   * once and its E2E selectors follow.
   */
  readonly "data-testid"?: string;
};

/** A slot's derived test id, or nothing at all when the root carries none. */
function derive(testId: string | undefined, part: string): string | undefined {
  return testId === undefined ? undefined : `${testId}-${part}`;
}

/**
 * Renders an empty-state card with an h2 message and optional decorative icon, description,
 * and action. Omitted optional sections render no wrapper.
 */
export function EmptyState({
  icon,
  message,
  description,
  action,
  className,
  "data-testid": testId,
  ...rest
}: EmptyStateProps): ReactElement {
  return (
    <Card {...rest} spacing={emptyStateRecipe()} className={className} data-testid={testId}>
      {icon === undefined ? null : (
        <div
          aria-hidden="true"
          className={emptyStateIconRecipe()}
          data-testid={derive(testId, "icon")}
        >
          {icon}
        </div>
      )}
      <Text as="h2" variant="subheading" data-testid={derive(testId, "message")}>
        {message}
      </Text>
      {description === undefined ? null : (
        <Text as="p" variant="body" color="muted" data-testid={derive(testId, "description")}>
          {description}
        </Text>
      )}
      {action === undefined ? null : (
        <div className={emptyStateActionRecipe()} data-testid={derive(testId, "action")}>
          {action}
        </div>
      )}
    </Card>
  );
}
