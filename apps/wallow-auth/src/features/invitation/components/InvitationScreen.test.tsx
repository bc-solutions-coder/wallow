import { isSafeReturnUrl } from "@bc-solutions-coder/sdk";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { type SdkCall, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { Route as invitationRoute } from "@app/routes/invitation";
import { InvitationScreen } from "./InvitationScreen";

/**
 * Component spec for the InvitationLanding screen (Wallow-vec7.3.9).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `invitation-loading`, `invitation-error`, `invitation-info`,
 * `invitation-expired`, `invitation-accept-error`, `invitation-accept`,
 * `invitation-decline`, `invitation-create-account`, `invitation-sign-in`.
 *
 * TEST SEAM: `@bc-solutions-coder/testing/sdk-harness` (Wallow-pu6a.5.1). Nothing
 * here mocks the SDK — not `@bc-solutions-coder/sdk`, not its `./query` entry,
 * and there is no app-level facade left to mock (Wallow-pu6a.5.5). The harness
 * builds the REAL SDK over a recording fake `fetch`, so the whole pipeline the
 * app ships — request-scoped SDK -> generated operation -> CSRF interceptor ->
 * serialization -> `WallowError` shaping -> React Query — executes here, and the
 * assertions below read the outgoing REQUEST instead of a spy on a stand-in.
 * `renderWithWallow` supplies the router context both the screen and the route
 * read their SDK off, and `createAuthHarness()` pins the harness origin to this
 * app's root-mounted API surface — which is why every recorded `path` below is
 * the bare endpoint path, with no `/api` prefix.
 *
 * ── THE AUTH-STATE SEAM (the gap this file's red phase left open) ────────────
 *
 * This screen depends on auth state that a server session would hold for free.
 * apps/wallow-auth
 * has no equivalent: its API surface is a PASSTHROUGH REVERSE PROXY with no
 * session store (`src/shared/lib/api-passthrough.server.ts`) and the auth cookie is HttpOnly.
 *
 * `isAuthenticated` is therefore a PROP, and the tests below pin both branches
 * against it. The ROUTE answers it with the generated
 * `usersGetCurrentUserQueryKey()` paired with the SDK's `getCurrentUser`
 * (Wallow-vec7.2.4, re-pointed at the generated surface in Wallow-pu6a.5.5): a
 * same-origin `GET /v1/identity/users/me` whose 200-vs-401 IS the answer —
 * `null` for anonymous, and it never throws on a 401. The route tests at the
 * bottom pin all three of its outcomes, stated as the wire statuses themselves.
 *
 * And the authenticated branch is a BUG FIX, not a port: `Wallow.Auth` registers
 * no authentication at all, so the oracle's `_isAuthenticated` is always false
 * and its accept/decline branch is dead code — while the API has always
 * supported it (`{token}/accept` is `[Authorize]`d, InvitationsController.cs:
 * 82-84).
 *
 * ── FOUR ORACLE BRANCHES COLLAPSE INTO REJECTIONS AT THIS SEAM ───────────────
 *
 * The oracle reads its API through `AuthApiClient`, which SWALLOWS non-2xx into
 * a sentinel — `VerifyInvitationAsync` returns `null` on any failure
 * (AuthApiClient.cs:297-312), `AcceptInvitationAsync` returns
 * `IsSuccessStatusCode` (AuthApiClient.cs:314-322) — so the oracle distinguishes
 * "the server said no" from "the call blew up" by sentinel-vs-`catch`, and gives
 * each its own copy:
 *
 *   verify:  null  -> "This invitation is not valid or has already been used."
 *            catch -> "Unable to verify this invitation. Please try again later."
 *   accept:  false -> "Unable to accept this invitation. It may have expired or
 *                      already been used."
 *            catch -> "An error occurred while accepting the invitation. Please
 *                      try again."
 *
 * The TS client THROWS on every non-2xx, so both of each pair arrive as one
 * rejected promise and the sentinel-vs-catch fork is gone. What survives is the
 * STATUS: `toWallowError` always populates `.status`, falling back to the raw
 * `Response.status` and then to 500 (packages/sdk/src/runtime-config.ts). That is
 * enough to keep all four messages, because the fork maps cleanly onto
 * 4xx-vs-not:
 *
 *   - `GET /v1/identity/invitations/verify/{token}` returns exactly ONE failure,
 *     `NotFound()` (InvitationsController.cs:71-80) — i.e. the oracle's `null`
 *     case IS the 404. Anything else (500, proxy/network) is the `catch` case.
 *   - `POST /v1/identity/invitations/{token}/accept` (InvitationsController.cs:
 *     82-91) throws `EntityNotFoundException` for an unknown/spent token and
 *     rejects an expired one from the aggregate — the "expired or already been
 *     used" copy verbatim — all 4xx. A 5xx is the `catch` case.
 *
 * Keyed on STATUS, not on `code`: unlike the `/v1/identity/auth/*` endpoints
 * (bd memory `mfa-endpoints-mfacontroller-return-business-failures-as-a`), these
 * two send no machine-readable code at all — `NotFound()` is a bare status with
 * no body, so `readCode()` finds nothing and every one of these rejections is
 * `code: "UNKNOWN"` (bd memory `wallow-auth-auth-client-ts-wallowerror-code-
 * loss`). A code-keyed mapping here would collapse all four messages into the
 * generic one. Verified by reading the controller, not assumed.
 *
 * ── THE INERT `email=` PARAMETER (an oracle wart, ported deliberately) ────────
 *
 * `GetRegisterUrl()` (InvitationLanding.razor:196-201) builds
 * `/register?email=…&returnUrl=…`, but `Register.razor` declares only
 * `client_id` and `returnUrl` as `[SupplyParameterFromQuery]` (Register.razor:
 * 179-183) — it never reads `email`. The `/register` route (Wallow-vec7.3.8)
 * likewise reads
 * only `client_id`/`returnUrl`. The `email` param is kept anyway — it is the link
 * contract, it costs nothing, and a Register that prefills the invited address
 * is a plausible follow-up — but it is pinned below as INERT so no one reads
 * these tests as proof the address prefills. It does not.
 *
 * ── WHY NO `isSafeReturnUrl` GUARD ON THIS SCREEN ────────────────────────────
 *
 * Every screen that ACCEPTS a returnUrl guards it (bd memory `returnurl-guard-
 * refuse-dont-sanitize`). This one accepts none: the oracle's only
 * `[SupplyParameterFromQuery]` is `Token`, and the two returnUrls here are BUILT
 * by the screen (`/invitation?token=…`), so they are safe by construction —
 * there is no open-redirect surface to guard. The attacker-controlled part is
 * the token, and the test below pins that it is percent-encoded INTO the
 * returnUrl (so a token like `x&returnUrl=//evil.example` cannot smuggle a
 * second parameter out) and that the result still satisfies the real
 * `isSafeReturnUrl` — imported from the SDK, not restated here.
 */

/**
 * THE READ SEAM (Wallow-evd5.3.1, re-expressed at the transport in
 * Wallow-pu6a.5.1, unified in Wallow-pu6a.5.5). The screen's invitation lookup is
 * `useQuery(invitationsVerifyOptions(...))` — the GENERATED factory — and the
 * ROUTE's signed-in probe is the generated `usersGetCurrentUserQueryKey()` paired
 * with the SDK's `getCurrentUser`, which softens the 401 to `null`. Accepting the
 * invitation calls the generated `invitationsAccept` operation. All three run for
 * real here; only the wire is faked.
 *
 * All three now share ONE transport, which is the change worth noting. The probe
 * used to be `getUser()` reaching for the GLOBAL `fetch` against `/bff/user`, so
 * this file carried a second stub to answer it. wallow-auth mounts no `/bff/**`
 * route at all (its server routes are the `/v1/**` and `/connect/**` passthrough
 * proxies), so that request had nowhere to land in the real app; the probe now
 * goes to `/v1/identity/users/me` through the same request-scoped SDK as
 * everything else, and the harness answers it alongside the rest.
 */

const TOKEN = "inv-tok-123";
const EMAIL = "invitee@example.com";
const HOME_HREF = "/";

/** The self-referential returnUrl both unauthenticated links carry back here. */
const SELF_RETURN_URL = `/invitation?token=${TOKEN}`;

/**
 * Wire paths, read off `packages/sdk/src/generated/sdk.gen.ts` rather than
 * guessed, and bare (no `/api` prefix) because wallow-auth's passthrough proxy
 * puts the client's baseUrl at `/` — see the TEST SEAM note above.
 */
const INVITATIONS_ROOT = "/v1/identity/invitations";
const VERIFY_PREFIX = `${INVITATIONS_ROOT}/verify/`;
const ACCEPT_SUFFIX = "/accept";
const ACCEPT_PATH = `${INVITATIONS_ROOT}/${TOKEN}${ACCEPT_SUFFIX}`;
/** The route's signed-in probe — `usersGetCurrentUser`, softened by `getCurrentUser`. */
const CURRENT_USER_PATH = "/v1/identity/users/me";

const UNAUTHORIZED = 401;
const NOT_FOUND = 404;
const SERVER_ERROR = 500;
const NO_CONTENT = 204;

/** An `InvitationResponse`, as `InvitationsController.MapToResponse` shapes it. */
function invitation(overrides: Record<string, unknown> = {}) {
  return {
    id: "8f1d4c9e-0000-4000-8000-000000000001",
    email: EMAIL,
    status: "Pending",
    expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
    createdAt: new Date(Date.now() - 86_400_000).toISOString(),
    acceptedByUserId: null,
    ...overrides,
  };
}

/**
 * The wire form of the only failure either endpoint sends: a BARE status with no
 * body (see "FOUR ORACLE BRANCHES" above). Driven through the real client this
 * becomes a `WallowError` carrying that status and `code: "UNKNOWN"` — the
 * code-less shape is not a convenience, it is what these two endpoints actually
 * produce, and answering with it stops an implementer from keying the copy on a
 * machine code that never arrives.
 */
function failure(status: number): Response {
  return new Response(null, { status });
}

/** A promise this test resolves/rejects by hand, to observe an in-flight state. */
function deferred<T>(): { promise: Promise<T>; resolve: (value: T) => void } {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((r) => {
    resolve = r;
  });
  return { promise, resolve };
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/**
 * `token` is read with `in` rather than `??`: an EXPLICIT `token: undefined` is
 * the tokenless link under test, and `props.token ?? TOKEN` would silently hand
 * it the default instead — testing the opposite of what the caller asked for.
 */
function renderScreen(props: { token?: string; isAuthenticated?: boolean } = {}) {
  return renderWithClient(
    <InvitationScreen
      isAuthenticated={props.isAuthenticated ?? false}
      token={"token" in props ? props.token : TOKEN}
    />,
  );
}

/**
 * THE NAVIGATION SEAM (vitest.config.ts NAVIGATION SEAM, real-browser variant).
 *
 * On a successful accept the screen hands off with a RAW
 * `globalThis.location.href = "/"` (the oracle's `NavigateTo("/", forceLoad:
 * true)`) — a full, non-`navigate()` reload, and NOT routed through a URL-builder
 * this test could assert instead. Under jsdom the old spec swapped `location`
 * wholesale with `vi.stubGlobal`; in real Chromium `window.location` is
 * `[Unforgeable]` and cannot be shadowed, and letting the assignment run
 * navigates the runner's iframe and tears the whole file down.
 *
 * So the hand-off is observed at the Navigation API instead: a same-origin
 * `location.href =` fires a cancelable `navigate` event whose `destination.url`
 * is exactly the string the screen assigned (resolved to an absolute URL).
 * Recording it and calling `preventDefault()` pins the destination for the one
 * test that asserts it AND keeps every other accept-success test from navigating
 * the iframe. The guard is installed once for the file and its log is reset per
 * test — equivalent to pinning the assigned `location.href`, since the
 * destination is deterministic.
 */
interface NavigateEvent extends Event {
  readonly destination: { readonly url: string };
  readonly cancelable: boolean;
}

const browserNavigation = globalThis as unknown as {
  navigation: {
    addEventListener: (type: "navigate", handler: (event: NavigateEvent) => void) => void;
  };
};

let recordedNavigations: string[] = [];

browserNavigation.navigation.addEventListener("navigate", (event) => {
  recordedNavigations.push(event.destination.url);
  if (event.cancelable) {
    event.preventDefault();
  }
});

/**
 * The `href` of one of the screen's links, PARSED — so the assertions below read
 * the query string the way a browser does (one decode, parameters by name)
 * rather than string-matching an encoded blob, which would pass on
 * `?returnUrl=/x&returnUrl=//evil.example` too. The base is a throwaway: these
 * hrefs are relative, and `URL` needs an origin to resolve one.
 */
async function linkUrl(testId: string): Promise<URL> {
  const link = page.getByTestId(testId);
  await expect.element(link).toBeInTheDocument();

  return new URL(link.element().getAttribute("href") ?? "", "https://auth.example");
}

let harness: SdkHarness;

/**
 * Per-test wire answers, dispatched by the ONE responder installed below. Held as
 * FUNCTIONS rather than values so a test can hand back a promise it settles by
 * hand (the in-flight cases) or vary its answer per call (the retry case).
 */
let verifyAnswer: () => Response | Promise<Response>;
let acceptAnswer: () => Response | Promise<Response>;
let currentUserAnswer: () => Response;

/** Every SDK request recorded so far whose path is a verify. */
function verifyCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path.startsWith(VERIFY_PREFIX));
}

