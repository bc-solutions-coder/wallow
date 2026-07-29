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
 * An ABSOLUTE return URL the API's own allow-list has already vouched for
 * (Wallow-a6jr).
 *
 * {@link isSafeReturnUrl} answers one question -- "can this value only ever
 * resolve against the current origin?" -- and every absolute URL fails it. But
 * the external-login MFA hand-off is absolute by construction: the redirect URL
 * `AccountController.ExternalLoginCallback` sends to `/mfa/challenge` either
 * passed `IsAllowedAsync` (which requires `Uri.TryCreate(UriKind.Absolute)`) or
 * is the `AuthUrl` fallback. A screen holding one of those has knowledge the
 * builder does not: it asked `/redirect-uri/validate` and the server said yes.
 *
 * This type is how that knowledge TRAVELS to the builder. A bare `string`
 * returnUrl still means "prove it is relative"; this wrapper means "the
 * server's allow-list already vouched for this absolute one, scoped to the
 * client the flow belongs to". A caller that never ran the probe cannot make
 * one accidentally -- {@link allowListedReturnUrl} demands the verdict as an
 * argument -- so the relative-only rule is never relaxed by default.
 */
export interface AllowListedReturnUrl {
  /** The absolute URL the allow-list admitted. */
  readonly url: string;
  /** Always `true`; the discriminant that separates this from a bare returnUrl. */
  readonly allowListed: true;
}

/** Schemes an exchange-ticket hand-off may navigate to. See {@link assertHandOffUrl}. */
const HAND_OFF_PROTOCOLS: ReadonlySet<string> = new Set(["http:", "https:"]);

/**
 * Throws unless `url` is an absolute http(s) URL.
 *
 * The SDK cannot re-run the server's allow-list, but it can insist on the SHAPE
 * the allow-list can only ever have admitted: `IsAllowedAsync` matches the
 * ORIGIN of a `Uri.TryCreate(UriKind.Absolute)` value against registered
 * redirect URIs, so anything that does not parse absolute, and anything whose
 * scheme is not http(s), was never allow-listed however it got here. Enforced at
 * the builder as well as the factory because {@link AllowListedReturnUrl} is a
 * structural type: a hand-built object must not reach `location.href`
 * unexamined, which is the difference between this and a `javascript:` XSS.
 */
function assertHandOffUrl(url: string): void {
  let parsed: URL;

  try {
    parsed = new URL(url);
  } catch {
    throw new TypeError(`allow-listed return url is not absolute: ${url}`);
  }

  if (!HAND_OFF_PROTOCOLS.has(parsed.protocol)) {
    throw new TypeError(`allow-listed return url is not http(s): ${url}`);
  }
}

/**
 * Mints the proof that an absolute `returnUrl` cleared the server's allow-list.
 *
 * @param url Absolute return URL that was submitted to the allow-list.
 * @param allowed The server's verdict for THAT url, scoped to the flow's client
 *   -- `auth.validateRedirectUri`'s `{ allowed: true }`, narrowed by the caller.
 *   Passing anything but `true` throws: the parameter exists so authorization is
 *   handed over explicitly rather than assumed by whoever calls the builder.
 * @throws TypeError If `allowed` is not `true`, or `url` is not an absolute
 *   http(s) URL (see {@link assertHandOffUrl}).
 */
export function allowListedReturnUrl(url: string, allowed: boolean): AllowListedReturnUrl {
  if (allowed !== true) {
    throw new TypeError(`return url is not allow-listed: ${url}`);
  }

  assertHandOffUrl(url);

  return { url, allowListed: true };
}

/**
 * Resolves the exchange hand-off's destination, applying the guard that matches
 * what the caller claimed: a bare string must be relative-only; an
 * {@link AllowListedReturnUrl} must be an absolute http(s) URL.
 */
function resolveHandOffReturnUrl(returnUrl: string | AllowListedReturnUrl): string {
  if (typeof returnUrl === "string") {
    assertSafeReturnUrl(returnUrl);
    return returnUrl;
  }

  assertHandOffUrl(returnUrl.url);

  return returnUrl.url;
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
 * @param returnUrl Relative URL to forward to once the cookie is set, or an
 *   {@link AllowListedReturnUrl} for the external-login hand-off, whose returnUrl
 *   is absolute and can only be judged by the server's allow-list.
 * @param clientId Client the flow belongs to, so the endpoint can scope its
 *   returnUrl allow-list check to it. Nullish or blank omits the parameter.
 * @throws TypeError If `ticket` is blank (message: "ticket is required"), a bare
 *   string `returnUrl` fails {@link isSafeReturnUrl} (message: "unsafe return
 *   url"), or an {@link AllowListedReturnUrl} is not an absolute http(s) URL.
 */
export function buildExchangeTicketUrl(
  origin: string,
  ticket: string,
  returnUrl: string | AllowListedReturnUrl,
  clientId?: string,
): string {
  // The oracle only builds this URL inside `if (!IsNullOrEmpty(SignInTicket))`;
  // a ticketless exchange-ticket URL is never a valid navigation target.
  if (ticket.trim() === "") {
    throw new TypeError("ticket is required to build an exchange-ticket url");
  }

  // Guards first: the client id is cargo, not a licence. Appending it before
  // the checks ran would build an attacker's URL for them.
  const target: string = resolveHandOffReturnUrl(returnUrl);

  const base: string =
    `${normalizeOrigin(origin)}/v1/identity/auth/exchange-ticket` +
    `?ticket=${encodeURIComponent(ticket)}` +
    `&returnUrl=${encodeURIComponent(target)}`;

  // A blank id is not a client: the endpoint fails an unknown one CLOSED to the
  // AuthUrl-only origin set, so an empty `clientId=` would refuse the very
  // returnUrl the caller is mid-journey to. Send nothing instead.
  if (clientId === undefined || clientId.trim() === "") {
    return base;
  }

  return `${base}&clientId=${encodeURIComponent(clientId)}`;
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
