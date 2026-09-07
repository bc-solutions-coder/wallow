/**
 * Authenticated /api proxy with session refresh, bounded request replay, and problem responses.
 */
import {
  ApiFailure,
  ClientErrorCode,
  ErrorCode,
  failureFromResponse,
  isApiFailure,
} from "@bc-solutions-coder/api-errors";

import { REQUEST_ID_HEADER, resolveRequestId } from "../request-id";
import type { BffConfig } from "./config";
import { csrfTokenMatches, CSRF_HEADER, isStateChangingMethod } from "./csrf";
import { redact, RefreshFailedError } from "./errors";
import {
  applyForwardedHeaders,
  resolveClientAddress,
  TRUST_NO_PROXIES,
  type PeerRequest,
  type TrustedProxies,
} from "./forwarded";
import {
  clearSession,
  readSession,
  readSessionRef,
  writeSession,
  writeSessionRef,
} from "./handlers";
import { discover, refreshTokens, type DiscoveryDoc, type TokenResponse } from "./oidc";
import { problemResponse, problemTitle } from "./problem";
import type { BffSession } from "./session";
import { CookieSessionStore } from "./store/cookie";
import type { SessionStore } from "./store/types";

/** How long before real expiry a token is treated as expired (ms). */
const EXPIRY_SKEW_MS = 30_000;

/** How long a single upstream forward may take before it is aborted (ms). */
export const FORWARD_TIMEOUT_MS = 30_000;

/** Upper bound honoured for an upstream `Retry-After` header (ms). */
export const MAX_RETRY_AFTER_MS = 5000;

/** Milliseconds in a second, for converting `expires_in` deltas. */
const MS_PER_SECOND = 1000;

/** The wait applied when a `Retry-After` is absent or unparseable (ms). */
const NO_DELAY_MS = 0;

/** The version delta applied to a session on each token rotation. */
const VERSION_STEP = 1;

/** HTTP status the BFF answers with when the session cannot authenticate. */
const UNAUTHORIZED_STATUS = 401;

/** HTTP status raised when the upstream cannot be reached at all. */
const NETWORK_FAILURE_STATUS = 503;

/** HTTP status raised when the upstream forward exceeds {@link FORWARD_TIMEOUT_MS}. */
const GATEWAY_TIMEOUT_STATUS = 504;

/** HTTP status carried by an upstream throttle response. */
const TOO_MANY_REQUESTS_STATUS = 429;

/** Inclusive lower bound of the HTTP redirect status range. */
const REDIRECT_STATUS_MIN = 300;

/** Exclusive upper bound of the HTTP redirect status range. */
const REDIRECT_STATUS_MAX = 400;

/**
 * The login path a .NET cookie-authentication challenge redirects to. A `3xx`
 * pointing here is a rejected bearer wearing a redirect, not a real redirect.
 */
const LOGIN_PATH: string = "/account/login";

/** A request to forward upstream, retryable because the body is materialised. */
export interface ForwardRequest {
  /** Absolute URL of the downstream API endpoint. */
  target: string;
  /** HTTP method to forward. */
  method: string;
  /** Headers to forward, excluding `authorization` (added per attempt). */
  headers: Headers;
  /** Materialised request body, replayable across a retry. */
  body?: BodyInit;
}

/** The outcome of a resilient forward. */
export interface ForwardResult {
  /** The upstream response. */
  response: Response;
  /** The session used for the successful attempt (refreshed when retried). */
  session: BffSession;
  /**
   * The store reference {@link session} lives under. A reactive refresh persists
   * the rotated session inside the refresh lock, and this is the reference that
   * write returned — the caller re-seals it into the cookie rather than writing
   * the session to the store a second time.
   */
  ref: string;
}

/** A session together with the store reference it was persisted under. */
interface StoredSession {
  session: BffSession;
  ref: string;
}

/**
 * An upstream failure that still carries the response it was parsed from.
 *
 * The proxy hands the upstream body back to the browser verbatim, so members
 * the RFC 7807 core does not model — ASP.NET's `errors[]` for a validation
 * failure, `traceId` — survive the trip through the BFF.
 */
