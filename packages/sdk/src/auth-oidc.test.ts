/**
 * OIDC handshake URL builders + the open-redirect guard (Wallow-vec7.2.2).
 *
 * These helpers are PURE: no fetch, no client, no mocks. Every test is an exact
 * string assertion against the Blazor oracle it ports, so the C# call site and
 * the TypeScript builder cannot drift silently.
 *
 * ORACLE MAP (api/src/Wallow.Auth/...):
 *   isSafeReturnUrl        <- Helpers/ReturnUrlValidator.cs IsSafe
 *   buildConsentSubmitUrl  <- Components/Pages/Consent.razor AppendToReturnUrl (L70-80)
 *   buildExchangeTicketUrl <- Components/Pages/Login.razor (L544-550)
 *   buildConnectLogoutUrl  <- Components/Pages/Logout.razor LogoutUrl (L66-77)
 *   buildConnectAuthorizeUrl -- no single call site; the API redirects TO the
 *     login page, so this is the reverse direction and is specified here only.
 *
 * GUARD CONTRACT: Login.razor L533-540 is the authoritative treatment of an
 * unsafe returnUrl -- it checks IsSafe and REFUSES to build the URL, bailing to
 * /error?reason=invalid_redirect_uri. The builders port that refusal as a thrown
 * TypeError rather than ReturnUrlValidator.Sanitize's silent "/" fallback: a
 * builder that quietly swaps an attacker's returnUrl for "/" would hand the
 * caller a URL it never asked for. The caller catches and decides where to go.
 * Assertions match on the thrown MESSAGE, never on a class identity, so no
 * instanceof-across-module-graphs trap exists here (bd memory
 * vitest-resetmodules-breaks-instanceof-across-graphs).
 */

import { describe, expect, it } from "vitest";

import {
  buildConnectAuthorizeUrl,
  buildConnectLogoutUrl,
  buildConsentSubmitUrl,
  buildExchangeTicketUrl,
  isSafeReturnUrl,
} from "./auth-oidc";

/** Same origin the wallow-auth proxy fronts; mirrors Blazor's ApiBaseUrl config value. */
const ORIGIN: string = "http://localhost:5001";

/**
 * Every shape ReturnUrlValidator.IsSafe rejects. Reused by the guard tests AND
 * by each builder's reject case, so a builder that forgets to call the guard
 * fails loudly for the exact input class it let through.
 */
const UNSAFE_RETURN_URLS: readonly (readonly [label: string, url: string])[] = [
  ["a protocol-relative URL", "//evil.com/steal"],
  ["a protocol-relative URL with no path", "//evil.com"],
  ["an absolute http URL", "http://evil.com/steal"],
  ["an absolute https URL", "https://evil.com/steal"],
  // oxlint no-script-url flags `javascript:` string literals as executable-URL
  // construction. Here it is the ATTACK STRING under test -- the guard exists
  // precisely to reject it -- so the rule is inverted and disabled for the row.
  // eslint-disable-next-line no-script-url
  ["a javascript: URI", "javascript:alert(1)"],
  ["a data: URI", "data:text/html,<script>alert(1)</script>"],
  ["a scheme-only URI", "vbscript:msgbox(1)"],
  ["a path with no leading slash", "dashboard"],
  ["a backslash-relative path", String.raw`\\evil.com/steal`],
  ["an empty string", ""],
  ["whitespace only", "   "],
];

describe("isSafeReturnUrl", () => {
  it.each([
    ["a root path", "/"],
    ["a single-segment path", "/dashboard"],
    ["a nested path", "/connect/authorize"],
    ["a path with a query string", "/connect/authorize?client_id=web&scope=openid"],
    ["a path with a fragment", "/dashboard#section"],
    ["a path with an encoded segment", "/apps/my%20app"],
  ])("accepts %s", (_label: string, url: string) => {
    expect(isSafeReturnUrl(url)).toBe(true);
  });

  it.each(UNSAFE_RETURN_URLS)("rejects %s", (_label: string, url: string) => {
    expect(isSafeReturnUrl(url)).toBe(false);
  });

  it.each([
    ["undefined", undefined],
    ["null", null],
  ])("rejects %s", (_label: string, url: string | null | undefined) => {
    expect(isSafeReturnUrl(url)).toBe(false);
  });

  it("rejects '//' exactly -- the protocol-relative prefix with nothing after it", () => {
    // The one case that both starts with '/' and starts with '//': the second
    // clause of IsSafe is what rejects it. A guard written as a bare
    // `startsWith('/')` passes every other case in this suite but fails here.
    expect(isSafeReturnUrl("//")).toBe(false);
  });
});