/** Every SDK request recorded so far whose path is an accept. */
function acceptCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path.endsWith(ACCEPT_SUFFIX));
}

/**
 * The token the screen actually put on the wire, decoded.
 *
 * Read back off the request PATH rather than from a spy's call arguments, and
 * percent-decoded because the token is a path segment the client escapes on the
 * way out. Throws rather than returning a sentinel so `vi.waitFor` retries while
 * no request has been made yet, and so a missing call fails loudly.
 */
function verifiedToken(): string {
  const [call] = verifyCalls();
  if (call === undefined) {
    throw new Error("expected a verify request, saw none");
  }

  return decodeURIComponent(call.path.slice(VERIFY_PREFIX.length));
}

beforeEach(() => {
  recordedNavigations = [];

  verifyAnswer = () => Response.json(invitation());
  acceptAnswer = () => new Response(null, { status: NO_CONTENT });
  // The probe's anonymous answer: a 401, which `getUser()` maps to `null` WITHOUT
  // throwing (Wallow-vec7.2.4). Only the route probes; the component takes
  // `isAuthenticated` as a prop.
  currentUserAnswer = () => failure(UNAUTHORIZED);

  harness = createAuthHarness();
  harness.respond((call: SdkCall) => {
    if (call.path.startsWith(VERIFY_PREFIX)) {
      return verifyAnswer();
    }
    if (call.path.endsWith(ACCEPT_SUFFIX)) {
      return acceptAnswer();
    }
    if (call.path === CURRENT_USER_PATH) {
      return currentUserAnswer();
    }

    throw new Error(`unexpected SDK request: ${call.method} ${call.path}`);
  });
});

