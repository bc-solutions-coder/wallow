import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  createPassthroughHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as logoutRoute } from "@app/routes/logout";
import { LogoutScreen } from "./LogoutScreen";

/**
 * Logout screen — confirm step, signed-out landing, and the `/logout` route.
 *
 * One route, two phases, driven off `signed_out`. Both share the
 * `logout-confirm-heading` testid, so the heading TEXT is what tells them apart.
 *
 * `post_logout_redirect_uri` is an absolute URI, so `isSafeReturnUrl` must NOT
 * guard it: the defence is the server's allow-list probe, whose 200 carries no
 * schema — the screen narrows `{ allowed: true }` itself, and fails closed.
 */

/** A registered post-logout URI: absolute, and another origin than this one. */
const REDIRECT_URI = "https://app.wallow.test/signed-out";

/**
 * The allow-list probe — the only endpoint this screen touches, which is why the
 * responses below can be programmed with a blanket `harness.respond`.
 */
const VALIDATE_ENDPOINT = "/v1/identity/auth/redirect-uri/validate";

const OK_STATUS = 200;

let harness: SdkHarness;

/** The `{ allowed }` body the API sends. */
function allowedBody(allowed: boolean): unknown {
  return { allowed };
}

/**
 * Answer the validation probe with `body`. `undefined` is sent as a BODYLESS 200,
 * the closest the wire has to "the operation resolved undefined"; the generated
 * client parses that to `{}`.
 */
function answerValidation(body: unknown): void {
  harness.respond(() =>
    body === undefined
      ? new Response(null, { status: OK_STATUS })
      : Response.json(body, { status: OK_STATUS }),
  );
}

/**
 * Refuse the validation probe with `status`. The screen never reads the code —
 * only that the call rejected — so the body is deliberately uninformative.
 */
function refuseValidation(status: number): void {
  harness.rejectJson({ title: "Validation failed", status }, status);
}

/** Every recorded probe of the allow-list endpoint, in order. */
function validationCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === VALIDATE_ENDPOINT);
}

/** The `uri` each recorded probe asked about. */
function probedUris(): readonly (string | null)[] {
  return validationCalls().map((call: SdkCall) => new URL(call.url).searchParams.get("uri"));
}

/**
 * The sign-out anchor's RESOLVED destination — what the browser would actually
 * navigate to, origin included. The `href` ATTRIBUTE is the relative string the
 * builder produced; the `href` PROPERTY resolves it against this document, which
 * is the only way a foreign origin becomes visible to an assertion.
 */
