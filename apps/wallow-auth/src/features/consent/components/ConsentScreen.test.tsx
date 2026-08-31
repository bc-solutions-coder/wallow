import {
  captureFormSubmission,
  type CapturedFormSubmission,
} from "@bc-solutions-coder/testing/form-submission";
import { navigationEscapes } from "@bc-solutions-coder/testing/navigation-escape";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  createPassthroughHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as consentRoute } from "@app/routes/consent";
import { ConsentScreen } from "./ConsentScreen";

/**
 * Consent screen: the prompt, approve/deny hand-off, the open-redirect guard,
 * the error state, and the `/consent` route's search parsing.
 *
 * Runs the real SDK over a faked fetch (sdk-harness), so assertions read the
 * recorded request, not a spy. The decision is a native form POST, captured
 * and cancelled by `captureFormSubmission` so the body can be read. Its action
 * is same-origin: this app proxies `/connect/**` at its own root, so an
 * API-origin prepend would send the browser cross-origin and drop the cookie
 * the authorize endpoint needs.
 */

// Only the ROUTER is stubbed — `useNavigate` is how the screen reports an
// unsafe returnUrl, and it is not part of the SDK seam.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const CLIENT_ID = "wallow-web";
const RETURN_URL = "/connect/authorize?client_id=wallow-web&scope=openid";
const CONSENT_TOKEN = "single-use-token";
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/**
 * The `scope` query parameter as the authorize endpoint sends it: ONE
 * space-delimited string, forwarded to the context lookup RAW — OAuth's own
 * delimiter is the wire format both ends already speak.
 */
const SCOPE = "openid profile email";

/**
 * The transaction-scoped context endpoint — keyed by the pending request's
 * `returnUrl`, never a bare client id. This app's SDK is rooted at the origin,
 * so the recorded `path` is the bare endpoint path with no `/api` prefix.
 */
const AUTHORIZE_CONTEXT_PATH = "/v1/identity/auth/authorize-context";

/** The two failure statuses the error-state tests answer with. */
const NOT_FOUND = 404;
const SERVER_ERROR = 500;

/** Where the consent form posts: `RETURN_URL`'s path on this origin. */
const AUTHORIZE_ACTION = `${globalThis.location.origin}/connect/authorize`;

/** `RETURN_URL`'s query, as the hidden fields the form carries it in. */
const RETURN_URL_FIELDS: readonly (readonly [string, string])[] = [
  ["client_id", "wallow-web"],
  ["scope", "openid"],
];

/**
 * Every recorded request to the context endpoint, whatever the query — so a
 * screen that looked up the WRONG transaction is still counted here and fails
 * the parameter assertion rather than silently reading as "no request".
 */
function contextCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === AUTHORIZE_CONTEXT_PATH);
}

/**
 * One query parameter of the first context request, decoded. `null` means the
 * key was omitted. Read through `URL.searchParams` so value encoding — a
 * serializer detail, not this screen's contract — cannot break the test.
 * THROWS when no request was made, because an absent request would make every
 * parameter assertion pass vacuously.
 */
function requestParameter(name: "returnUrl" | "scope"): string | null {
  const [call] = contextCalls();
  if (call === undefined) {
    throw new Error("expected an authorize-context request, but the screen made none");
  }

  return new URL(call.url).searchParams.get(name);
}

/** Click one of the two answers and return the submission the browser assembled. */
async function answer(testId: "consent-approve" | "consent-deny"): Promise<CapturedFormSubmission> {
  const user = userEvent.setup();
  const submission: Promise<CapturedFormSubmission> = captureFormSubmission();

  await user.click(page.getByTestId(testId));

  return submission;
}

/** The value posted under `name`, or every value when it repeats. */
function posted(submission: CapturedFormSubmission, name: string): string[] {
  return submission.fields
    .filter(([field]: readonly [string, string]) => field === name)
    .map(([, value]: readonly [string, string]) => value);
}