describe("InvitationScreen — missing token", () => {
  it("refuses a link with no token, without calling the API", async () => {
    renderScreen({ token: undefined });

    // The oracle's `IsNullOrWhiteSpace(Token)` guard (InvitationLanding.razor:
    // 118-124): its own message, and it returns BEFORE the verify call.
    await expect
      .element(page.getByTestId("invitation-error"))
      .toHaveTextContent(/no invitation token provided/iu);
    expect(verifyCalls()).toEqual([]);
  });

  it("treats a whitespace-only token as no token", async () => {
    renderScreen({ token: "   " });

    await expect
      .element(page.getByTestId("invitation-error"))
      .toHaveTextContent(/no invitation token provided/iu);
    expect(verifyCalls()).toEqual([]);
  });

  it("offers a way back to sign-in from the error state", async () => {
    renderScreen({ token: undefined });

    // InvitationLanding.razor:32-34 — the error branch is a dead end without it.
    await expect.element(page.getByTestId("invitation-error")).toBeInTheDocument();
    await expect
      .element(page.getByRole("link", { name: /back to sign in/iu }))
      .toHaveAttribute("href", "/login");
  });
});

describe("InvitationScreen — verifying", () => {
  it("verifies the token from the link", async () => {
    renderScreen({ token: TOKEN });

    await vi.waitFor(() => {
      expect(verifiedToken()).toBe(TOKEN);
    });
    expect(verifyCalls()[0]?.method).toBe("GET");
  });

  it("shows the loading state while the verify is in flight, then the invitation", async () => {
    const pending = deferred<Response>();
    verifyAnswer = () => pending.promise;

    renderScreen();

    // Anchored: the loading state must be REPLACED, not merely present — a
    // screen that renders the spinner forever would pass the first assertion.
    await expect.element(page.getByTestId("invitation-loading")).toBeInTheDocument();
    expect(page.getByTestId("invitation-info").query()).toBeNull();

    pending.resolve(Response.json(invitation()));

    await expect.element(page.getByTestId("invitation-info")).toBeInTheDocument();
    expect(page.getByTestId("invitation-loading").query()).toBeNull();
  });

  it("names the invited address once verified", async () => {
    renderScreen();

    // InvitationLanding.razor:41 — the address is the whole point of the info
    // block: it tells the user WHICH identity the invitation is for.
    await expect.element(page.getByTestId("invitation-info")).toHaveTextContent(EMAIL);
  });

  it("reports a 404 as an invalid or already-used invitation", async () => {
    verifyAnswer = () => failure(NOT_FOUND);

    renderScreen();

    await expect
      .element(page.getByTestId("invitation-error"))
      .toHaveTextContent(/not valid or has already been used/iu);
    expect(page.getByTestId("invitation-info").query()).toBeNull();
  });

  it("reports a server failure as a transient problem, not a bad invitation", async () => {
    verifyAnswer = () => failure(SERVER_ERROR);

    renderScreen();

    // The oracle's `catch` branch. Distinct copy from the 404: telling a user
    // their invitation is spent when the server merely fell over sends them to
    // an administrator for a replacement they do not need.
    await expect
      .element(page.getByTestId("invitation-error"))
      .toHaveTextContent(/unable to verify this invitation/iu);
  });
});

