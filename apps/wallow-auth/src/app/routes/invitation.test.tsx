import { type CurrentUser, currentUserQuery } from "@bc-solutions-coder/auth";
import type { QueryClient } from "@bc-solutions-coder/query";
import {
  createTestQueryClient,
  renderWithWallow,
} from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkCall, SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "../test/harness";
import { Route as invitationRoute } from "./invitation";

/**
 * Route spec for `/invitation`'s AUTH-STATE PROBE (Wallow-x4qn.9.2).
 *
 * The route used to hand-roll that probe: the generated `users/me` key with its
 * own `queryFn` over the SDK's `getCurrentUser` and a local `retry: false`. This
 * task deletes it in favour of `useCurrentUser` from `@bc-solutions-coder/auth`,
 * the workspace's ONE definition of "who is signed in" — and a deletion of an
 * auth read is exactly the change that compiles cleanly while breaking the
 * screen. So this file pins the SURVIVING probe from both directions:
 *
 *  1. what must NOT change — the route still asks the API who the visitor is,
 *     still treats a 401 (and a failure) as anonymous rather than as an error,
 *     still refuses to redirect a signed-out visitor away from an invitation
 *     link, and still holds the screen back until the answer arrives;
 *  2. what the shared query ADDS — the resolved user lands in the cache with the
 *     `sub` the SDK's claim helpers read, and a user another route already
 *     primed is REUSED instead of re-fetched (the 30-second `staleTime` the
 *     inline probe never had).
 *
 * (2) is what a wrong deletion cannot fake: a hand-rolled copy resolves the same
 * cache key, so only the stored shape and the re-fetch behaviour tell the two
 * apart. `src/shared-current-user.test.ts` pins the same deletion structurally.
 *
 * TEST SEAM: `@bc-solutions-coder/testing/sdk-harness`. Nothing here mocks the
 * SDK or the auth package — the harness builds the REAL SDK over a recording
 * fake `fetch`, so the whole pipeline the app ships (request-scoped SDK ->
 * generated operation -> `getCurrentUser`'s 401 softening -> React Query) runs
 * for real and the assertions read the outgoing REQUEST. `renderWithWallow`
 * supplies the router context the route reads its SDK off, and
 * `createAuthHarness()` pins the harness origin to this app's root-mounted API
 * surface — which is why every path below is bare, with no `/api` prefix.
 *
 * The four wire branches of the probe (200 / 401 / 500 / in flight) are asserted
 * through the SCREEN's testids because that is the only thing a visitor sees of
 * the answer: `invitation-accept` for a signed-in one, `invitation-sign-in` for
 * an anonymous one. Those two branches are also exercised from
 * `features/invitation/components/InvitationScreen.test.tsx`; they are restated
 * here deliberately, as the regression pins for THIS task's deletion.
 */

const TOKEN = "inv-tok-9f2";
const EMAIL = "invitee@example.com";
const USER_ID = "8f1d4c9e-0000-4000-8000-0000000000a1";

/** Wire paths, read off `packages/sdk/src/generated/sdk.gen.ts`. */
const CURRENT_USER_PATH = "/v1/identity/users/me";
const VERIFY_PREFIX = "/v1/identity/invitations/verify/";

const UNAUTHORIZED = 401;
const SERVER_ERROR = 500;

let harness: SdkHarness;

/** Per-test wire answers, held as functions so a test can defer or vary one. */
let currentUserAnswer: () => Response | Promise<Response>;
let verifyAnswer: () => Response | Promise<Response>;

/** A `CurrentUserResponse`, as `UsersController.GetCurrentUser` shapes it. */
function currentUserBody(): Record<string, unknown> {
  return {
    id: USER_ID,
    email: EMAIL,
    firstName: "Ada",
    lastName: "Lovelace",
    roles: ["User"],
    permissions: [],
  };
}

/** An `InvitationResponse`, pending and unexpired — not this file's subject. */
function invitationBody(): Record<string, unknown> {
  return {
    id: "8f1d4c9e-0000-4000-8000-0000000000b2",
    email: EMAIL,
    status: "Pending",
    expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
    createdAt: new Date(Date.now() - 86_400_000).toISOString(),
    acceptedByUserId: null,
  };
}

/** The wire form of both endpoints' only failure: a bare status, no body. */
function failure(status: number): Response {
  return new Response(null, { status });
}

/** Every recorded request that asked the API who the visitor is. */
function probeCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === CURRENT_USER_PATH);
}

/** Every recorded request that verified the invitation token. */
function verifyCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path.startsWith(VERIFY_PREFIX));
}

/**
 * Mount the real route through a memory router, optionally on a cache a test has
 * primed BEFORE the route mounts — which stands in for the cache a real
 * navigation arrives with: `router.tsx`'s `createQueryClient()` lives for the
 * whole browser session, so a visitor who has already been through any other
 * current-user read reaches this route with the answer in hand.
 */
function renderRouteAt(url: string, queryClient?: QueryClient) {
  return renderWithWallow(null, {
    harness,
    queryClient,
    path: url,
    routes: [{ path: "/invitation", route: invitationRoute }],
  });
}

