import {
  forkLinks as buildTimeForkLinks,
  type ForkLinks,
  readInjectedForkLinks,
} from "@bc-solutions-coder/styles";

/**
 * This deployment's outbound fork links (repository, docs), as any component
 * anywhere in the tree sees them.
 *
 * They are resolved from the ENVIRONMENT — `WALLOW_REPOSITORY_URL` /
 * `WALLOW_DOCS_URL`, read in `src/app/start.ts`, the one place a `process.env`
 * read is legal — so one image can be run against a staging docs site and a
 * production one without a rebuild. This module is the reading end of that: the
 * shell states the server's answer in the document as an inline script, and this
 * reads it back, so the SSR render and the hydrating render produce the same
 * href. A differing href across that boundary is a hydration mismatch.
 *
 * Falling back to the fork's build-time pair from `branding.json` is what keeps
 * the seam free for a caller with no document behind it: a component spec
 * mounting a screen on its own, or Storybook.
 *
 * It is a plain function, not a hook and not a context: the value is fixed for
 * the life of the document, so there is nothing to subscribe to and nothing to
 * re-render. It also imports nothing from `@tanstack/react-start` — the request
 * side of the resolution lives in `app/routes/__root.tsx`, which is what keeps
 * Start's `node:async_hooks` request storage out of every screen that renders a
 * fork link.
 */
export function forkLinks(): ForkLinks {
  return readInjectedForkLinks(globalThis) ?? buildTimeForkLinks;
}