function resolvedLogoutUrl(): URL {
  return new URL((page.getByTestId("logout-confirm-button").element() as HTMLAnchorElement).href);
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

beforeEach(() => {
  harness = createPassthroughHarness();
  answerValidation(allowedBody(true));
});

describe("LogoutScreen — the confirm step", () => {
  it("heads the card 'Sign out'", async () => {
    await renderWithClient(<LogoutScreen />);

    await expect.element(page.getByTestId("logout-confirm-heading")).toHaveTextContent("Sign out");
  });

  it("asks for confirmation before signing the user out", async () => {
    await renderWithClient(<LogoutScreen />);

    // A sign-out that fires on navigation rather than on a click is a CSRF sink:
    // `<img src="/logout">` would end the session.
    await expect.element(page.getByText("Are you sure you want to sign out?")).toBeInTheDocument();
  });

  it("points the sign-out button at this origin's /connect/logout", async () => {
    await renderWithClient(<LogoutScreen />);

    // THE ORIGIN TRAP. The passthrough proxy serves /connect/** at the root, so
    // the hand-off must stay same-origin or the SameSite auth cookie never
    // reaches the endpoint that needs it to know whose session to end.
    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute("href", "/connect/logout");
  });

  it("builds that URL against the empty origin, not a configured API base URL", async () => {
    await renderWithClient(<LogoutScreen />);

    // Asserted on the RESOLVED destination: an absolute API base prepended to the
    // path is indistinguishable from the href ATTRIBUTE alone, and the origin is
    // the part that decides whether the SameSite auth cookie travels.
    const target: URL = resolvedLogoutUrl();

    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe("/connect/logout");
  });

  it("carries post_logout_redirect_uri through to the logout URL", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    // OpenIddict needs this on the END-SESSION request to know where to send the
    // browser back to; dropping it here strands the user on the landing page.
    expect(resolvedLogoutUrl().searchParams.get("post_logout_redirect_uri")).toBe(REDIRECT_URI);
    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute(
        "href",
        `/connect/logout?post_logout_redirect_uri=${encodeURIComponent(REDIRECT_URI)}`,
      );
  });

  it("omits the parameter entirely when no redirect URI was supplied", async () => {
    await renderWithClient(<LogoutScreen />);

    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute("href", "/connect/logout");
  });

  it("treats an empty post_logout_redirect_uri as absent", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri="" />);

    // `?post_logout_redirect_uri=` is a malformed link, not a request to return
    // to the empty string.
    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute("href", "/connect/logout");
  });

  it("does not validate the redirect URI on the confirm step", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    // Validating here would be wasted — the API re-validates the parameter on
    // the end-session request itself — and would leak a probe on every render of
    // the prompt. Anchored on the prompt actually rendering, so this cannot pass
    // by the screen simply not being the confirm step.
    await expect.element(page.getByTestId("logout-confirm-button")).toBeInTheDocument();
    expect(harness.calls).toHaveLength(0);
  });

  it("does not apply the relative-path returnUrl guard to an absolute URI", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    // `isSafeReturnUrl` demands a single leading '/', so applying it to a
    // post-logout URI would reject every legitimate relying party. Stated as the
    // OUTCOME a guarded screen could not produce: the absolute URI survives,
    // unrefused and unsanitized, into the logout URL.
    expect(resolvedLogoutUrl().searchParams.get("post_logout_redirect_uri")).toBe(REDIRECT_URI);
    await expect.element(page.getByTestId("logout-confirm-button")).toBeInTheDocument();
  });

  it("does not render the signed-out copy", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    // Anchored on the confirm step being present: telling a user who has NOT
    // signed out that they have is the failure this pins.
    await expect.element(page.getByTestId("logout-confirm-heading")).toHaveTextContent("Sign out");
    expect(page.getByText("You have been successfully signed out.").query()).toBeNull();
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it("renders the sign-out control as a link, not a button", async () => {
    await renderWithClient(<LogoutScreen />);

    // This has to be a real navigation: /connect/logout is served by the
    // passthrough proxy and is not in the client-side route tree, so a
    // router-driven control would 404 in-app.
    expect(page.getByTestId("logout-confirm-button").element().tagName).toBe("A");
  });
});

describe("LogoutScreen — signed_out is an exact string match", () => {
  it.each([
    ["false", "the literal string false"],
    ["TRUE", "a differently-cased true"],
    ["True", "a pascal-cased true"],
    ["1", "a truthy-looking 1"],
    ["", "an empty value"],
    ["yes", "an unrelated value"],
  ])("shows the confirm step for signed_out=%s (%s)", async (signedOut: string) => {
    await renderWithClient(<LogoutScreen signedOut={signedOut} />);

    // An ordinal string equality, not a boolean parse — and it fails in the safe
    // direction: anything else falls to the CONFIRM step, so a mangled link asks
    // again rather than telling a still-signed-in user they are signed out.
    await expect.element(page.getByTestId("logout-confirm-heading")).toHaveTextContent("Sign out");
    await expect.element(page.getByTestId("logout-confirm-button")).toBeInTheDocument();
  });

  it("shows the confirm step when signed_out is absent", async () => {
    await renderWithClient(<LogoutScreen />);

    await expect.element(page.getByTestId("logout-confirm-heading")).toHaveTextContent("Sign out");
  });

  it("shows the landing only for exactly 'true'", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" />);

    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(page.getByTestId("logout-confirm-button").query()).toBeNull();
  });
});