describe("buildConnectAuthorizeUrl", () => {
  it("builds the authorize endpoint with the params as a query string", () => {
    expect(
      buildConnectAuthorizeUrl(ORIGIN, {
        client_id: "wallow-web",
        response_type: "code",
        scope: "openid profile",
      }),
    ).toBe(
      `${ORIGIN}/connect/authorize?client_id=wallow-web&response_type=code&scope=openid+profile`,
    );
  });

  it("omits the '?' entirely when there are no params", () => {
    expect(buildConnectAuthorizeUrl(ORIGIN, {})).toBe(`${ORIGIN}/connect/authorize`);
  });

  it("percent-encodes param values that contain URL-significant characters", () => {
    expect(buildConnectAuthorizeUrl(ORIGIN, { redirect_uri: "http://localhost:3000/cb?a=b" })).toBe(
      `${ORIGIN}/connect/authorize?redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Fcb%3Fa%3Db`,
    );
  });

  it("does not double the slash when the origin carries a trailing one", () => {
    // ApiBaseUrl comes from config and may be written either way.
    expect(buildConnectAuthorizeUrl(`${ORIGIN}/`, {})).toBe(`${ORIGIN}/connect/authorize`);
  });
});

describe("buildConsentSubmitUrl", () => {
  it("appends consent_granted=true and prepends the origin when granted", () => {
    expect(buildConsentSubmitUrl(ORIGIN, "/connect/authorize", true)).toBe(
      `${ORIGIN}/connect/authorize?consent_granted=true`,
    );
  });

  it("appends consent_denied=true when denied", () => {
    expect(buildConsentSubmitUrl(ORIGIN, "/connect/authorize", false)).toBe(
      `${ORIGIN}/connect/authorize?consent_denied=true`,
    );
  });

  it("joins with '&' when the returnUrl already carries a query string", () => {
    expect(
      buildConsentSubmitUrl(ORIGIN, "/connect/authorize?client_id=web&scope=openid", true),
    ).toBe(`${ORIGIN}/connect/authorize?client_id=web&scope=openid&consent_granted=true`);
  });

  it("joins with '&' when the returnUrl ends in a bare '?'", () => {
    // Contains('?') is true, so the C# oracle picks '&' here too.
    expect(buildConsentSubmitUrl(ORIGIN, "/connect/authorize?", true)).toBe(
      `${ORIGIN}/connect/authorize?&consent_granted=true`,
    );
  });

  it.each([
    ["undefined", undefined],
    ["null", null],
  ])(
    "falls back to the root path when returnUrl is %s",
    (_l: string, returnUrl?: string | null) => {
      // Mirrors the oracle's `string baseUrl = ReturnUrl ?? "/"`.
      expect(buildConsentSubmitUrl(ORIGIN, returnUrl, true)).toBe(
        `${ORIGIN}/?consent_granted=true`,
      );
    },
  );

  it.each(UNSAFE_RETURN_URLS)(
    "throws rather than build a URL from %s",
    (_l: string, url: string) => {
      expect(() => buildConsentSubmitUrl(ORIGIN, url, true)).toThrow(/unsafe return url/i);
    },
  );

  it("names the rejected value in the thrown message", () => {
    expect(() => buildConsentSubmitUrl(ORIGIN, "//evil.com", true)).toThrow(/\/\/evil\.com/u);
  });
});