/** An `AuthorizeContextResponse`, as the generated type shapes it. */
function authorizeContext(overrides: Record<string, unknown> = {}) {
  return {
    clientId: CLIENT_ID,
    displayName: "Wallow Web",
    tagline: null,
    logoUrl: null,
    themeJson: null,
    organizationName: null,
    firstParty: true,
    scopes: [
      { name: "openid", description: "Sign you in" },
      { name: "profile", description: "See your profile" },
    ],
    ...overrides,
  };
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

let harness: SdkHarness;

/**
 * Every request that reached the network in the current test. A function rather
 * than a direct `harness.calls` read because `harness` is reassigned per test,
 * and a closure inside a `for` loop capturing a reassigned binding is what
 * `no-loop-func` forbids.
 */
function recordedCalls(): readonly SdkCall[] {
  return harness.calls;
}

beforeEach(() => {
  vi.clearAllMocks();
  // The real SDK over a recording transport; the default answer is a loaded
  // consent prompt, which most tests below take as their starting point.
  harness = createPassthroughHarness();
  harness.resolveJson(authorizeContext());
});

describe("ConsentScreen — loading", () => {
  it("requests the context for the transaction in the query string", async () => {
    harness.pending();

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await vi.waitFor(() => {
      expect(contextCalls()).toHaveLength(1);
    });

    expect(contextCalls()[0]?.method).toBe("GET");
    // The transaction, not a client id, is the lookup key: the endpoint
    // validates it against a real pending authorize request.
    expect(requestParameter("returnUrl")).toBe(RETURN_URL);
  });

  it("forwards the space-delimited scope to the context lookup raw", async () => {
    harness.pending();

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} scope={SCOPE} />);

    await vi.waitFor(() => {
      expect(contextCalls()).toHaveLength(1);
    });

    // ONE space-joined `scope` value, unsplit — a re-joined or comma-joined
    // value arrives at the endpoint as a different request.
    expect(requestParameter("scope")).toBe(SCOPE);
  });

  it("asks with no scope when the link carries none", async () => {
    // A link without `scope` is malformed, not an invitation to invent a list.
    harness.pending();

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await vi.waitFor(() => {
      expect(contextCalls()).toHaveLength(1);
    });

    // The facade omits the key entirely for an absent value; an empty value is
    // equally acceptable to the controller, so both count as "asked with none".
    const scope: string | null = requestParameter("scope");

    expect(scope === null || scope === "").toBe(true);
  });

  it("shows no error while the request is still in flight", async () => {
    // A missing-context check that does not exclude the in-flight state
    // flashes "Unable to load consent information" at every user.
    harness.pending();

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    // Pin that a request is genuinely IN FLIGHT before asserting the absence,
    // or a screen that never fetched satisfies this by never fetching. The
    // harness records a request BEFORE running the responder, so a
    // never-settling one is still observable.
    await vi.waitFor(() => {
      expect(contextCalls()).toHaveLength(1);
    });
    expect(page.getByTestId("consent-error").query()).toBeNull();
  });

  it("shows no consent prompt before the client is known", async () => {
    // The user cannot approve access for an application not yet identified.
    harness.pending();

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await vi.waitFor(() => {
      expect(contextCalls()).toHaveLength(1);
    });
    expect(page.getByTestId("consent-heading").query()).toBeNull();
    expect(page.getByTestId("consent-approve").query()).toBeNull();
    expect(page.getByTestId("consent-deny").query()).toBeNull();
  });

  it("fires the request exactly once", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-heading")).toBeInTheDocument();

    expect(contextCalls()).toHaveLength(1);
  });
});

describe("ConsentScreen — the consent prompt", () => {
  it("names the requesting application in the heading", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect
      .element(page.getByTestId("consent-heading"))
      .toHaveTextContent(/Wallow Web is requesting access/u);
  });

  it("lists every requested scope", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-scopes")).toHaveTextContent("openid");
    await expect.element(page.getByTestId("consent-scopes")).toHaveTextContent("profile");
  });

  it("lists no scope the client did not request", async () => {
    harness.resolveJson(
      authorizeContext({ scopes: [{ name: "openid", description: "Sign you in" }] }),
    );

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-scopes")).toHaveTextContent("openid");
    await expect.element(page.getByTestId("consent-scopes")).not.toHaveTextContent("profile");
  });

  it("renders the prompt for a client requesting no scopes", async () => {
    harness.resolveJson(authorizeContext({ scopes: [] }));

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-scopes")).toBeInTheDocument();
    await expect.element(page.getByTestId("consent-approve")).toBeInTheDocument();
  });

  it("offers both an approve and a deny action", async () => {
    // A consent screen with only an approve path is not a consent screen.
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-approve")).toBeInTheDocument();
    await expect.element(page.getByTestId("consent-deny")).toBeInTheDocument();
  });

  it("shows no error alongside a loaded prompt", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-heading")).toBeInTheDocument();

    expect(page.getByTestId("consent-error").query()).toBeNull();
  });

  it("drops the pre-registration placeholder marker", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-heading")).toBeInTheDocument();

    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });
});