class UpstreamError extends ApiFailure {
  /** The upstream response, its body already consumed into {@link bodyText}. */
  readonly response: Response;
  /** The upstream response body, verbatim. */
  readonly bodyText: string;

  constructor(problem: ApiFailure, response: Response, bodyText: string) {
    super({
      status: problem.status,
      code: problem.code,
      title: problem.title,
      detail: problem.detail,
      traceId: problem.traceId,
      requestId: problem.requestId,
      fieldErrors: problem.fieldErrors,
      retryAfter: problem.retryAfter,
    });
    this.name = "UpstreamError";
    this.response = response;
    this.bodyText = bodyText;
  }
}

/**
 * Refresh a session when its access token expires within 30 seconds.
 *
 * Runs refresh through the store lock and persists rotated tokens. A store that declines a
 * contended lock causes an immediate session reread; cookie stores can join the in-flight
 * refresh instead. This helper does not write response cookies or destroy failed sessions.
 *
 * @param session Current session; expiresAt is epoch milliseconds.
 *
 * @param config OIDC client configuration.
 *
 * @param store Store shared with the handlers that issued the session.
 *
 * @param ref Opaque reference read from the session cookie.
 *
 * @returns The refreshed or adopted session, or the unchanged session when the token is still
 * fresh.
 *
 * @throws {RefreshFailedError} When a refresh token is missing, discovery or refresh fails, or
 * the contended session no longer exists. Other store failures propagate.
 */
export async function ensureFreshSession(
  session: BffSession,
  config: BffConfig,
  store: SessionStore,
  ref: string,
): Promise<BffSession> {
  if (session.expiresAt - EXPIRY_SKEW_MS > Date.now()) {
    return session;
  }

  if (session.refreshToken === undefined || session.refreshToken === "") {
    throw new RefreshFailedError("Session expired and no refresh token is available");
  }

  const stored: StoredSession = await refreshUnderLock(session, config, store, ref);
  return stored.session;
}

/**
 * Rotate the session's tokens inside {@link SessionStore.withRefreshLock} and
 * persist the result, so concurrent requests for the same session cannot spend
 * the one-time refresh token in parallel.
 *
 * When the lock is already held by a peer request (`withRefreshLock` resolves to
 * `undefined`), the session the peer stored is adopted instead of refreshing a
 * second time.
 *
 * @returns The refreshed session and the reference it was stored under.
 * @throws When the lock is held but the store no longer has the session.
 */
async function refreshUnderLock(
  session: BffSession,
  config: BffConfig,
  store: SessionStore,
  ref: string,
): Promise<StoredSession> {
  const refreshToken: string = session.refreshToken ?? "";

  const refreshed: StoredSession | undefined = await store.withRefreshLock(
    ref,
    async (): Promise<StoredSession> => {
      // Any refresh failure — a rejected grant (logout elsewhere, user
      // deactivated, token already spent) or even a transient fault reaching
      // the auth host — ends the session. Fail closed: a session the auth
      // host cannot vouch for is not kept alive on the chance the fault was
      // transient; the user signs in again.
      let tokens: TokenResponse;
      try {
        const doc: DiscoveryDoc = await discover(config);
        tokens = await refreshTokens(config, doc, refreshToken);
      } catch (error: unknown) {
        throw new RefreshFailedError(causeDetail(error));
      }

      const next: BffSession = {
        ...session,
        accessToken: tokens.access_token,
        refreshToken: tokens.refresh_token ?? session.refreshToken,
        idToken: tokens.id_token ?? session.idToken,
        expiresAt: Date.now() + tokens.expires_in * MS_PER_SECOND,
        version: session.version + VERSION_STEP,
      };
      const nextRef: string = await store.write(next);
      return { session: next, ref: nextRef };
    },
  );

  if (refreshed !== undefined) {
    return refreshed;
  }

  // The lock was held by a concurrent refresh; adopt whatever it stored. The
  // reference is unchanged: the peer rotated the session behind it, not the
  // cookie that points at it.
  const peer: BffSession | null = await store.read(ref);
  if (peer === null) {
    throw new RefreshFailedError("Session refresh lock was held but the session is gone");
  }
  return { session: peer, ref };
}

