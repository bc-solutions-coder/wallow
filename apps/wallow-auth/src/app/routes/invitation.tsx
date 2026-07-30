import { useCurrentUser } from "@bc-solutions-coder/auth";
import { createFileRoute, useRouteContext } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { InvitationLoading, InvitationScreen } from "@features/invitation";

/**
 * The `/invitation` route (Wallow-vec7.3.9).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract — `/invitation`, singular — so this task replaced the placeholder
 * component here and left `src/router.tsx` untouched.
 *
 * This route owns the two inputs the screen is a pure function of: the query
 * string (the oracle's single `[SupplyParameterFromQuery] Token`) and the auth
 * state.
 *
 * ── THE AUTH-STATE PROBE ─────────────────────────────────────────────────────
 *
 * This app has no server session and the auth cookie is HttpOnly, so the answer
 * comes from `useCurrentUser` — the workspace's ONE definition of "who is signed
 * in" (`@bc-solutions-coder/auth`), a same-origin `GET /v1/identity/users/me`
 * through the SDK passthrough proxy whose 200-vs-401 IS the answer. JS never
 * reads the cookie; it observes only the status of its own request.
 *
 * CLIENT-SIDE, not in an SSR loader: the browser attaches the cookie to a
 * same-origin request automatically, whereas the SDK client is `baseUrl: "/"`
 * with no server-side cookie plumbing — and auth state must not be SSR-cached.
 * The subscribing hook, not the `beforeLoad` primer half of that contract: this
 * route must RENDER for an anonymous visitor — an invitation link is reached by
 * people who have no account yet — rather than resolve a user before it renders.
 *
 * A REJECTION IS ANONYMOUS, mirroring the oracle's `catch { _isAuthenticated =
 * false; }` (:133-136). The canonical current-user query owns the 401-softening
 * (that status resolves `null` instead of throwing), so a rejection here means
 * the probe itself failed — and the less-privileged branch is the safe read: it
 * offers a sign-in link, while the other offers an accept button whose POST
 * would 401. The server enforces the real thing regardless (`{token}/accept` is
 * `[Authorize]`d); this is a UI affordance only. A failed probe is not retried,
 * which is the shared query client's own default: grinding on it would hold the
 * invitation behind a spinner when a failure is already an answer.
 *
 * The screen mounts only once the probe settles. The alternative — mount with
 * `isAuthenticated: false` and flip on arrival — would flash "Create account" at
 * a signed-in user and invite them to register a second account. The wait is
 * spent behind the same `invitation-loading` state the verify would show anyway.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside, with
 * no `branding` prop, so it falls back to the fork's own — the per-client
 * (`client_id`) branding overlay is not wired on this screen, and no acceptance
 * criterion asks for it (this oracle declares no `client_id` parameter at all).
 */
interface InvitationSearch {
  /** The invitation link's `token` — `undefined` when the link omits it. */
  readonly token?: string;
}

/**
 * The token is OPTIONAL, deliberately: a bare `/invitation` must render the
 * oracle's "No invitation token provided." error rather than throw a
 * search-validation error at a visitor who followed a mangled link.
 *
 * Anything non-string is treated as absent for the same reason, and it is not
 * hypothetical: TanStack JSON-parses scalar search values, so `?token=true`
 * arrives as the BOOLEAN `true`. Handing that to `verifyInvitation(token: string)`
 * would interpolate it into a URL path segment.
 */
function validateSearch(search: Record<string, unknown>): InvitationSearch {
  return { token: typeof search.token === "string" ? search.token : undefined };
}

function InvitationRoute() {
  const { token } = Route.useSearch();
  const { sdk } = useRouteContext({ from: "__root__" });

  // The request-scoped client off the router context — never a module-global
  // one, which would hand one request's session to the next under SSR.
  const authQuery = useCurrentUser(sdk.client);

  if (authQuery.isPending) {
    return (
      <AuthLayout>
        <InvitationLoading />
      </AuthLayout>
    );
  }

  return (
    <AuthLayout>
      <InvitationScreen
        token={token}
        isAuthenticated={authQuery.data !== null && authQuery.data !== undefined}
      />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/invitation")({
  validateSearch,
  component: InvitationRoute,
});