describe("buildExchangeTicketUrl", () => {
  it("builds the exchange-ticket endpoint with ticket and returnUrl", () => {
    expect(buildExchangeTicketUrl(ORIGIN, "tkt-123", "/connect/authorize")).toBe(
      `${ORIGIN}/v1/identity/auth/exchange-ticket?ticket=tkt-123&returnUrl=%2Fconnect%2Fauthorize`,
    );
  });

  it("encodes with encodeURIComponent semantics, not form encoding", () => {
    // The oracle uses Uri.EscapeDataString: a space becomes %20, NOT '+'. A
    // URLSearchParams-based implementation produces '+' here and fails.
    expect(buildExchangeTicketUrl(ORIGIN, "a b+c", "/a b")).toBe(
      `${ORIGIN}/v1/identity/auth/exchange-ticket?ticket=a%20b%2Bc&returnUrl=%2Fa%20b`,
    );
  });

  it("preserves the returnUrl's own query string through encoding", () => {
    expect(buildExchangeTicketUrl(ORIGIN, "tkt", "/connect/authorize?client_id=web")).toBe(
      `${ORIGIN}/v1/identity/auth/exchange-ticket?ticket=tkt&returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dweb`,
    );
  });

  it.each(UNSAFE_RETURN_URLS)(
    "throws rather than build a URL from %s",
    (_l: string, url: string) => {
      expect(() => buildExchangeTicketUrl(ORIGIN, "tkt", url)).toThrow(/unsafe return url/i);
    },
  );

  it.each([
    ["an empty ticket", ""],
    ["a whitespace-only ticket", "  "],
  ])("throws on %s", (_label: string, ticket: string) => {
    // A ticketless exchange-ticket URL is never a valid navigation target; the
    // oracle only builds one inside `if (!string.IsNullOrEmpty(SignInTicket))`.
    // Matched on the full contract phrase, not a bare /ticket/ -- the builder's
    // own name contains "ticket", so a loose pattern matches any incidental
    // throw from inside it and asserts nothing.
    expect(() => buildExchangeTicketUrl(ORIGIN, ticket, "/dashboard")).toThrow(
      /ticket is required/i,
    );
  });

  it("does not double the slash when the origin carries a trailing one", () => {
    expect(buildExchangeTicketUrl(`${ORIGIN}/`, "tkt", "/x")).toBe(
      `${ORIGIN}/v1/identity/auth/exchange-ticket?ticket=tkt&returnUrl=%2Fx`,
    );
  });
});

describe("buildConnectLogoutUrl", () => {
  it("builds the bare logout endpoint when no redirect URI is given", () => {
    expect(buildConnectLogoutUrl(ORIGIN)).toBe(`${ORIGIN}/connect/logout`);
  });

  it.each([
    ["undefined", undefined],
    ["null", null],
    ["an empty string", ""],
  ])(
    "omits the query entirely when postLogoutRedirectUri is %s",
    (_label: string, uri?: string | null) => {
      // Mirrors the oracle's `if (!string.IsNullOrEmpty(PostLogoutRedirectUri))`.
      expect(buildConnectLogoutUrl(ORIGIN, uri)).toBe(`${ORIGIN}/connect/logout`);
    },
  );

  it("appends an encoded post_logout_redirect_uri when given", () => {
    expect(buildConnectLogoutUrl(ORIGIN, "http://localhost:3000/signed-out")).toBe(
      `${ORIGIN}/connect/logout?post_logout_redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Fsigned-out`,
    );
  });

  it("accepts an ABSOLUTE post-logout redirect URI -- it is not returnUrl-shaped", () => {
    // Unlike returnUrl, this value is an absolute URI by definition and is
    // validated server-side against the client's registered post-logout URIs.
    // isSafeReturnUrl must NOT be applied to it -- doing so rejects every real
    // caller. The oracle (Logout.razor L66-77) applies no guard here.
    expect(() => buildConnectLogoutUrl(ORIGIN, "https://app.example.com/bye")).not.toThrow();
  });

  it("encodes with encodeURIComponent semantics, not form encoding", () => {
    expect(buildConnectLogoutUrl(ORIGIN, "http://x/a b")).toBe(
      `${ORIGIN}/connect/logout?post_logout_redirect_uri=http%3A%2F%2Fx%2Fa%20b`,
    );
  });

  it("does not double the slash when the origin carries a trailing one", () => {
    expect(buildConnectLogoutUrl(`${ORIGIN}/`)).toBe(`${ORIGIN}/connect/logout`);
  });
});