/**
 * Refresh a session even when its access token has not expired.
 *
 * Runs refresh through the store lock and persists rotated tokens. A store that declines a
 * contended lock causes an immediate session reread; cookie stores can join the in-flight
 * refresh instead. This helper does not write response cookies or destroy failed sessions.
 *
 * @param session Current session; expiresAt is epoch milliseconds.
 *
 * @param config OIDC client configuration.
 *
 * @param store Store shared with the handlers that issued the session.
 *
 * @param ref Opaque reference read from the session cookie.
 *
 * @returns The refreshed or adopted session.
 *
 * @throws {RefreshFailedError} When a refresh token is missing, discovery or refresh fails, or
 * the contended session no longer exists. Other store failures propagate.
 */
export async function forceRefreshSession(
  session: BffSession,
  config: BffConfig,
  store: SessionStore,
  ref: string,
): Promise<BffSession> {
  const stored: StoredSession = await forceRefreshStored(session, config, store, ref);
  return stored.session;
}

/** {@link forceRefreshSession}, keeping the reference the rotation stored. */
async function forceRefreshStored(
  session: BffSession,
  config: BffConfig,
  store: SessionStore,
  ref: string,
): Promise<StoredSession> {
  if (!hasRefreshToken(session)) {
    throw new RefreshFailedError(
      "The upstream rejected the access token and no refresh token is available",
    );
  }
  return await refreshUnderLock(session, config, store, ref);
}

/**
 * Forward a buffered request with a session bearer and at most one replay.
 *
 * A 401 or redirect to /account/login triggers token refresh when a refresh token exists. A 429
 * instead retries after Retry-After, capped at five seconds. Each attempt has a 30-second
 * timeout; other redirects are returned for the caller to handle.
 *
 * @param request Absolute API target, method, headers, and replayable body. This helper does not
 * restrict the target URL.
 *
 * @param config OIDC client configuration for reactive refresh.
 *
 * @param store Shared session store.
 *
 * @param session Session whose access token authorizes the first attempt.
 *
 * @param ref Opaque reference for the session.
 *
 * @returns Upstream response, resulting session, and its stored reference; propagate a changed
 * reference to the browser cookie.
 *
 * @throws {ApiFailure} For upstream errors, network failures, and timeouts. Refresh or store
 * failures also propagate.
 */
export async function forwardWithResilience(
  request: ForwardRequest,
  config: BffConfig,
  store: SessionStore,
  session: BffSession,
  ref: string,
): Promise<ForwardResult> {
  let current: StoredSession = { session, ref };
  let response: Response = await attemptForward(request, current.session);

  // At most one reactive retry: a rejected token is force-refreshed and the
  // request replayed; a throttled request waits out its `Retry-After` and
  // replays. Both classifications retry exactly once, so this is a single
  // guarded step rather than a loop.
  if (isAuthFailure(response) && hasRefreshToken(current.session)) {
    current = await forceRefreshStored(current.session, config, store, current.ref);
    response = await attemptForward(request, current.session);
  } else if (response.status === TOO_MANY_REQUESTS_STATUS) {
    await delay(retryAfterMs(response));
    response = await attemptForward(request, current.session);
  }

  if (response.ok) {
    return { response, session: current.session, ref: current.ref };
  }

  // Any non-auth redirect is the API's own business (a 3xx to a resource, a
  // 304): hand it back untouched rather than treating it as a failure.
  if (isRedirect(response) && !isAuthFailure(response)) {
    return { response, session: current.session, ref: current.ref };
  }

  if (isAuthFailure(response)) {
    throw await authFailureError(request, response);
  }

  throw await upstreamError(request, response);
}