describe("InvitationScreen — expired invitation", () => {
  it("shows the expired notice when the server says the status is Expired", async () => {
    verifyAnswer = () => Response.json(invitation({ status: "Expired" }));

    renderScreen({ isAuthenticated: true });

    await expect.element(page.getByTestId("invitation-expired")).toHaveTextContent(/has expired/iu);
    await expect.element(page.getByTestId("invitation-info")).toBeInTheDocument();
  });

  it("shows the expired notice when expiresAt has passed, whatever the status says", async () => {
    // InvitationLanding.razor:147 is an OR: a `Pending` row whose `ExpiresAt` is
    // past is expired too. The status only flips to `Expired` when the
    // `CleanupExpiredAsync` sweep gets to it (InvitationService.cs:71-89), so
    // between expiry and the sweep this is the ONLY branch that catches it.
    const oneSecondAgo: string = new Date(Date.now() - 1_000).toISOString();
    verifyAnswer = () => Response.json(invitation({ status: "Pending", expiresAt: oneSecondAgo }));

    renderScreen({ isAuthenticated: true });

    await expect.element(page.getByTestId("invitation-expired")).toBeInTheDocument();
  });

  it("offers no way to accept an expired invitation, even when signed in", async () => {
    verifyAnswer = () => Response.json(invitation({ status: "Expired" }));

    renderScreen({ isAuthenticated: true });

    // Anchored on the expired notice: without it, `getByTestId(...).query()` to
    // be null would pass against a screen that rendered nothing at all.
    await expect.element(page.getByTestId("invitation-expired")).toBeInTheDocument();
    expect(page.getByTestId("invitation-accept").query()).toBeNull();
    expect(page.getByTestId("invitation-decline").query()).toBeNull();
  });

  it("offers no sign-in path for an expired invitation either", async () => {
    verifyAnswer = () => Response.json(invitation({ status: "Expired" }));

    renderScreen({ isAuthenticated: false });

    // The expiry check precedes the auth branch (InvitationLanding.razor:46-54):
    // signing in to accept a dead invitation is a wasted round trip.
    await expect.element(page.getByTestId("invitation-expired")).toBeInTheDocument();
    expect(page.getByTestId("invitation-create-account").query()).toBeNull();
    expect(page.getByTestId("invitation-sign-in").query()).toBeNull();
  });
});

