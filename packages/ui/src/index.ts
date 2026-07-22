import { createElement, type ReactElement } from "react";

/**
 * Placeholder barrel for @bc-solutions-coder/ui.
 *
 * The real primitives (Button/Input/Label/Card/ErrorBanner) and the moved
 * ready-indicator/focus-on-navigate components land in the follow-up Wallow-0q2s.6.x
 * tasks. This placeholder exists only so the package builds and typechecks with a
 * concrete export while the scaffold is wired up.
 */
export function Placeholder(): ReactElement {
  return createElement("div", { "data-ui-placeholder": "true" });
}