/**
 * Run one forward attempt: a `redirect: "manual"` fetch carrying the session's
 * bearer, aborted after {@link FORWARD_TIMEOUT_MS}.
 *
 * @throws {ApiFailure} `504 Transport.Timeout` when the abort fired, and
 *   `503 Transport.NetworkError` for any other transport failure. The
 *   failure's `detail` is the transport's own message, scrubbed, for the log
 *   record only — {@link respondToFailure} never writes it into a body.
 */
async function attemptForward(request: ForwardRequest, session: BffSession): Promise<Response> {
  const controller: AbortController = new AbortController();
  const timeout: ReturnType<typeof setTimeout> = setTimeout((): void => {
    controller.abort();
  }, FORWARD_TIMEOUT_MS);

  // A fresh Headers per attempt: the retry carries the rotated bearer, and the
  // caller's Headers are never mutated.
  const headers: Headers = new Headers(request.headers);
  headers.set("authorization", `Bearer ${session.accessToken}`);

  try {
    return await fetch(request.target, {
      method: request.method,
      headers,
      body: request.body,
      redirect: "manual",
      signal: controller.signal,
    });
  } catch (error: unknown) {
    const timedOut: boolean = controller.signal.aborted;
    const code: string = timedOut
      ? ClientErrorCode.TRANSPORT_TIMEOUT
      : ClientErrorCode.TRANSPORT_NETWORK_ERROR;
    const fault: ApiFailure = new ApiFailure({
      status: timedOut ? GATEWAY_TIMEOUT_STATUS : NETWORK_FAILURE_STATUS,
      code,
      title: problemTitle(code),
      detail: causeDetail(error),
    });
    logFault(request, fault);
    throw fault;
  } finally {
    clearTimeout(timeout);
  }
}

/**
 * Whether the response says the access token was rejected: a `401`, or the
 * cookie-authentication redirect to the login page the .NET API answers with
 * when no bearer was accepted (visible only because the forward opted out of
 * following redirects).
 */
function isAuthFailure(response: Response): boolean {
  if (response.status === UNAUTHORIZED_STATUS) {
    return true;
  }
  if (!isRedirect(response)) {
    return false;
  }
  const location: string = response.headers.get("location") ?? "";
  return location.toLowerCase().includes(LOGIN_PATH);
}

function isRedirect(response: Response): boolean {
  return response.status >= REDIRECT_STATUS_MIN && response.status < REDIRECT_STATUS_MAX;
}

function hasRefreshToken(session: BffSession): boolean {
  return session.refreshToken !== undefined && session.refreshToken !== "";
}

/**
 * The error for an authentication failure the refresh could not recover from.
 *
 * A login redirect is an authentication failure whatever status it wears, so it
 * surfaces as a `401` rather than as the `302` the API sent — and it is not
 * handed back to the browser, which would only follow it to a login page it has
 * no business seeing through the tunnel.
 */
async function authFailureError(request: ForwardRequest, response: Response): Promise<ApiFailure> {
  if (response.status === UNAUTHORIZED_STATUS) {
    return await upstreamError(request, response);
  }

  const error: ApiFailure = new ApiFailure({
    status: UNAUTHORIZED_STATUS,
    code: ErrorCode.AUTH_UNAUTHENTICATED,
    title: problemTitle(ErrorCode.AUTH_UNAUTHENTICATED),
    detail: "The upstream API redirected the request to its login page.",
  });
  logFault(request, error);
  return error;
}

/** The error for a non-OK response, carrying the upstream body for the caller. */
async function upstreamError(request: ForwardRequest, response: Response): Promise<UpstreamError> {
  const bodyText: string = await response.text();
  const error: UpstreamError = new UpstreamError(
    failureFromResponse(response, bodyText),
    response,
    bodyText,
  );
  logFault(request, error);
  return error;
}

/**
 * How long to wait before replaying a throttled request, from the upstream
 * `Retry-After` header (delta-seconds or an HTTP date), bounded by
 * {@link MAX_RETRY_AFTER_MS} so an hour-long back-off cannot park the request
 * for an hour. An absent or unparseable header means replay immediately.
 */
