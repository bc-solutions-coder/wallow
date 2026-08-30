import { DEFAULT_AUTH_URL, readInjectedAuthUrl } from "@bc-solutions-coder/env/auth-origin";

/**
 * The sign-in app's public origin, as any component anywhere in the tree sees
 * it — what "Preview sign-in" links point at.
 *
 * Resolved from `WALLOW_AUTH_URL` in `src/app/start.ts`, the one place a
 * `process.env` read is legal; the shell states the server's answer in the
 * document as an inline script and this reads it back, so the SSR render and
 * the hydrating render produce the same href — a differing href across that
 * boundary is a hydration mismatch. Falling back to the local dev default
 * keeps the seam free for a caller with no document behind it: a component
 * spec mounting a screen on its own, or Storybook.
 *
 * A plain function, not a hook and not a context, for the same reason
 * `forkLinks()` is: the value is fixed for the life of the document, so there
 * is nothing to subscribe to and nothing to re-render.
 */
export function authUrl(): string {
  return readInjectedAuthUrl(globalThis) ?? DEFAULT_AUTH_URL;
}