describe("InvitationScreen — authenticated branch", () => {
  it("asks the signed-in user to accept or decline", async () => {
    renderScreen({ isAuthenticated: true });

    await expect.element(page.getByTestId("invitation-accept")).toBeInTheDocument();
    await expect.element(page.getByTestId("invitation-decline")).toBeInTheDocument();
    // The account links belong to the OTHER branch; both showing would offer a
    // signed-in user a "create account" they do not need.
    expect(page.getByTestId("invitation-create-account").query()).toBeNull();
    expect(page.getByTestId("invitation-sign-in").query()).toBeNull();
  });

  it("declining just leaves, without touching the invitation", async () => {
    renderScreen({ isAuthenticated: true });

    // InvitationLanding.razor:75-81 — `Href="/"`, no call. "No thanks" does NOT
    // revoke the invitation; it stays open for a later visit.
    await expect.element(page.getByTestId("invitation-decline")).toHaveAttribute("href", HOME_HREF);
    expect(acceptCalls()).toEqual([]);

    // And it is ANNOUNCED as the navigation it is. This is the catalog Button
    // composed onto an anchor, and Base UI stamps `role="button"` on every
    // non-native element it substitutes — which would put "No thanks" in a screen
    // reader's buttons list, alongside the accept control that really does POST.
    // The catalog supplies the link role itself (Wallow-lrlm.12); this call site
    // used to pass `role="link"` by hand with nothing asserting it.
    expect(page.getByRole("link", { name: /no thanks/iu }).query()).toBe(
      page.getByTestId("invitation-decline").element(),
    );
    expect(page.getByRole("button", { name: /no thanks/iu }).query()).toBeNull();
  });

  it("accepts the invitation with the link's token and lands the user home", async () => {
    const user = userEvent.setup();

    renderScreen({ isAuthenticated: true });
    await user.click(page.getByTestId("invitation-accept"));

    // The token is in the PATH — `POST /v1/identity/invitations/{token}/accept`
    // (sdk.gen.ts) — so the request itself carries the whole claim the old
    // `toHaveBeenCalledWith(TOKEN)` made, and one layer closer to the API.
    await vi.waitFor(() => {
      expect(acceptCalls()).toHaveLength(1);
    });
    expect(acceptCalls()[0]?.path).toBe(ACCEPT_PATH);
    expect(acceptCalls()[0]?.method).toBe("POST");

    // A FULL navigation, not `navigate()` — the oracle's
    // `NavigateTo("/", forceLoad: true)` (InvitationLanding.razor:179). The
    // reload is load-bearing: accepting the invitation changes the user's tenant
    // membership, and a client-side transition would carry the pre-acceptance
    // session state into the destination. Observed at the Navigation API seam
    // (see the guard above): the raw `location.href = "/"` resolves to home.
    await vi.waitFor(() => {
      expect(recordedNavigations.length).toBeGreaterThan(0);
    });
    const lastNavigation = recordedNavigations.at(-1);
    if (lastNavigation === undefined) {
      throw new Error("expected a recorded navigation");
    }
    const target: URL = new URL(lastNavigation);
    expect(target.pathname).toBe(HOME_HREF);
    expect(target.search).toBe("");
  });

  it("disables accept and makes decline inert while an accept is in flight", async () => {
    const user = userEvent.setup();
    const pending = deferred<Response>();
    acceptAnswer = () => pending.promise;

    renderScreen({ isAuthenticated: true });
    await user.click(page.getByTestId("invitation-accept"));

    // Wait for the POST to REACH the transport before asserting on the in-flight
    // state: the button goes disabled the moment the mutation starts, a tick or
    // two before `fetch` is called, and releasing into that gap would leave the
    // never-settling answer installed forever.
    await vi.waitFor(() => {
      expect(acceptCalls()).toHaveLength(1);
    });

    // The oracle's `_isSubmitting` guard (InvitationLanding.razor:164,169-171):
    // a second accept is a second POST against a one-shot token.
    await expect.element(page.getByTestId("invitation-accept")).toBeDisabled();

    // Decline is a LINK, and a link cannot carry `disabled`: `toBeDisabled` only
    // recognises form and custom elements, and `aria-disabled` alone still
    // navigates on click. Losing the `href` is what actually makes it inert — and
    // leaving mid-POST would hide the outcome of a request that is changing the
    // user's tenant membership.
    const decline = page.getByTestId("invitation-decline");
    await expect.element(decline).not.toHaveAttribute("href");
    await expect.element(decline).toHaveAttribute("aria-disabled", "true");

    pending.resolve(new Response(null, { status: NO_CONTENT }));
    // Let the success hand-off fire and be captured (and cancelled) by the
    // Navigation guard before the test unwinds, so the iframe never navigates.
    await vi.waitFor(() => {
      expect(recordedNavigations.length).toBeGreaterThan(0);
    });
  });

  it("reports a rejected accept as expired or already used, keeping the buttons alive", async () => {
    const user = userEvent.setup();
    acceptAnswer = () => failure(NOT_FOUND);

    renderScreen({ isAuthenticated: true });
    await user.click(page.getByTestId("invitation-accept"));

    await expect
      .element(page.getByTestId("invitation-accept-error"))
      .toHaveTextContent(/expired or already been used/iu);
    // `invitation-accept-error`, NOT `invitation-error`: the invitation verified
    // fine, so the info block and the buttons stay (InvitationLanding.razor:
    // 58-65 renders inside the authenticated branch, above the buttons).
    await expect.element(page.getByTestId("invitation-info")).toBeInTheDocument();
    await expect.element(page.getByTestId("invitation-accept")).toBeEnabled();
  });

  it("reports a server failure on accept as a retryable error", async () => {
    const user = userEvent.setup();
    acceptAnswer = () => failure(SERVER_ERROR);

    renderScreen({ isAuthenticated: true });
    await user.click(page.getByTestId("invitation-accept"));

    await expect
      .element(page.getByTestId("invitation-accept-error"))
      .toHaveTextContent(/an error occurred while accepting the invitation/iu);
  });

  it("clears a previous accept error when the user tries again", async () => {
    const user = userEvent.setup();
    // The FIRST accept fails, the retry succeeds. Keyed off the recorded calls
    // rather than a `mockRejectedValueOnce`, which the transport has no analogue
    // for — the harness records before it answers, so during the first POST
    // exactly one accept is on the log.
    acceptAnswer = () =>
      acceptCalls().length === 1
        ? failure(SERVER_ERROR)
        : new Response(null, { status: NO_CONTENT });

    renderScreen({ isAuthenticated: true });
    await user.click(page.getByTestId("invitation-accept"));
    await expect.element(page.getByTestId("invitation-accept-error")).toBeInTheDocument();

    // The oracle's `_acceptError = null` on re-entry (InvitationLanding.razor:
    // 170): a stale failure banner above a succeeded accept is a lie. The retry
    // succeeds, so its home hand-off is absorbed by the Navigation guard above.
    await user.click(page.getByTestId("invitation-accept"));

    await vi.waitFor(() => {
      expect(page.getByTestId("invitation-accept-error").query()).toBeNull();
    });
    expect(acceptCalls()).toHaveLength(2);
  });
});