function retryAfterMs(response: Response): number {
  const header: string | null = response.headers.get("retry-after");
  if (header === null || header.trim() === "") {
    return NO_DELAY_MS;
  }

  const seconds: number = Number(header);
  if (Number.isFinite(seconds)) {
    return boundWait(seconds * MS_PER_SECOND);
  }

  const until: number = Date.parse(header);
  return Number.isNaN(until) ? NO_DELAY_MS : boundWait(until - Date.now());
}

function boundWait(ms: number): number {
  return Math.min(Math.max(ms, NO_DELAY_MS), MAX_RETRY_AFTER_MS);
}

function delay(ms: number): Promise<void> {
  return new Promise<void>((resolve): void => {
    setTimeout(resolve, ms);
  });
}

/** The message of a thrown transport failure, scrubbed of anything credential-shaped. */
function causeDetail(cause: unknown): string | undefined {
  if (!(cause instanceof Error)) {
    return undefined;
  }
  const scrubbed: unknown = redact(cause.message);
  return typeof scrubbed === "string" ? scrubbed : undefined;
}

/** Report a failed forward without spilling the bearer into the log. */
function logFault(request: ForwardRequest, error: ApiFailure): void {
  const headers: Record<string, string> = Object.fromEntries(request.headers.entries());
  console.warn(
    "wallow-bff: forward failed",
    redact({
      target: request.target,
      method: request.method,
      headers,
      status: error.status,
      code: error.code,
      title: error.title,
      detail: error.detail,
    }),
  );
}

/**
 * The `/api` reverse proxy: a web-standard request in, a response out. The
 * request may carry the peer address srvx exposes on `ip`; that, and never a
 * header, is where the forwarded client address comes from.
 */
export type ApiProxyHandler = (request: PeerRequest) => Promise<Response>;

/** HTTP status the BFF answers with when the CSRF check rejects a request. */
const FORBIDDEN_STATUS = 403;

/** HTTP status for a path this proxy does not serve. */
const NOT_FOUND_STATUS = 404;

/** The only path prefix this proxy forwards. */
const API_PREFIX: string = "/api";

/** The path a request for the bare `/api` root forwards to. */
const ROOT_PATH: string = "/";

/** The dot-segment that would climb out of the API base path. */
const PARENT_SEGMENT: string = "..";

/** Byte length at which a buffered request body is no body at all. */
const EMPTY_BODY_LENGTH = 0;

/**
 * Request header allowlist. The proxy adds authorization per attempt and rewrites forwarded
 * headers with applyForwardedHeaders. Incoming BFF cookies remain at this hop.
 */
const FORWARDED_REQUEST_HEADERS: readonly string[] = [
  "content-type",
  "accept",
  "x-forwarded-for",
  "x-forwarded-proto",
  "x-forwarded-host",
];

/**
 * Response headers never re-emitted on the re-framed response.
 *
 * `transfer-encoding` and `content-encoding` describe a framing this proxy does
 * not reproduce, and `content-length` describes a body whose encoding has
 * already been undone by the time it reaches here — a length that no longer
 * matches the bytes on the wire is a request-smuggling primitive, so the length
 * is recomputed by the host rather than relayed.
 */
const HOP_BY_HOP_HEADERS: ReadonlySet<string> = new Set<string>([
  "transfer-encoding",
  "content-encoding",
  "content-length",
]);

/**
 * The upstream path for an incoming request path, or `null` when this proxy
 * does not serve it.
 *
 * The prefix test is a segment-boundary test: a bare `startsWith("/api")` also
 * accepts `/apiary`. Mounting is the host's business under a router, but the
 * handler is also called directly, and an unchecked path turns it into an open
 * bearer-attaching relay for anything a caller invents.
 */
