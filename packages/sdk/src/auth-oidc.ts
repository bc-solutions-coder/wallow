/**
 * OIDC handshake URL builders + the open-redirect guard (Wallow-vec7.2.2).
 *
 * These are the handshake steps the generated OpenAPI client does NOT cover:
 * the protocol itself lives in OpenIddict on the API, so there are no ops to
 * wrap -- only navigation targets to build. Every helper here is PURE (string
 * in, string out): no fetch, no client, no session. The caller navigates.
 *
 * Each builder takes an explicit `origin` rather than reading config. Under the
 * wallow-auth proxy the origin is same-origin, but the Blazor oracle these port
 * from resolves it from `ApiBaseUrl` config, and keeping it a parameter leaves
 * the helpers pure and testable.
 */

/**
 * Strips a trailing '/' so `{origin}/path` never doubles the separator.
 * `ApiBaseUrl` comes from config and may be written either way.
 */
function normalizeOrigin(origin: string): string {
  return origin.replace(/\/+$/u, "");
}

/**
 * Throws unless `returnUrl` passes {@link isSafeReturnUrl}, naming the rejected
 * value so the caller can log what it refused.
 *
 * Ports `Login.razor` L533-540: an unsafe returnUrl is REFUSED, not sanitized.
 * `ReturnUrlValidator.Sanitize`'s silent "/" fallback is deliberately not used
 * here -- a builder that quietly swaps an attacker's returnUrl for "/" hands the
 * caller a URL it never asked for.
 */
function assertSafeReturnUrl(returnUrl: string): void {
  if (!isSafeReturnUrl(returnUrl)) {
    throw new TypeError(`unsafe return url: ${returnUrl}`);
  }
}

/**
 * True when `url` is a safe relative path -- it starts with exactly one '/'.
 *
 * Ports `ReturnUrlValidator.IsSafe` from
 * `api/src/Wallow.Auth/Helpers/ReturnUrlValidator.cs`, which rejects absolute
 * URLs, protocol-relative `//evil.com`, and dangerous schemes (`javascript:`,
 * `data:`) by the same single rule: a value that starts with '/' but not '//'
 * can only ever resolve against the current origin.
 *
 * Backslashes are normalized to forward slashes before the prefix check
 * (Wallow-41ot): WHATWG URL parsing treats a backslash as an extra path
 * separator for http/https, so `/\evil.com` resolves protocol-relative and
 * cross-origin even though it starts with a single '/'. ASCII tab/newline/CR
 * are stripped first because WHATWG URL parsing removes them before applying
 * backslash-as-separator logic, so `/\t\evil.com` would otherwise slip past
 * the prefix check yet still resolve cross-origin in a browser. A
 * percent-encoded backslash (`%5C`) is decoded next because a router can hand
 * the guard the decoded value; no other percent-encoding is touched, so
 * `/apps/my%20app` stays accepted.
 *
 * @param url Candidate return URL; nullish and blank are unsafe.
 */
export function isSafeReturnUrl(url: string | null | undefined): boolean {
  // string.IsNullOrWhiteSpace parity: a blank value is never a navigation target.
  if (url === null || url === undefined || url.trim() === "") {
    return false;
  }

  const normalized: string = url
    .replaceAll(/[\t\n\r]/gu, "")
    .replaceAll(/%5c/giu, "\\")
    .replaceAll("\\", "/");

  return normalized.startsWith("/") && !normalized.startsWith("//");
}

/**
 * Builds the OIDC authorization-request URL: `{origin}/connect/authorize?...`.
 *
 * Has no Blazor call site -- it is the reverse direction, where the API
 * redirects TO the login page -- and exists so app code never hand-rolls the
 * authorize target.
 *
 * @param origin Origin hosting the OIDC endpoints; a trailing '/' is ignored.
 * @param params Authorization request params (`client_id`, `scope`, ...),
 *   form-encoded. Empty params produce no '?'.
 */
export function buildConnectAuthorizeUrl(origin: string, params: Record<string, string>): string {
  const url: string = `${normalizeOrigin(origin)}/connect/authorize`;
  // Authorization request params are form-encoded (space => '+'), unlike the
  // Uri.EscapeDataString call sites below. No oracle constrains this one.
  const query: string = new URLSearchParams(params).toString();

  return query === "" ? url : `${url}?${query}`;
}