describe("InvitationScreen — unauthenticated branch", () => {
  it("offers the anonymous visitor an account to create or a session to sign into", async () => {
    renderScreen({ isAuthenticated: false });

    await expect.element(page.getByTestId("invitation-create-account")).toBeInTheDocument();
    await expect.element(page.getByTestId("invitation-sign-in")).toBeInTheDocument();
    // Accepting needs a `[Authorize]`d POST (InvitationsController.cs:82-83);
    // offering it to an anonymous visitor buys them a 401.
    expect(page.getByTestId("invitation-accept").query()).toBeNull();

    // Both are the catalog Button composed onto an anchor, and both must ANNOUNCE
    // as links: Base UI stamps `role="button"` on every non-native element it
    // substitutes, which drops them from a screen reader's links list while their
    // hrefs still offer open-in-new-tab. The catalog supplies the link role itself
    // (Wallow-lrlm.12); both call sites used to pass `role="link"` by hand, and
    // the "back to sign in" assertion elsewhere in this file covers the error
    // state's PLAIN anchor, not either of these.
    expect(page.getByRole("link", { name: /create account/iu }).query()).toBe(
      page.getByTestId("invitation-create-account").element(),
    );
    expect(page.getByRole("link", { name: /sign in to accept/iu }).query()).toBe(
      page.getByTestId("invitation-sign-in").element(),
    );
    expect(page.getByRole("button", { name: /create account/iu }).query()).toBeNull();
    expect(page.getByRole("button", { name: /sign in to accept/iu }).query()).toBeNull();
  });

  it("sends the visitor to register with the invited address and a way back", async () => {
    renderScreen({ isAuthenticated: false });

    const url: URL = await linkUrl("invitation-create-account");

    expect(url.pathname).toBe("/register");
    // INERT — `/register` does not read `email` (see the header note). Pinned so
    // the oracle's link shape survives verbatim, not as a claim that it prefills.
    expect(url.searchParams.get("email")).toBe(EMAIL);
    expect(url.searchParams.get("returnUrl")).toBe(SELF_RETURN_URL);
  });

  it("sends the visitor to sign in with a way back", async () => {
    renderScreen({ isAuthenticated: false });

    const url: URL = await linkUrl("invitation-sign-in");

    expect(url.pathname).toBe("/login");
    // The round trip is the point: sign in, come back HERE, and the screen then
    // renders the authenticated branch with the accept button.
    expect(url.searchParams.get("returnUrl")).toBe(SELF_RETURN_URL);
  });

  it("encodes a hostile token into the returnUrl instead of letting it smuggle parameters", async () => {
    const hostileToken = "x&returnUrl=//evil.example";

    renderScreen({ isAuthenticated: false, token: hostileToken });

    const url: URL = await linkUrl("invitation-sign-in");

    // ONE returnUrl, and it is ours: the token is a VALUE inside it, not a
    // second parameter appended to the query string.
    expect(url.searchParams.getAll("returnUrl")).toHaveLength(1);
    expect(url.searchParams.get("returnUrl")).toBe(`/invitation?token=${hostileToken}`);
    // And what we built still satisfies the real guard — the SDK's own
    // `isSafeReturnUrl`, not a local restatement of it. This screen accepts no
    // returnUrl of its own, so safety is by construction, not by check.
    expect(isSafeReturnUrl(url.searchParams.get("returnUrl"))).toBe(true);
  });
});