function strippedApiPath(pathname: string): string | null {
  if (pathname !== API_PREFIX && !pathname.startsWith(`${API_PREFIX}/`)) {
    return null;
  }

  // Empty path segments are collapsed: a leading `//` makes a URL parser read
  // the next segment as an AUTHORITY, and `\` is normalised to `/` before this
  // ever runs, so `/api/\/evil.test/x` arrives here as `//evil.test/x`.
  const stripped: string = pathname.slice(API_PREFIX.length).replaceAll(/\/{2,}/gu, ROOT_PATH);
  if (stripped === "") {
    return ROOT_PATH;
  }
  return stripped.split(ROOT_PATH).includes(PARENT_SEGMENT) ? null : stripped;
}

/**
 * Join the stripped path onto the API base path without relative URL resolution. Reject a target
 * outside the configured origin or base path before attaching credentials.
 */
function upstreamTarget(config: BffConfig, path: string, search: string): string | null {
  const base: URL = new URL(config.apiBaseUrl);
  const basePath: string = base.pathname.replace(/\/$/u, "");
  const target: URL = new URL(`${base.origin}${basePath}${path}${search}`);

  if (target.origin !== base.origin || !target.pathname.startsWith(basePath)) {
    return null;
  }
  return target.toString();
}

/** Whether a refresh rotated the session out from under `before`. */
function changed(before: BffSession, after: BffSession): boolean {
  return after.version !== before.version || after.accessToken !== before.accessToken;
}

/** Copy every `Set-Cookie` from `source` onto `target` without overwriting. */
function mergeCookies(target: Headers, source: Headers): void {
  for (const cookie of source.getSetCookie()) {
    target.append("set-cookie", cookie);
  }
}

/** Upstream response headers safe to re-emit on a re-framed response. */
function forwardableHeaders(headers: Headers): Headers {
  const forwardable: Headers = new Headers();
  headers.forEach((value: string, key: string): void => {
    const lower: string = key.toLowerCase();
    // `Set-Cookie` is re-emitted below: it is the one header that may legally
    // repeat, and copying it here would either collapse the duplicates into one
    // comma-joined line or drop all but the last.
    if (HOP_BY_HOP_HEADERS.has(lower) || lower === "set-cookie") {
      return;
    }
    forwardable.append(key, value);
  });
  for (const cookie of headers.getSetCookie()) {
    forwardable.append("set-cookie", cookie);
  }
  return forwardable;
}

/**
 * End a session whose refresh failed terminally.
 *
 * The failure means the grant behind the session is gone (revoked at the auth
 * host, or spent with nothing left to spend), so the session is ended exactly
 * the way a logout ends it: the store record is destroyed and the session
 * cookies — the base cookie, its CSRF companion, and any chunks the request
 * presented — are cleared, before the 401 problem details go back. Leaving
 * either in place would replay the same doomed refresh on every request.
 */
async function tearDownDeadSession(
  error: RefreshFailedError,
  request: Request,
  config: BffConfig,
  store: SessionStore,
  ref: string,
  cookies: Headers,
  requestId: string,
): Promise<Response> {
  await store.destroy(ref);
  clearSession(cookies, request, config);
  return respondToFailure(error, cookies, requestId);
}

/**
 * Relay upstream status, headers, and body while retaining rotated session cookies. Locally
 * originated problems include requestId and fixed public messages; transport details remain in
 * logs.
 */
function respondToFailure(error: unknown, cookies: Headers, requestId: string): Response {
  if (error instanceof UpstreamError) {
    const headers: Headers = forwardableHeaders(error.response.headers);
    mergeCookies(headers, cookies);
    return new Response(error.bodyText, { status: error.response.status, headers });
  }

  if (isApiFailure(error)) {
    return problemResponse(error.status, error.code, { requestId, headers: cookies });
  }

  throw error;
}

/**
 * The answer for a refresh the proxy could not even attempt — the store
 * faulting, a lock that could not be taken. That is not a verdict on the
 * grant, so the session is kept for the next request to try again; only the
 * answer is the same 401 a terminal refresh failure gets.
 */
function refreshFaultResponse(cookies: Headers, requestId: string): Response {
  return problemResponse(UNAUTHORIZED_STATUS, ClientErrorCode.BFF_SESSION_REFRESH_FAILED, {
    requestId,
    headers: cookies,
  });
}

