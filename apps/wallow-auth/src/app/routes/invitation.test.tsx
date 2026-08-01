import { type CurrentUser, currentUserQuery } from "@bc-solutions-coder/auth";
import type { QueryClient } from "@bc-solutions-coder/query";
import {
  createTestQueryClient,
  renderWithWallow,
} from "@bc-solutions-coder/testing/render-with-wallow";
import {
  createPassthroughHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as invitationRoute } from "./invitation";

/**
 * `/invitation`'s auth-state probe: what the route asks the API about the
 * visitor, and what the screen offers for each answer.
 *
 * Runs the real SDK over a faked fetch (sdk-harness), so assertions read the
 * recorded request, not a spy. `renderWithWallow` supplies the router context
 * the route reads its SDK off, and `createPassthroughHarness()` pins the harness origin
 * to this app's root-mounted API surface — which is why every path below is
 * bare, with no `/api` prefix.
 */

const TOKEN = "inv-tok-9f2";
const EMAIL = "invitee@example.com";
const USER_ID = "8f1d4c9e-0000-4000-8000-0000000000a1";

/** Wire paths, as the generated SDK spells them. */
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

/** An `InvitationResponse`, pending and unexpired. */
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
 * Mount the real route through a memory router, optionally on a cache primed
 * before it mounts — which stands in for the cache a real navigation arrives
 * with: the router's `createQueryClient()` lives for the whole browser session.
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

  harness = createPassthroughHarness();
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
    // "Create account" at a signed-in user. The absence of a verify request is
    // the load-bearing half: the screen has not mounted at all yet.
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
    // read it. A probe that stored the raw response under this same key would
    // leave those guards seeing a user with no `sub`.
    const { queryClient } = renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-accept")).toBeInTheDocument();

    const cached: CurrentUser | null | undefined = queryClient.getQueryData<CurrentUser | null>(
      currentUserQuery(harness.sdk.client).queryKey,
    );

    expect(cached?.id).toBe(USER_ID);
    expect(cached?.sub).toBe(USER_ID);
  });

  it("reuses a user the shared query already primed instead of re-probing", async () => {
    // The canonical query's 30-second `staleTime`, observed: primed data is
    // FRESH, so mounting this route reads the cache rather than re-asking
    // `users/me` on every arrival at an invitation link.
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
