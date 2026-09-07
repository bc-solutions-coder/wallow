import { useSyncExternalStore } from "react";

/**
 * Observe the 48rem desktop breakpoint with matchMedia. The server and first hydration render
 * have no observed viewport and return undefined; AppShell uses responsive CSS during that
 * interval.
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