/**
 * Builds the navigation target for a consent decision, appending
 * `consent_granted=true` or `consent_denied=true` to `returnUrl`.
 *
 * Ports `Consent.razor`'s `AppendToReturnUrl` (L70-80): pick the separator from
 * whether `returnUrl` already contains a '?', then prepend `origin` because the
 * returnUrl is a relative path issued by the API's `/connect/authorize` and the
 * Auth app's own origin does not host it.
 *
 * @param origin Origin hosting `/connect/authorize`; a trailing '/' is ignored.
 * @param returnUrl Relative return URL from the authorize request. Nullish
 *   falls back to '/' (the oracle's `ReturnUrl ?? "/"`).
 * @param granted Whether the user approved the consent request.
 * @throws TypeError If `returnUrl` is present but fails {@link isSafeReturnUrl}.
 *   Per `Login.razor` L533-540 an unsafe returnUrl is refused, not sanitized.
 */
export function buildConsentSubmitUrl(
  origin: string,
  returnUrl: string | null | undefined,
  granted: boolean,
): string {
  // `ReturnUrl ?? "/"`: only nullish falls back. A PRESENT value -- including
  // the empty string -- is a caller-supplied return URL and must clear the guard.
  let baseUrl: string = "/";
  if (returnUrl !== null && returnUrl !== undefined) {
    assertSafeReturnUrl(returnUrl);
    baseUrl = returnUrl;
  }

  // Contains('?')-based, so a bare-'?' returnUrl joins with '&' too.
  const separator: string = baseUrl.includes("?") ? "&" : "?";
  const parameter: string = granted ? "consent_granted=true" : "consent_denied=true";

  return `${normalizeOrigin(origin)}${baseUrl}${separator}${parameter}`;
}

/**
 * Builds the sign-in-ticket exchange URL. The API's cookie-setting endpoint
 * trades the ticket for an auth cookie, then forwards to `returnUrl` -- so the
 * browser is authenticated before it reaches `/connect/authorize`.
 *
 * Ports `Login.razor` L544-550. Encoding follows `Uri.EscapeDataString`
 * (`encodeURIComponent`), NOT form encoding: a space must be `%20`, not '+'.
 *
 * @param origin Origin hosting the exchange endpoint; a trailing '/' is ignored.
 * @param ticket Single-use sign-in ticket issued by the login response.
 * @param returnUrl Relative URL to forward to once the cookie is set.
 * @throws TypeError If `ticket` is blank (message: "ticket is required"), or
 *   `returnUrl` fails {@link isSafeReturnUrl} (message: "unsafe return url").
 */
export function buildExchangeTicketUrl(origin: string, ticket: string, returnUrl: string): string {
  // The oracle only builds this URL inside `if (!IsNullOrEmpty(SignInTicket))`;
  // a ticketless exchange-ticket URL is never a valid navigation target.
  if (ticket.trim() === "") {
    throw new TypeError("ticket is required to build an exchange-ticket url");
  }

  assertSafeReturnUrl(returnUrl);

  return (
    `${normalizeOrigin(origin)}/v1/identity/auth/exchange-ticket` +
    `?ticket=${encodeURIComponent(ticket)}` +
    `&returnUrl=${encodeURIComponent(returnUrl)}`
  );
}

/**
 * Builds the OIDC end-session URL: `{origin}/connect/logout`, optionally
 * carrying `post_logout_redirect_uri`.
 *
 * Ports `Logout.razor`'s `LogoutUrl` getter (L66-77).
 *
 * `postLogoutRedirectUri` is deliberately NOT guarded by
 * {@link isSafeReturnUrl}: it is an absolute URI by definition, and OpenIddict
 * validates it server-side against the client's registered post-logout URIs.
 * Applying the relative-path guard would reject every legitimate caller.
 *
 * @param origin Origin hosting `/connect/logout`; a trailing '/' is ignored.
 * @param postLogoutRedirectUri Absolute URI to return to after sign-out.
 *   Nullish or blank omits the query parameter entirely.
 */
export function buildConnectLogoutUrl(
  origin: string,
  postLogoutRedirectUri?: string | null,
): string {
  const url: string = `${normalizeOrigin(origin)}/connect/logout`;

  // `if (!string.IsNullOrEmpty(PostLogoutRedirectUri))`.
  if (
    postLogoutRedirectUri === null ||
    postLogoutRedirectUri === undefined ||
    postLogoutRedirectUri === ""
  ) {
    return url;
  }

  return `${url}?post_logout_redirect_uri=${encodeURIComponent(postLogoutRedirectUri)}`;
}
