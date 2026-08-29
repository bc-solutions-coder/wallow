import { getGlobalStartContext } from "@tanstack/react-start";

import { webAppUrl } from "./web-app-url";

/**
 * The request side of `web-app-url.ts`: the URL THIS request's middleware
 * resolved, off Start's global context, falling back to what the document
 * already published. On the server `getGlobalStartContext()` THROWS with no
 * Start context surrounding the call — a spec rendering a route directly — and
 * that is not an error here; in the browser there is no request at all, and the
 * fallback is the value the shell's inline script published.
 *
 * Its own module so the screens stay free of `@tanstack/react-start` (and its
 * `node:async_hooks` request storage); only route files import this.
 */
export function requestWebAppUrl(): string | undefined {
  try {
    const fromRequest: string | undefined = getGlobalStartContext()?.webAppUrl;
    if (fromRequest !== undefined) {
      return fromRequest;
    }
  } catch {
    // No Start context in scope: fall through to the published value.
  }

  return webAppUrl();
}
