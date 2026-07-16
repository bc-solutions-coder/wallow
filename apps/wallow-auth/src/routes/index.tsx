import { createFileRoute, redirect } from "@tanstack/react-router";

/**
 * The auth app's index route (Wallow-vec7.1.4). Unlike wallow-web's public home
 * page, `/` here immediately redirects to `/login`: the auth frontend has no
 * landing page of its own, it exists to serve the login/register/reset flows.
 *
 * The redirect is issued from `beforeLoad` (not a component-level `Navigate`) so
 * the SSR request handler emits a real HTTP redirect Response — `curl /` returns
 * a 3xx with `Location: /login` rather than any rendered markup. `/login` is not
 * a registered route yet in Phase 0, so `href` (a raw location) is used instead
 * of `to` (which resolves against the typed route tree); a blank page at the
 * redirect target is acceptable at this stage per the acceptance criteria.
 */
export const Route = createFileRoute("/")({
  beforeLoad: () => {
    throw redirect({ href: "/login" });
  },
});