describe("LogoutScreen — the signed-out landing", () => {
  it("heads the card 'Signed out'", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" />);

    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
  });

  it("confirms the sign-out succeeded", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" />);

    await expect
      .element(page.getByText("You have been successfully signed out."))
      .toBeInTheDocument();
  });

  it("does not offer to sign the user out again", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The session is already gone, so a second sign-out control here would be a
    // dead end. Anchored on the landing actually rendering.
    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(page.getByTestId("logout-confirm-button").query()).toBeNull();
    expect(page.getByText("Are you sure you want to sign out?").query()).toBeNull();
  });

  it("validates the post-logout redirect URI against the server", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The right endpoint, as a read, asking about the right URI — and asking it
    // UNSCOPED, because sending a client id would narrow the allow-list the
    // answer is drawn from.
    await vi.waitFor(() => {
      expect(probedUris()).toEqual([REDIRECT_URI]);
    });
    expect(validationCalls()[0]?.method).toBe("GET");
    expect(new URL(validationCalls()[0]?.url ?? "").searchParams.has("clientId")).toBe(false);
  });

  it("offers a link back to the application once the URI is allowed", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The href is the RAW post-logout URI, not the /connect/logout URL — this is
    // the return trip.
    await expect
      .element(page.getByTestId("logout-return-link"))
      .toHaveAttribute("href", REDIRECT_URI);
    await expect
      .element(page.getByTestId("logout-return-link"))
      .toHaveTextContent("Return to application");
  });

  it("does not link to a URI the server refused", async () => {
    answerValidation(allowedBody(false));

    await renderWithClient(
      <LogoutScreen signedOut="true" postLogoutRedirectUri="https://evil.test/collect" />,
    );

    // THE OPEN-REDIRECT DEFENCE. `signed_out` and `post_logout_redirect_uri` are
    // both attacker-suppliable — this landing renders for anyone who types the
    // URL, with no proof a sign-out ever happened. Only the server can permit the
    // link, which is what keeps this page from laundering a branded link to an
    // arbitrary origin.
    await vi.waitFor(() => {
      expect(validationCalls()).not.toHaveLength(0);
    });
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it("never puts an unvalidated URI in the DOM, even briefly", async () => {
    harness.pending();

    const { container } = await renderWithClient(
      <LogoutScreen signedOut="true" postLogoutRedirectUri="https://evil.test/collect" />,
    );

    // A link rendered optimistically and retracted on the answer is a link a fast
    // user can click — the whole point of the check is that it gates FIRST.
    // Anchored on the landing having rendered, so an empty screen cannot
    // satisfy this.
    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
    expect(container.innerHTML).not.toContain("evil.test");
  });

  it("still confirms the sign-out while the validation is in flight", async () => {
    harness.pending();

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // Only the LINK waits on the probe: the user is told the sign-out worked
    // without waiting on a check that has nothing to do with it.
    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    await expect
      .element(page.getByText("You have been successfully signed out."))
      .toBeInTheDocument();
  });

  it("skips validation entirely when no redirect URI was supplied", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" />);

    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(harness.calls).toHaveLength(0);
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it("skips validation when the redirect URI is empty", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri="" />);

    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(harness.calls).toHaveLength(0);
  });
});

