import type { HTMLAttributes, ReactElement, ReactNode } from "react";

import { cn } from "../../core/cn";
import { Text } from "../text/text";
import {
  pageHeaderActionsRecipe,
  pageHeaderRecipe,
  pageHeaderTitleGroupRecipe,
} from "./page-header.styles";

/**
 * The page-level heading block every dashboard and detail page opens with: the
 * page title, an optional description under it, and an optional actions slot at
 * the trailing edge for the page's primary actions.
 *
 * `title` is a ReactNode, so `HTMLAttributes`' string `title` attribute is
 * omitted from the passthrough — a page title is content, not a tooltip.
 */
export type PageHeaderProps = Omit<HTMLAttributes<HTMLDivElement>, "title"> & {
  /** The page title. Rendered through `Text` as the page's `<h1>`. */
  readonly title: ReactNode;
  /** Optional supporting copy rendered under the title. */
  readonly description?: ReactNode;
  /** Optional page actions rendered at the header's trailing edge. */
  readonly actions?: ReactNode;
  /** Merged over the row recipe, last value winning. */
  readonly className?: string;
  /**
   * App-owned test id for the block. The inner parts derive theirs from it
   * (`-title`, `-description`, `-actions`), so an app names the header once.
   */
  readonly "data-testid"?: string;
};

/** A part's derived test id, or nothing at all when the root carries none. */
function derive(testId: string | undefined, part: string): string | undefined {
  return testId === undefined ? undefined : `${testId}-${part}`;
}

/**
 * The heading row both wallow-web list routes hand-roll today. The title and
 * the description go through `Text` rather than raw tags, so the type scale and
 * the semantic colour stay one decision made in one place — `variant="title"`
 * because `as="h1"` alone would derive the larger `display` scale.
 */
export function PageHeader({
  title,
  description,
  actions,
  className,
  "data-testid": testId,
  ...rest
}: PageHeaderProps): ReactElement {
  return (
    <div {...rest} className={cn(pageHeaderRecipe(), className)} data-testid={testId}>
      <div className={pageHeaderTitleGroupRecipe()}>
        <Text as="h1" variant="title" data-testid={derive(testId, "title")}>
          {title}
        </Text>
        {description === undefined ? null : (
          <Text as="p" variant="bodySm" color="muted" data-testid={derive(testId, "description")}>
            {description}
          </Text>
        )}
      </div>
      {actions === undefined ? null : (
        <div className={pageHeaderActionsRecipe()} data-testid={derive(testId, "actions")}>
          {actions}
        </div>
      )}
    </div>
  );
}
