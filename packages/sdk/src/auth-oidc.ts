/** URL and consent-form builders for authentication UIs. These helpers do not make requests. */

/**
 * Strips a trailing '/' so `{origin}/path` never doubles the separator.
 * `ApiBaseUrl` comes from config and may be written either way.
 */
function normalizeOrigin(origin: string): string {
  return origin.replace(/\/+$/u, "");
}

/**
 * Reject unsafe local return URLs with TypeError so callers never receive a silently substituted
 * destination.
 */
function assertSafeReturnUrl(returnUrl: string): void {
  if (!isSafeReturnUrl(returnUrl)) {
    throw new TypeError(`unsafe return url: ${returnUrl}`);
  }
}

/**
 * Check whether a return URL is a local absolute path such as `/settings`.
 *
 * Rejects empty values and protocol-relative URLs. Browser-ignored tabs and
 * newlines, backslashes, and encoded backslashes are normalized for the check.
 * Use the server's redirect-URI validation for absolute external URLs.
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
 * Build an identity-provider `/connect/authorize` URL from OIDC parameters.
 *
 * Encodes the supplied parameters without generating PKCE, state, or nonce.
 * This is a URL builder for callers managing the authorization flow; navigate
 * the browser to the result to start it.
 */
export function buildConnectAuthorizeUrl(origin: string, params: Record<string, string>): string {
  const url: string = `${normalizeOrigin(origin)}/connect/authorize`;
  // Authorization request params are form-encoded (space => '+'), unlike the
  // Uri.EscapeDataString call sites below. No oracle constrains this one.
  const query: string = new URLSearchParams(params).toString();

  return query === "" ? url : `${url}?${query}`;
}

/** The form field carrying the single-use token the consent screen was issued. */
export const CONSENT_TOKEN_FIELD = "consent_token";

/** The form field carrying the user's answer: {@link CONSENT_GRANTED} or {@link CONSENT_DENIED}. */
export const CONSENT_DECISION_FIELD = "consent_decision";

/** The {@link CONSENT_DECISION_FIELD} value that approves the request. */
export const CONSENT_GRANTED = "granted";

/** The {@link CONSENT_DECISION_FIELD} value that refuses it. */
export const CONSENT_DENIED = "denied";

/** A consent decision as the form that delivers it: where it posts and what it carries. */
export interface ConsentSubmission {
  /** The absolute URL the form posts to — the authorize endpoint on `origin`. */
  readonly action: string;
  /**
   * The hidden fields, in order: the authorize request's own parameters (a
   * repeated one stays repeated) followed by the consent token. The decision is
   * not among them — it is the submit button's own name and value.
   */
  readonly fields: readonly (readonly [name: string, value: string])[];
}

/**
 * Build the form action and fields for submitting an authorization consent decision.
 *
 * Copies the return URL's query parameters and adds `consent_token` when
 * provided. The caller must add `consent_decision` (`granted` or `denied`) and
 * submit the fields as a browser POST. A nullish return URL defaults to `/`.
 *
 * @throws TypeError when the return URL is not a safe local path.
 */
export function buildConsentSubmission(
  origin: string,
  returnUrl: string | null | undefined,
  consentToken: string | undefined,
): ConsentSubmission {
  // Only nullish falls back. A PRESENT value -- including the empty string --
  // is a caller-supplied return URL and must clear the guard.
  let target: string = "/";
  if (returnUrl !== null && returnUrl !== undefined) {
    assertSafeReturnUrl(returnUrl);
    target = returnUrl;
  }

  // Split at the first '?': everything before is the path the form posts to,
  // everything after is the request the fields carry.
  const [path, ...rest]: string[] = target.split("?");
  const query: string = rest.join("?");

  const fields: (readonly [string, string])[] = [...new URLSearchParams(query)];
  if (consentToken !== undefined) {
    fields.push([CONSENT_TOKEN_FIELD, consentToken]);
  }

  return { action: `${normalizeOrigin(origin)}${path}`, fields };
}

/**
 * An absolute HTTP(S) return URL approved by the server for the current client. Create it with
 * allowListedReturnUrl after validating the destination with the API; bare strings remain
 * restricted to local paths.
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
 * Require an absolute HTTP(S) destination even for manually constructed AllowListedReturnUrl
 * objects. This checks URL shape; server approval remains the caller’s responsibility.
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
 * Wrap an absolute HTTP(S) return URL after the server has approved it.
 *
 * Pass the verdict from `accountValidateRedirectUri` as `allowed`. This helper
 * checks that the verdict is `true` and the URL uses HTTP(S); it makes no request.
 * The result can be passed to `buildExchangeTicketUrl`.
 *
 * @throws TypeError when approval is false or the URL is invalid.
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
 * Build the browser URL that exchanges a sign-in ticket for an identity-provider cookie.
 *
 * Navigate to the result to consume the ticket. `returnUrl` must be a safe
 * local path or an absolute URL wrapped by `allowListedReturnUrl`. An omitted
 * or whitespace-only `clientId` is excluded from the query string.
 *
 * @throws TypeError when the ticket is blank or the return URL is unsafe.
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
 * Build an identity-provider `/connect/logout` URL from optional logout parameters.
 *
 * Null, undefined, and empty parameter values are omitted. This only builds
 * the URL; it does not clear the application's BFF session. Use `logout()`
 * for the browser BFF logout flow.
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