beforeEach(() => {
  currentUserAnswer = () => Response.json(currentUserBody());
  verifyAnswer = () => Response.json(invitationBody());

  harness = createAuthHarness();
  harness.respond((call: SdkCall) => {
    if (call.path === CURRENT_USER_PATH) {
      return currentUserAnswer();
    }
    if (call.path.startsWith(VERIFY_PREFIX)) {
      return verifyAnswer();
    }

    throw new Error(`unexpected SDK request: ${call.method} ${call.path}`);
  });
});

describe("/invitation asks the API who the visitor is", () => {
  it("probes users/me once, through the request-scoped client", async () => {
    renderRouteAt(`/invitation?token=${TOKEN}`);

    await vi.waitFor(() => {
      expect(probeCalls()).toHaveLength(1);
    });
    expect(probeCalls()[0]?.method).toBe("GET");
  });

  it("shows the accept button to a visitor the API resolves", async () => {
    // The auth cookie is HttpOnly, so a resolved user IS the session as far as
    // the browser can tell.
    renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-accept")).toBeInTheDocument();
    expect(page.getByTestId("invitation-sign-in").query()).toBeNull();
  });

  it("holds the screen back until the probe settles", async () => {
    // Mounting with `isAuthenticated: false` and flipping on arrival would flash
    // "Create account" at a signed-in user. Asserted at the WIRE rather than on
    // `invitation-loading` (which the screen's own pending state also renders):
    // the screen has not mounted while no verify request exists.
    currentUserAnswer = () => new Promise<Response>(() => {});

    renderRouteAt(`/invitation?token=${TOKEN}`);

    await vi.waitFor(() => {
      expect(probeCalls()).toHaveLength(1);
    });
    await expect.element(page.getByTestId("invitation-loading")).toBeInTheDocument();
    expect(verifyCalls()).toEqual([]);
    expect(page.getByTestId("invitation-accept").query()).toBeNull();
  });
});

describe("/invitation treats an unresolved visitor as anonymous, never as an error", () => {
  it("offers a sign-in link on the API's 401", async () => {
    // A 401 is the ANSWER "anonymous": `getCurrentUser` resolves `null` without
    // throwing, and the canonical query preserves that. Nothing about this route
    // may become a gate — an invitation link is reached by people with no account.
    currentUserAnswer = () => failure(UNAUTHORIZED);

    const { router } = renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-sign-in")).toBeInTheDocument();
    expect(page.getByTestId("invitation-accept").query()).toBeNull();
    // And the visitor is still ON the invitation, not bounced to a login route.
    expect(router.state.location.pathname).toBe("/invitation");
  });

  it("falls back to anonymous when the probe itself fails", async () => {
    // A 500 from `users/me` is not evidence of a session, and the less-privileged
    // branch is the safe read: it offers a sign-in link, where the other offers an
    // accept button whose `[Authorize]`d POST would 401. The invitation itself
    // still verifies — the probe is an affordance, not a gate on the content.
    currentUserAnswer = () => failure(SERVER_ERROR);

    renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-sign-in")).toBeInTheDocument();
    expect(page.getByTestId("invitation-accept").query()).toBeNull();
    await expect.element(page.getByTestId("invitation-info")).toBeInTheDocument();
  });

  it("does not retry a failed probe", async () => {
    // The client's own default (`createQueryClient()`), which is why the route
    // needs no local override: retrying would hold the invitation behind a
    // spinner while a failure is already an answer.
    currentUserAnswer = () => failure(SERVER_ERROR);

    renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-sign-in")).toBeInTheDocument();
    expect(probeCalls()).toHaveLength(1);
  });
});

describe("/invitation reads the visitor through the shared current-user query", () => {
  it("caches the resolved user with the sub the SDK's claim helpers read", async () => {
    // `packages/auth`'s query renames the API's `id` to `sub` so the stored user
    // satisfies `WallowUser` and the shared `requireAuth`/`isAdmin` guards can
    // read it. The hand-rolled probe stored the raw response under the SAME key,
    // so a guard reading this entry saw a user with no `sub` — which is the
    // silent half of the duplication this task deletes.
    const { queryClient } = renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-accept")).toBeInTheDocument();

    const cached: CurrentUser | null | undefined = queryClient.getQueryData<CurrentUser | null>(
      currentUserQuery(harness.sdk.client).queryKey,
    );

    expect(cached?.id).toBe(USER_ID);
    expect(cached?.sub).toBe(USER_ID);
  });

  it("reuses a user the shared query already primed instead of re-probing", async () => {
    // The canonical query's 30-second `staleTime`, observed: primed data is FRESH,
    // so mounting this route reads the cache. The inline probe declared no
    // `staleTime` at all, which made every mount of an invitation link re-ask
    // `users/me` — and left this route disagreeing with every other current-user
    // read about how often to ask.
    const queryClient: QueryClient = createTestQueryClient();
    await queryClient.fetchQuery(currentUserQuery(harness.sdk.client));

    expect(probeCalls()).toHaveLength(1);

    renderRouteAt(`/invitation?token=${TOKEN}`, queryClient);

    // The screen mounts only once the probe has an answer, so a verify request
    // proves the route's own subscription has already run its mount effects —
    // i.e. a re-fetch, had the route asked for one, would be recorded by now.
    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });
    await expect.element(page.getByTestId("invitation-accept")).toBeInTheDocument();
    expect(probeCalls()).toHaveLength(1);
  });
});