/** Report a session the proxy could not keep fresh, without the tokens. */
function logSessionFault(requestId: string, error: unknown): void {
  const detail: string | undefined = isApiFailure(error) ? error.detail : causeDetail(error);
  console.warn("wallow-bff: session refresh failed", redact({ requestId, detail }));
}

/**
 * Create an authenticated same-origin /api proxy.
 *
 * Requires the BFF session cookie and a matching x-csrf-token for POST, PUT, PATCH, and DELETE.
 * Removes /api from the path, appends the remaining path to config.apiBaseUrl, and attaches the
 * session bearer. Refreshes expiring tokens and propagates rotated session cookies; terminal
 * refresh failures destroy the session.
 *
 * @param config API target, OIDC client, and cookie settings.
 *
 * @param store Store shared with createBffHandlers; defaults to CookieSessionStore.
 *
 * @param trustedProxies Ranges used to resolve request.ip and forwarded client addresses;
 * defaults to no trusted proxies.
 *
 * @returns A web-standard handler that relays upstream responses and emits problem responses for
 * proxy failures.
 */
export function createApiProxy(
  config: BffConfig,
  store: SessionStore = new CookieSessionStore({
    password: config.cookiePasswords ?? config.cookiePassword,
    ttlSeconds: config.sessionTtlSeconds,
  }),
  trustedProxies: TrustedProxies = TRUST_NO_PROXIES,
): ApiProxyHandler {
  return async (request: PeerRequest): Promise<Response> => {
    // Minted before anything else and stamped onto whatever comes back, so the
    // failures the proxy answers ITSELF — a rejected path, an unauthenticated
    // session, a failed CSRF check — are correlatable too. Those are exactly the
    // ones a user cannot otherwise describe, since they never reach the API and
    // so appear in no backend trace at all.
    const requestId: string = resolveRequestId(request.headers);
    const response: Response = await proxyRequest(
      request,
      requestId,
      config,
      store,
      trustedProxies,
    );
    response.headers.set(REQUEST_ID_HEADER, requestId);
    return response;
  };
}

/**
 * The proxy's actual work, for one already-correlated request.
 *
 * Separated from {@link createApiProxy} so the request id is stamped at a single
 * exit rather than at each of the seven responses below — a response that
 * escaped without it would be a request the browser cannot name.
 */
