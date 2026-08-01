import { useSyncExternalStore } from "react";

/**
 * `useIsDesktop` — whether the viewport is at or above Tailwind's `md`
 * breakpoint, subscribed at RUNTIME rather than expressed as a `md:` class.
 *
 * The shell's nav does not merely restyle across the breakpoint, it renders
 * different things: at desktop widths a persistent rail, below them no rail at
 * all and an overlay drawer instead. A `hidden md:flex` cannot express that —
 * the rail would still be in the DOM (and in the tab order, and in the
 * accessibility tree) on a phone. Reading the media query as state lets the
 * components mount only what that width actually has.
 *
 * `48rem` is Tailwind v4's default `md`; `packages/styles` overrides no
 * breakpoints, so this string and the `md:` prefix stay the same boundary.
 *
 * SSR: there is no viewport on the server, so the server snapshot answers
 * `undefined` — "no viewport has been observed yet". It used to claim desktop,
 * which made every phone paint the desktop rail and then have it yanked away a
 * frame later; claiming mobile instead would only move that flash onto laptops.
 * Neither guess can be right, so the hook does not guess.
 *
 * `useSyncExternalStore` consults `getServerSnapshot` during `renderToString`
 * AND for React's first hydration render, then re-reads the real query — so
 * `undefined` is exactly the window in which the client has not yet corrected
 * anything, and a consumer that sees it must render chrome that is honest at
 * either width (the shell hands that decision to a `md:` media query, which
 * resolves at first paint; JavaScript cannot). A fresh client mount never
 * observes `undefined`: it takes `getSnapshot` on its very first render.
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

function getServerSnapshot(): undefined {
  return undefined;
}

/**
 * `true` while the viewport is at or above the `md` breakpoint, `false` below
 * it, and `undefined` while no viewport has been observed — on the server and
 * for React's first hydration render.
 */
export function useIsDesktop(): boolean | undefined {
  return useSyncExternalStore<boolean | undefined>(subscribe, getSnapshot, getServerSnapshot);
}