describe("ConsentScreen — approve", () => {
  it("posts the decision to the authorize endpoint rather than following a link", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} consentToken={CONSENT_TOKEN} />);

    const submission: CapturedFormSubmission = await answer("consent-approve");

    // A full-page POST: `/connect/authorize` is served by the passthrough
    // reverse proxy, not by the client-side route tree — and the endpoint
    // honours a decision only from a request body.
    expect(submission.method).toBe("post");
    expect(submission.action).toBe(AUTHORIZE_ACTION);
    expect(posted(submission, "consent_decision")).toEqual(["granted"]);
  });

  it("posts same-origin", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} consentToken={CONSENT_TOKEN} />);

    const submission: CapturedFormSubmission = await answer("consent-approve");

    // Pinned explicitly: this app has no browser-reachable API origin to
    // prepend even if it wanted one — the only API URL it knows is a
    // server-side internal address.
    expect(new URL(submission.action).origin).toBe(globalThis.location.origin);
  });

  it("carries the authorize request and the token as fields", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} consentToken={CONSENT_TOKEN} />);

    const submission: CapturedFormSubmission = await answer("consent-approve");

    // The returnUrl's query string becomes the body, so OpenIddict reads the
    // same request it redirected away from; the token is what lets the answer
    // through.
    for (const field of RETURN_URL_FIELDS) {
      expect(submission.fields).toContainEqual(field);
    }
    expect(posted(submission, "consent_token")).toEqual([CONSENT_TOKEN]);
  });

  it("posts to a returnUrl that has no query string of its own", async () => {
    await renderWithClient(
      <ConsentScreen returnUrl="/connect/authorize" consentToken={CONSENT_TOKEN} />,
    );

    const submission: CapturedFormSubmission = await answer("consent-approve");

    expect(submission.action).toBe(AUTHORIZE_ACTION);
    expect(submission.fields).toEqual([
      ["consent_token", CONSENT_TOKEN],
      ["consent_decision", "granted"],
    ]);
  });

  it("posts no token when the link carried none", async () => {
    // The endpoint refuses and re-asks with a fresh token; a fabricated value
    // would read as a forgery.
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    const submission: CapturedFormSubmission = await answer("consent-approve");

    expect(posted(submission, "consent_token")).toEqual([]);
  });

  it("does not deny while approving", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} consentToken={CONSENT_TOKEN} />);

    const submission: CapturedFormSubmission = await answer("consent-approve");

    // The two buttons differ by one value, so a mis-wired button is invisible
    // to a test that only checks that SOME submission happened.
    expect(posted(submission, "consent_decision")).toEqual(["granted"]);
  });
});

describe("ConsentScreen — deny", () => {
  it("posts the denial to the authorize endpoint rather than staying put", async () => {
    // A deny that silently did nothing strands the user on a dead consent screen
    // and leaves the relying party's authorize request hanging.
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} consentToken={CONSENT_TOKEN} />);

    const submission: CapturedFormSubmission = await answer("consent-deny");

    expect(submission.method).toBe("post");
    expect(submission.action).toBe(AUTHORIZE_ACTION);
    expect(posted(submission, "consent_decision")).toEqual(["denied"]);
  });

  it("carries the same request and token as an approval", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} consentToken={CONSENT_TOKEN} />);

    const submission: CapturedFormSubmission = await answer("consent-deny");

    // The endpoint has to know WHICH request was refused, and prove the refusal
    // came from the screen it issued.
    for (const field of RETURN_URL_FIELDS) {
      expect(submission.fields).toContainEqual(field);
    }
    expect(posted(submission, "consent_token")).toEqual([CONSENT_TOKEN]);
  });

  it("does not grant while denying", async () => {
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} consentToken={CONSENT_TOKEN} />);

    const submission: CapturedFormSubmission = await answer("consent-deny");

    // The side that matters: a Deny wired to `granted` authorizes the client
    // the user just refused.
    expect(posted(submission, "consent_decision")).toEqual(["denied"]);
  });
});