async function proxyRequest(
  request: PeerRequest,
  requestId: string,
  config: BffConfig,
  store: SessionStore,
  trustedProxies: TrustedProxies,
): Promise<Response> {
  const url: URL = new URL(request.url);

  // The allowlist runs before anything else: a path this proxy does not serve
  // must not even cost a session read.
  const path: string | null = strippedApiPath(url.pathname);
  if (path === null) {
    return problemResponse(NOT_FOUND_STATUS, ErrorCode.HTTP_NOT_FOUND, { requestId });
  }

  const ref: string | null = readSessionRef(request, config);
  if (ref === null) {
    return problemResponse(UNAUTHORIZED_STATUS, ClientErrorCode.BFF_SESSION_MISSING, {
      requestId,
    });
  }

  const session: BffSession | null = await readSession(request, config, store);
  if (session === null) {
    return problemResponse(UNAUTHORIZED_STATUS, ClientErrorCode.BFF_SESSION_MISSING, {
      requestId,
    });
  }

  // Cookies the BFF writes for itself during this request. They are collected
  // apart from the upstream response's own headers so that both survive onto
  // whichever response is finally returned.
  const cookies: Headers = new Headers();

  // Gate state-changing requests on the session-bound CSRF token before the
  // session is refreshed or anything is forwarded: a rejected request must
  // die here, never reaching the downstream API. `csrfToken` survives a
  // refresh untouched, so the pre-refresh session is the right thing to
  // compare against. `Headers.get` answers `null` where h3 answered
  // `undefined`; coerce, or the comparison is handed a value its types deny.
  if (isStateChangingMethod(request.method)) {
    const presented: string | undefined = request.headers.get(CSRF_HEADER) ?? undefined;
    if (!csrfTokenMatches(session.csrfToken, presented)) {
      return problemResponse(FORBIDDEN_STATUS, ClientErrorCode.BFF_CSRF_INVALID, {
        requestId,
        headers: cookies,
      });
    }
  }

  let fresh: BffSession;
  let currentRef: string = ref;
  try {
    fresh = await ensureFreshSession(session, config, store, ref);
  } catch (error: unknown) {
    logSessionFault(requestId, error);
    if (error instanceof RefreshFailedError) {
      return await tearDownDeadSession(error, request, config, store, ref, cookies, requestId);
    }
    return refreshFaultResponse(cookies, requestId);
  }

  // Re-seal the cookie only when the session actually changed.
  if (changed(session, fresh)) {
    currentRef = await writeSession(cookies, config, store, fresh);
  }

  const target: string | null = upstreamTarget(config, path, url.search);
  if (target === null) {
    return problemResponse(NOT_FOUND_STATUS, ErrorCode.HTTP_NOT_FOUND, {
      requestId,
      headers: cookies,
    });
  }

  const method: string = request.method.toUpperCase();
  const forwardHeaders: Headers = new Headers();
  for (const name of FORWARDED_REQUEST_HEADERS) {
    const value: string | null = request.headers.get(name);
    if (value !== null) {
      forwardHeaders.set(name, value);
    }
  }

  // The API rate-limits on the forwarded chain, so the caller's address has to
  // survive this hop: without it every user of this app reaches the API wearing
  // the BFF's own address and is limited as one client (M6). It is resolved
  // from the socket peer and the trusted-proxy list, never from a header the
  // caller could have written.
  applyForwardedHeaders(
    forwardHeaders,
    url,
    resolveClientAddress(request, request.ip, trustedProxies),
  );

  // Set on the headers `forwardWithResilience` replays from, not per attempt, so
  // a reactive-401 replay reaches the API under the id the first attempt used:
  // one logical request is one trace, not two.
  forwardHeaders.set(REQUEST_ID_HEADER, requestId);

  // BUFFERED, not streamed: `forwardWithResilience` replays the request after
  // a reactive 401, and a `ReadableStream` body is consumed by the first
  // attempt. Streaming stays a response-direction optimisation only.
  let body: BodyInit | undefined;
  if (method !== "GET" && method !== "HEAD") {
    const buffered: Uint8Array<ArrayBuffer> = new Uint8Array(await request.arrayBuffer());
    body = buffered.byteLength > EMPTY_BODY_LENGTH ? buffered : undefined;
  }

  let result: ForwardResult;
  try {
    result = await forwardWithResilience(
      { target, method, headers: forwardHeaders, body },
      config,
      store,
      fresh,
      currentRef,
    );
  } catch (error: unknown) {
    // A reactive refresh that failed is the same dead session the proactive
    // path tears down — the API rejected the token AND the grant behind it is
    // gone — so it gets the same teardown, under the reference the session
    // currently lives at.
    if (error instanceof RefreshFailedError) {
      logSessionFault(requestId, error);
      return await tearDownDeadSession(
        error,
        request,
        config,
        store,
        currentRef,
        cookies,
        requestId,
      );
    }
    if (!isApiFailure(error)) {
      // The forward's own faults are all ApiFailures; anything else escaped
      // from the store during the reactive refresh — the same fault the
      // proactive path answers above, and it gets the same answer.
      logSessionFault(requestId, error);
      return refreshFaultResponse(cookies, requestId);
    }
    return respondToFailure(error, cookies, requestId);
  }

  // A reactive refresh rotated the session mid-request: the browser needs the
  // new reference, or its next request arrives with a spent refresh token.
  if (changed(fresh, result.session)) {
    writeSessionRef(cookies, config, result.ref);
  }

  const upstream: Response = result.response;
  const headers: Headers = forwardableHeaders(upstream.headers);
  mergeCookies(headers, cookies);
  return new Response(upstream.body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers,
  });
}