/**
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criterion under test — "the token is
 * read from the query string" — only exists once a URL is parsed by a router.
 * The root here is a throwaway: the app's real `__root.tsx` renders `<html>`,
 * and `src/router.tsx` is off-limits to this task (Wallow-vec7.3.16).
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/invitation", route: invitationRoute }],
  });
}

describe("/invitation route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path is the contract — `/invitation`,
    // singular — and is not this task's to change.
    renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-info")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("verifies the token it read from the query string", async () => {
    renderRouteAt(`/invitation?token=${TOKEN}`);

    await vi.waitFor(() => {
      expect(verifiedToken()).toBe(TOKEN);
    });
  });

  it("percent-decodes the token before verifying it", async () => {
    const rawToken = "a b+c/d";
    renderRouteAt(`/invitation?token=${encodeURIComponent(rawToken)}`);

    // Decoded off the request path, so this pins the round trip end to end: the
    // router decodes the search parameter once, and the client re-encodes it as
    // a path segment — a token that survived neither would show up here.
    await vi.waitFor(() => {
      expect(verifiedToken()).toBe(rawToken);
    });
  });

  it("treats a link with no token as no token", async () => {
    renderRouteAt("/invitation");

    await expect
      .element(page.getByTestId("invitation-error"))
      .toHaveTextContent(/no invitation token provided/iu);
    expect(verifyCalls()).toEqual([]);
  });

  it("asks the API who the visitor is, and shows the accept button to a signed-in one", async () => {
    // The route's answer to the oracle's `AuthStateProvider` (see the header):
    // a resolved user IS the session. The auth cookie is HttpOnly, so the 200 is
    // the only thing the browser can observe about it.
    currentUserAnswer = () => Response.json({ id: "u-1", email: EMAIL });

    renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-accept")).toBeInTheDocument();
    expect(page.getByTestId("invitation-sign-in").query()).toBeNull();
  });

  it("treats the seam's anonymous answer as anonymous", async () => {
    // `getUser()` maps 401 to `null` WITHOUT throwing (Wallow-vec7.2.4) —
    // anonymous is an expected answer, not a failure. Stated as the WIRE answer
    // now, so the mapping itself is under test rather than assumed.
    currentUserAnswer = () => failure(UNAUTHORIZED);

    renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-sign-in")).toBeInTheDocument();
    expect(page.getByTestId("invitation-accept").query()).toBeNull();
  });

  it("treats a failed auth probe as anonymous rather than crashing the invitation", async () => {
    // The oracle's `catch { _isAuthenticated = false; }` (InvitationLanding.razor:
    // 133-136). A 500 from `/users/me` is not evidence of a session, and the
    // less-privileged branch is the safe read: it offers a sign-in link, where the
    // other offers an accept button whose `[Authorize]`d POST would 401.
    currentUserAnswer = () => failure(SERVER_ERROR);

    renderRouteAt(`/invitation?token=${TOKEN}`);

    await expect.element(page.getByTestId("invitation-sign-in")).toBeInTheDocument();
    expect(page.getByTestId("invitation-accept").query()).toBeNull();
    // And the invitation itself still verifies — the probe is an affordance, not
    // a gate on the page's content.
    await expect.element(page.getByTestId("invitation-info")).toBeInTheDocument();
  });

  it("treats a non-string token as absent rather than verifying a boolean", async () => {
    // TanStack's search parsing JSON-parses scalars, so `?token=true` arrives as
    // the BOOLEAN `true`, not the string "true" (bd memory on validateSearch).
    // Handing that to `verifyInvitation(token: string)` would put `true` in a URL
    // path segment.
    renderRouteAt("/invitation?token=true");

    await expect
      .element(page.getByTestId("invitation-error"))
      .toHaveTextContent(/no invitation token provided/iu);
    expect(verifyCalls()).toEqual([]);
  });
});