describe("ConsentScreen — the open-redirect guard", () => {
  const UNSAFE_RETURN_URLS: readonly string[] = [
    // Protocol-relative: looks relative, resolves off-origin. The guard's whole
    // reason to exist.
    "//evil.example/steal",
    // Absolute, off-origin.
    "https://evil.example/steal",
    // A scheme that executes rather than navigates. The `no-script-url` lint
    // exists to stop this string being USED as a URL; here it is the attack
    // being tested for, and rejecting it is the whole point of the case.
    // oxlint-disable-next-line no-script-url
    "javascript:alert(1)",
    // Present but blank: a supplied value that fails the guard, NOT the nullish
    // fallback case.
    "",
  ];

  for (const returnUrl of UNSAFE_RETURN_URLS) {
    it(`refuses to render a consent prompt for returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      await renderWithClient(<ConsentScreen returnUrl={returnUrl} />);

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalled();
      });

      // Refusing at CLICK time would be too late: the user would be asked to
      // authorize a request we had already decided was malformed.
      expect(page.getByTestId("consent-approve").query()).toBeNull();
      expect(page.getByTestId("consent-heading").query()).toBeNull();
    });

    it(`routes to the error page for returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      await renderWithClient(<ConsentScreen returnUrl={returnUrl} />);

      // REFUSE, do not silently fall back to "/". `href` rather than
      // `to`+`search`, so this screen does not couple to `/error`'s search shape.
      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
      });
    });

    it(`never navigates to the unsafe returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      await renderWithClient(<ConsentScreen returnUrl={returnUrl} />);

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalled();
      });

      // Whatever else happens, the browser must not be sent to any submit URL.
      expect(navigationEscapes()).toEqual([]);
    });

    it(`does not fetch the context for returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      await renderWithClient(<ConsentScreen returnUrl={returnUrl} />);

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalled();
      });

      // Bailing before the request keeps the client's display name and scope
      // list from being disclosed to an attacker-crafted link.
      expect(recordedCalls()).toEqual([]);
    });
  }

  it("shows no consent error for an unsafe returnUrl", async () => {
    // Flashing "Unable to load consent information" on the way to `/error`
    // misreports an open-redirect attempt as a transient server problem.
    await renderWithClient(<ConsentScreen returnUrl="//evil.example" />);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalled();
    });

    expect(page.getByTestId("consent-error").query()).toBeNull();
  });

  it("lets a safe returnUrl through untouched", async () => {
    // The negative control: a screen routing EVERY returnUrl to `/error` would
    // pass every other test in this block.
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-approve")).toBeInTheDocument();

    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});

