import { useSyncExternalStore } from "react";

/**
 * `useIsDesktop` (Wallow-0byr.2) — whether the viewport is at or above Tailwind's
 * `md` breakpoint, subscribed at RUNTIME rather than expressed as a `md:` class.
 *
 * The dashboard nav does not merely restyle across the breakpoint, it renders
 * different things: at desktop widths a persistent rail, below them no rail at
 * all and an overlay drawer instead. A `hidden md:flex` cannot express that —
 * the rail would still be in the DOM (and in the tab order, and in the
 * accessibility tree) on a phone. Reading the media query as state lets the
 * components mount only what that width actually has.
 *
 * `48rem` is Tailwind v4's default `md`; `packages/styles` overrides no
 * breakpoints, so this string and the `md:` prefix stay the same boundary.
 *
 * SSR: there is no viewport on the server, so the server snapshot claims desktop
 * — the dashboard's primary form factor. `useSyncExternalStore` re-reads the
 * real query right after hydration, so a phone corrects itself in the first
 * client render instead of mismatching.
 */
const DESKTOP_QUERY = "(min-width: 48rem)";

function subscribe(onStoreChange: () => void): () => void {
  const media: MediaQueryList = globalThis.matchMedia(DESKTOP_QUERY);
  media.addEventListener("change", onStoreChange);
  return () => {
    media.removeEventListener("change", onStoreChange);
  };
}

function getSnapshot(): boolean {
  return globalThis.matchMedia(DESKTOP_QUERY).matches;
}

function getServerSnapshot(): boolean {
  return true;
}

/** True while the viewport is at or above the `md` breakpoint. */
export function useIsDesktop(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
