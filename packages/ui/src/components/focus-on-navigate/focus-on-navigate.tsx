/** FocusOnNavigate follows the router's resolved pathname so focus moves after the destination content commits. */

import { useRouterState } from "@tanstack/react-router";
import { useEffect, useRef } from "react";

/**
 * Selector FocusOnNavigate uses to find the destination page's heading.
 */
export const MAIN_HEADING_SELECTOR = "h1";

/**
 * Moves focus to the first h1 after a TanStack Router pathname change resolves. Skips initial
 * load and query-only changes, adds tabIndex=-1 when needed, and prevents scrolling. Render
 * inside the router; this component renders nothing.
 */
export function FocusOnNavigate(): null {
  const resolvedPathname: string | undefined = useRouterState({
    select: (state) => state.resolvedLocation?.pathname,
  });
  const previousPathname = useRef<string | null>(null);

  useEffect(() => {
    // Nothing has resolved yet — wait for the first committed location.
    if (resolvedPathname === undefined) {
      return;
    }

    // Record the initial resolved location without stealing focus from the
    // browser's real page-load placement.
    if (previousPathname.current === null) {
      previousPathname.current = resolvedPathname;
      return;
    }

    if (previousPathname.current === resolvedPathname) {
      return;
    }
    previousPathname.current = resolvedPathname;

    const heading: HTMLElement | null = document.querySelector<HTMLElement>(MAIN_HEADING_SELECTOR);
    if (heading === null) {
      return;
    }

    if (!heading.hasAttribute("tabindex")) {
      heading.setAttribute("tabindex", "-1");
    }
    heading.focus({ preventScroll: true });
  }, [resolvedPathname]);

  return null;
}