describe("ConsentScreen — error state", () => {
  it("shows the error when no returnUrl is supplied", async () => {
    // With no returnUrl there is no pending transaction to describe; an absent
    // value is a malformed link, not a hostile one, so the screen errors in
    // place rather than routing to `/error`.
    await renderWithClient(<ConsentScreen />);

    await expect
      .element(page.getByTestId("consent-error"))
      .toHaveTextContent(/unable to load consent information/iu);

    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("does not call the endpoint when no returnUrl is supplied", async () => {
    // A screen that "helpfully" sent `returnUrl: undefined` would 404 and blame
    // the server for the link's own defect.
    await renderWithClient(<ConsentScreen />);

    await expect.element(page.getByTestId("consent-error")).toBeInTheDocument();

    expect(harness.calls).toEqual([]);
  });

  it("shows the error when the context request fails", async () => {
    // A non-2xx arrives as a REJECTION here, because the facade's `unwrap()`
    // throws rather than returning null. The endpoint answers 404 for an
    // expired transaction, an unknown client and a malformed returnUrl alike.
    harness.rejectJson({}, NOT_FOUND);

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect
      .element(page.getByTestId("consent-error"))
      .toHaveTextContent(/unable to load consent information/iu);
  });

  it("shows the same error for a server failure", async () => {
    // One message for every failure — this screen narrows on no status.
    harness.rejectJson({}, SERVER_ERROR);

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect
      .element(page.getByTestId("consent-error"))
      .toHaveTextContent(/unable to load consent information/iu);
  });

  it("survives a rejection that is not WallowError-shaped at all", async () => {
    // A network failure has no `status` and must land on the same error surface
    // rather than throwing inside the error branch. A transport that THROWS is
    // the faithful version: the generated client does not wrap its `fetch`, so
    // the raw rejection reaches React Query un-shaped.
    harness.respond(() => {
      throw new Error("network down");
    });

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-error")).toBeInTheDocument();
  });

  it("offers no approve or deny action in the error state", async () => {
    // With no context there is no scope list, so an Approve button here would
    // authorize an unknown client for unknown scopes.
    harness.rejectJson({}, NOT_FOUND);

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-error")).toBeInTheDocument();

    expect(page.getByTestId("consent-approve").query()).toBeNull();
    expect(page.getByTestId("consent-deny").query()).toBeNull();
    expect(page.getByTestId("consent-heading").query()).toBeNull();
    expect(page.getByTestId("consent-scopes").query()).toBeNull();
  });

  it("never leaks the raw rejection into the page", async () => {
    // `code: "UNKNOWN"` / `title: "Unknown error"` are seam artefacts, not
    // user-facing copy. An empty error body is what makes `toWallowError` fall
    // back to exactly those two values, so this is the strongest leak test.
    harness.rejectJson({}, NOT_FOUND);

    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-error")).toBeInTheDocument();

    expect(document.body.textContent).not.toMatch(/unknown error|UNKNOWN/u);
  });
});

/**
 * Route-level spec, rendered through a real memory router rather than by poking
 * at `Route.options.component`: the component reads its search through
 * `Route.useSearch()`, and every router hook dereferences a `null` router
 * outside a `RouterProvider`, so a bare render is unsatisfiable by any correct
 * implementation. The root is a throwaway — the app's real `__root.tsx` renders
 * `<html>`.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/consent", route: consentRoute }],
  });
}

describe("/consent route", () => {
  it("renders the consent screen for the transaction on the query string", async () => {
    await renderRouteAt(
      `/consent?client_id=${CLIENT_ID}&returnUrl=${encodeURIComponent(RETURN_URL)}` +
        `&consent_token=${CONSENT_TOKEN}`,
    );

    await expect.element(page.getByTestId("consent-heading")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
    // The query parameters must actually reach the screen, not merely parse:
    // `returnUrl` threads as far as the context lookup...
    expect(requestParameter("returnUrl")).toBe(RETURN_URL);

    // ...and as far as the form approve posts, together with `consent_token`.
    // A route that dropped the first would post to the "/" fallback; one that
    // dropped the second would post an answer the endpoint refuses.
    const submission: CapturedFormSubmission = await answer("consent-approve");
    expect(submission.action).toBe(AUTHORIZE_ACTION);
    for (const field of RETURN_URL_FIELDS) {
      expect(submission.fields).toContainEqual(field);
    }
    expect(posted(submission, "consent_token")).toEqual([CONSENT_TOKEN]);
  });

  it("reads the consent token off the query string", () => {
    const validateSearch = consentRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({ consent_token: CONSENT_TOKEN })).toEqual({
      returnUrl: undefined,
      client_id: undefined,
      scope: undefined,
      consent_token: CONSENT_TOKEN,
    });
  });

  it("reads returnUrl and client_id off the query string", () => {
    // The wire name is `client_id` (snake_case) — OpenIddict's parameter name.
    // It is parsed as relay cargo the authorize redirect sends along, though no
    // longer consumed: the context lookup keys on `returnUrl` alone.
    const validateSearch = consentRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch).toBeDefined();
    expect(validateSearch?.({ returnUrl: RETURN_URL, client_id: CLIENT_ID })).toEqual({
      returnUrl: RETURN_URL,
      client_id: CLIENT_ID,
    });
  });

  it("tolerates a query string with neither of them", () => {
    const validateSearch = consentRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({})).toEqual({
      returnUrl: undefined,
      client_id: undefined,
    });
  });

  it("reads the space-delimited scope off the query string", () => {
    // The wire name is `scope` (singular), matching OAuth; the value is kept as
    // the raw string here and forwarded to the lookup unsplit.
    const validateSearch = consentRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({ returnUrl: RETURN_URL, client_id: CLIENT_ID, scope: SCOPE })).toEqual(
      { returnUrl: RETURN_URL, client_id: CLIENT_ID, scope: SCOPE },
    );
  });

  it("treats a non-string scope as absent", () => {
    // TanStack Router's default search parser JSON-parses every value BEFORE
    // `validateSearch` sees it, so `?scope=123` arrives as a NUMBER. Same rule
    // the other two parameters follow: anything non-string is absent.
    const validateSearch = consentRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({ scope: 123 })).toEqual({
      returnUrl: undefined,
      client_id: undefined,
      scope: undefined,
    });
  });

  it("threads the scope from the query string all the way to the lookup", async () => {
    // The whole chain: authorize redirect -> route search -> screen ->
    // context request. Parsing `scope` without handing it down leaves the
    // consent list blank.
    await renderRouteAt(
      `/consent?client_id=${CLIENT_ID}&returnUrl=${encodeURIComponent(RETURN_URL)}` +
        `&scope=${encodeURIComponent(SCOPE)}`,
    );

    await vi.waitFor(() => {
      expect(contextCalls()).toHaveLength(1);
    });

    expect(requestParameter("scope")).toBe(SCOPE);
  });
});