describe("LogoutScreen — the validation response is untyped and must fail closed", () => {
  it("links when the body is exactly { allowed: true }", async () => {
    answerValidation(allowedBody(true));

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    await expect.element(page.getByTestId("logout-return-link")).toBeInTheDocument();
  });

  it.each<[unknown, string]>([
    [{ allowed: "true" }, "the STRING 'true' rather than the boolean"],
    [{ allowed: 1 }, "a truthy non-boolean"],
    [{ allowed: null }, "an explicit null"],
    [{}, "a body missing the key"],
    [null, "a null body"],
    // Over a real transport `undefined` is a 200 with NOTHING in it, which the
    // generated client parses to `{}` — the same observation as "a body missing
    // the key". Stated anyway: an answerless 200 is not permission.
    [undefined, "an undefined body"],
    ["allowed", "a bare string body"],
    [true, "a bare boolean body"],
    [{ Allowed: true }, "the C# PascalCase key the wire does not use"],
  ])("refuses to link when the body is %j (%s)", async (body: unknown) => {
    answerValidation(body);

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The SDK hands this screen an `unknown` — the spec declares the 200 with no
    // schema — so the narrowing is the screen's own, and it is STRICT. A screen
    // that leaned on JS truthiness would link on `allowed: "false"`, which is a
    // string and truthy.
    await vi.waitFor(() => {
      expect(validationCalls()).not.toHaveLength(0);
    });
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it.each([
    [400, "a bad request"],
    [401, "an unauthenticated call"],
    [404, "an unregistered client"],
    [500, "a server fault"],
  ])("refuses to link when validation rejects with %i (%s)", async (status: number) => {
    refuseValidation(status);

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // A refusal arrives as a REJECTION, because the SDK throws on non-2xx.
    // FAILING CLOSED is the whole point: an unreachable validator must not become
    // a reason to trust the attacker's URI.
    await vi.waitFor(() => {
      expect(validationCalls()).not.toHaveLength(0);
    });
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it("surfaces no error state when validation fails", async () => {
    refuseValidation(500);

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // There is NO error state on this screen. A failed validation is not the
    // user's problem: they ARE signed out, which is what they came for, and only
    // the convenience link is lost.
    await vi.waitFor(() => {
      expect(validationCalls()).not.toHaveLength(0);
    });
    expect(page.getByTestId("logout-error").query()).toBeNull();
    await expect
      .element(page.getByText("You have been successfully signed out."))
      .toBeInTheDocument();
  });
});

describe("LogoutScreen — the footer", () => {
  it.each([
    ["the confirm step", undefined],
    ["the signed-out landing", "true"],
  ])("links back to sign in from %s", async (_phase: string, signedOut: string | undefined) => {
    await renderWithClient(<LogoutScreen signedOut={signedOut} />);

    await expect.element(page.getByTestId("logout-back-link")).toHaveAttribute("href", "/login");
    await expect.element(page.getByTestId("logout-back-link")).toHaveTextContent("Back to sign in");
  });
});

/**
 * `logout-return-link` is the one element on this screen gated by a security
 * check, so it stays addressable: without a testid the open-redirect defence is
 * untestable from E2E, which is where it most needs proving.
 */
describe("LogoutScreen — testids", () => {
  it("exposes the oracle's testids on the confirm step", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    await expect.element(page.getByTestId("logout-confirm-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("logout-confirm-button")).toBeInTheDocument();
    await expect.element(page.getByTestId("logout-back-link")).toBeInTheDocument();
  });

  it("exposes the oracle's testids on the signed-out landing", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    await expect.element(page.getByTestId("logout-confirm-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("logout-return-link")).toBeInTheDocument();
    await expect.element(page.getByTestId("logout-back-link")).toBeInTheDocument();
  });
});

/**
 * Mounts the REAL route through a memory router: bare-rendering a route component
 * that reads search params throws, because `useSearch` needs router context.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/logout", route: logoutRoute }],
  });
}

describe("/logout route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    await renderRouteAt("/logout");

    await expect.element(page.getByTestId("logout-confirm-heading")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("threads post_logout_redirect_uri from the query string into the logout URL", async () => {
    await renderRouteAt(`/logout?post_logout_redirect_uri=${encodeURIComponent(REDIRECT_URI)}`);

    // Must actually REACH the screen, not merely parse: a route that dropped it
    // would build a bare /connect/logout and strand the user after sign-out.
    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute(
        "href",
        `/connect/logout?post_logout_redirect_uri=${encodeURIComponent(REDIRECT_URI)}`,
      );
  });

  it("threads signed_out from the query string into the phase choice", async () => {
    await renderRouteAt(
      `/logout?signed_out=true&post_logout_redirect_uri=${encodeURIComponent(REDIRECT_URI)}`,
    );

    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    await vi.waitFor(() => {
      expect(probedUris()).toEqual([REDIRECT_URI]);
    });
  });

  it("reads both parameters off the query string under their wire names", () => {
    // Both wire names are snake_case: `post_logout_redirect_uri` is OpenIddict's
    // own parameter name and is not this screen's to rename, even though the prop
    // it feeds is `postLogoutRedirectUri`.
    const validateSearch = logoutRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch).toBeDefined();
    expect(
      validateSearch?.({ post_logout_redirect_uri: REDIRECT_URI, signed_out: "true" }),
    ).toEqual({
      post_logout_redirect_uri: REDIRECT_URI,
      signed_out: "true",
    });
  });

  it("tolerates a query string with neither of them", () => {
    const validateSearch = logoutRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({})).toEqual({
      post_logout_redirect_uri: undefined,
      signed_out: undefined,
    });
  });

  it("treats a non-string signed_out as absent rather than crashing", () => {
    const validateSearch = logoutRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    // `?signed_out[]=true` parses to an array. Reaching the same answer must not
    // throw, because that would turn a junk link into a blank page.
    expect(validateSearch?.({ signed_out: ["true"], post_logout_redirect_uri: 42 })).toEqual({
      post_logout_redirect_uri: undefined,
      signed_out: undefined,
    });
  });
});
