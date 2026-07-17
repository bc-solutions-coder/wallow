/**
 * Route-change focus management for wallow-auth (Wallow-ffpq.2.8) — the React
 * port of Blazor's `<FocusOnNavigate RouteData="..." Selector="h1" />` from
 * `api/src/Wallow.Auth/Components/Routes.razor`.
 *
 * The Blazor original moved keyboard/screen-reader focus to the page's `<h1>`
 * after each client-side navigation, so assistive tech announces the new screen.
 * The React port lost that; this component restores it and, like
 * `ReadyIndicator`, sits at the root so *every* auth page gets the behaviour.
 */

import { useRouterState } from "@tanstack/react-router";
import { useEffect, useRef } from "react";

/**
 * The CSS selector for the page's main heading, mirroring Blazor's
 * `Selector="h1"`. `auth-layout.tsx` renders exactly one `<h1>` per screen.
 */
export const MAIN_HEADING_SELECTOR = "h1";

/**
 * Moves DOM focus to the destination screen's main heading after each
 * client-side navigation. Renders nothing.
 *
 * It keys off the router's *resolved* location, not the pending one: on
 * navigation the root re-renders with the new `location` while the `<Outlet/>`
 * still shows the old route, and only once the destination route has committed
 * does `resolvedLocation` advance. Focusing on the resolved pathname is what
 * guarantees the new screen's `<h1>` is already in the DOM.
 *
 * The first resolved location (the initial page load) is recorded without
 * moving focus: the browser has already placed focus for a real load, and
 * stealing it to the heading on first paint is a documented a11y anti-pattern.
 * A bare `<h1>` is not focusable, so the heading is given `tabindex="-1"` before
 * `.focus()` — which also keeps it out of the Tab order for sighted keyboard
 * users. This is the standard SPA route-change focus pattern.
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
