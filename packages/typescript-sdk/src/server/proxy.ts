/**
 * The `/api` reverse proxy for the BFF tunnel with silent token refresh.
 *
 * `ensureFreshSession` is a pure helper that transparently refreshes the OIDC
 * access token when it is within {@link EXPIRY_SKEW_MS} of expiry.
 * `createApiProxy` is the h3 handler that reads the session, ensures it is
 * fresh, strips the `/api` prefix, and forwards the request to the downstream
 * API with a `Bearer` token.
 */
import {
  defineEventHandler,
  getMethod,
  getRequestHeaders,
  getRequestPath,
  readRawBody,
  setResponseHeaders,
  setResponseStatus,
  type EventHandler,
  type H3Event,
} from "h3";

import type { BffConfig } from "./config";
import { parseProblemDetails, redact, WallowError } from "./errors";
import {
  discover,
  refreshTokens,
  type DiscoveryDoc,
  type TokenResponse,
} from "./oidc";
import type { BffSession } from "./session";
import {
  readSession,
  readSessionRef,
  writeSession,
  writeSessionRef,
} from "./handlers";
import { CookieSessionStore } from "./store/cookie";
import type { SessionStore } from "./store/types";

/** How long before real expiry a token is treated as expired (ms). */
export const EXPIRY_SKEW_MS = 30_000;

/** How long a single upstream forward may take before it is aborted (ms). */
export const FORWARD_TIMEOUT_MS = 30_000;

/** Upper bound honoured for an upstream `Retry-After` header (ms). */
export const MAX_RETRY_AFTER_MS = 5_000;

/** Code carried by the {@link WallowError} raised for a transport failure. */
export const NETWORK_ERROR_CODE = "NETWORK_ERROR";

/** Code carried by the {@link WallowError} raised when the forward times out. */
export const NETWORK_TIMEOUT_CODE = "NETWORK_TIMEOUT";

/** Code carried by the {@link WallowError} raised for an unrecoverable 401. */
const UNAUTHORIZED_CODE: string = "UNAUTHORIZED";

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
class UpstreamError extends WallowError {
  /** The upstream response, its body already consumed into {@link bodyText}. */
  readonly response: Response;
  /** The upstream response body, verbatim. */
  readonly bodyText: string;

  constructor(problem: WallowError, response: Response, bodyText: string) {
    super({
      status: problem.status,
      code: problem.code,
      title: problem.title,
      detail: problem.detail,
    });
    this.response = response;
    this.bodyText = bodyText;
  }
}

/**
 * Ensure the session's access token is fresh, refreshing it when it is within
 * {@link EXPIRY_SKEW_MS} of expiry.
 *
 * The refresh runs inside {@link SessionStore.withRefreshLock} so concurrent
 * requests for the same session cannot rotate the refresh token in parallel.
 * When the lock is already held by a peer request (`withRefreshLock` resolves
 * to `undefined`), the freshly-refreshed session is re-read from the store
 * instead of refreshing a second time.
 *
 * @param session The current session.
 * @param config BFF configuration.
 * @param store The session store used to lock, persist, and re-read sessions.
 * @param ref The opaque store reference for this session.
 * @returns The (possibly refreshed) session.
 * @throws When the token is expired and no refresh token is available, or when
 *   the lock is held but the store no longer has the session.
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
    throw new Error("Session expired and no refresh token is available");
  }

  const stored: StoredSession = await refreshUnderLock(
    session,
    config,
    store,
    ref,
  );
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
      const doc: DiscoveryDoc = await discover(config);
      const tokens: TokenResponse = await refreshTokens(
        config,
        doc,
        refreshToken,
      );

      const next: BffSession = {
        ...session,
        accessToken: tokens.access_token,
        refreshToken: tokens.refresh_token ?? session.refreshToken,
        idToken: tokens.id_token ?? session.idToken,
        expiresAt: Date.now() + tokens.expires_in * 1000,
        version: session.version + 1,
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
    throw new Error("Session refresh lock was held but the session is gone");
  }
  return { session: peer, ref };
}

/**
 * Refresh the session's tokens unconditionally, whatever the local expiry says.
 *
 * Used for the reactive path: the upstream API rejected a token the BFF still
 * believed was fresh (revoked, rotated out of band, or clock skew), so
 * {@link ensureFreshSession} would be a no-op. Runs inside
 * {@link SessionStore.withRefreshLock} and persists the rotated session exactly
 * like {@link ensureFreshSession} does.
 *
 * @param session The current session.
 * @param config BFF configuration.
 * @param store The session store used to lock, persist, and re-read sessions.
 * @param ref The opaque store reference for this session.
 * @returns The refreshed session.
 * @throws When no refresh token is available, or when the lock is held but the
 *   store no longer has the session.
 */
export async function forceRefreshSession(
  session: BffSession,
  config: BffConfig,
  store: SessionStore,
  ref: string,
): Promise<BffSession> {
  const stored: StoredSession = await forceRefreshStored(
    session,
    config,
    store,
    ref,
  );
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
    throw new Error(
      "The upstream rejected the access token and no refresh token is available",
    );
  }
  return refreshUnderLock(session, config, store, ref);
}

/**
 * Forward a request to the downstream API with the Appendix B resilience
 * behaviours.
 *
 * Each attempt runs with `redirect: "manual"` (so an auth cookie redirect to
 * the login page is observable rather than silently followed) under an
 * {@link AbortController} bounded by {@link FORWARD_TIMEOUT_MS}.
 *
 * Reactive classification, each retried at most once:
 * - `401` — the token was rejected: force a refresh and replay the request.
 * - `3xx` whose `Location` points at the login page: the same auth failure in
 *   redirect clothing; force a refresh and replay the request.
 * - `429` — wait for `Retry-After` (bounded by {@link MAX_RETRY_AFTER_MS}) and
 *   replay the request.
 *
 * @param request The materialised request to forward.
 * @param config BFF configuration.
 * @param store The session store, used to refresh under lock.
 * @param session The session whose access token authorises the forward.
 * @param ref The opaque store reference for this session.
 * @returns The upstream response plus the session it was made with.
 * @throws {WallowError} With the upstream status and parsed RFC 7807 details
 *   for a non-OK response, `503 NETWORK_ERROR` for a transport failure, and
 *   `503 NETWORK_TIMEOUT` when the attempt exceeds {@link FORWARD_TIMEOUT_MS}.
 */
export async function forwardWithResilience(
  request: ForwardRequest,
  config: BffConfig,
  store: SessionStore,
  session: BffSession,
  ref: string,
): Promise<ForwardResult> {
  let current: StoredSession = { session, ref };
  let retried: boolean = false;

  for (;;) {
    const response: Response = await attemptForward(request, current.session);

    if (response.ok) {
      return { response, session: current.session, ref: current.ref };
    }

    if (isAuthFailure(response)) {
      if (retried || !hasRefreshToken(current.session)) {
        throw await authFailureError(request, response);
      }
      retried = true;
      current = await forceRefreshStored(
        current.session,
        config,
        store,
        current.ref,
      );
      continue;
    }

    // Any other redirect is the API's own business (a 3xx to a resource, a 304):
    // hand it back untouched rather than treating it as a failure.
    if (isRedirect(response)) {
      return { response, session: current.session, ref: current.ref };
    }

    if (response.status === 429 && !retried) {
      retried = true;
      await delay(retryAfterMs(response));
      continue;
    }

    throw await upstreamError(request, response);
  }
}

/**
 * Run one forward attempt: a `redirect: "manual"` fetch carrying the session's
 * bearer, aborted after {@link FORWARD_TIMEOUT_MS}.
 *
 * @throws {WallowError} `503 NETWORK_TIMEOUT` when the abort fired, and
 *   `503 NETWORK_ERROR` for any other transport failure.
 */
async function attemptForward(
  request: ForwardRequest,
  session: BffSession,
): Promise<Response> {
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
  } catch (cause: unknown) {
    const timedOut: boolean = controller.signal.aborted;
    const error: WallowError = new WallowError({
      status: 503,
      code: timedOut ? NETWORK_TIMEOUT_CODE : NETWORK_ERROR_CODE,
      title: timedOut
        ? "The upstream request timed out"
        : "The upstream request failed",
      detail: causeDetail(cause),
    });
    logFault(request, error);
    throw error;
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
  if (response.status === 401) {
    return true;
  }
  if (!isRedirect(response)) {
    return false;
  }
  const location: string = response.headers.get("location") ?? "";
  return location.toLowerCase().includes(LOGIN_PATH);
}

function isRedirect(response: Response): boolean {
  return response.status >= 300 && response.status < 400;
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
async function authFailureError(
  request: ForwardRequest,
  response: Response,
): Promise<WallowError> {
  if (response.status === 401) {
    return upstreamError(request, response);
  }

  const error: WallowError = new WallowError({
    status: 401,
    code: UNAUTHORIZED_CODE,
    title: "Unauthorized",
    detail: "The upstream API redirected the request to its login page.",
  });
  logFault(request, error);
  return error;
}

/** The error for a non-OK response, carrying the upstream body for the caller. */
async function upstreamError(
  request: ForwardRequest,
  response: Response,
): Promise<UpstreamError> {
  const bodyText: string = await response.text();
  const error: UpstreamError = new UpstreamError(
    parseProblemDetails(response, bodyText),
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
    return 0;
  }

  const seconds: number = Number(header);
  if (Number.isFinite(seconds)) {
    return boundWait(seconds * 1000);
  }

  const until: number = Date.parse(header);
  return Number.isNaN(until) ? 0 : boundWait(until - Date.now());
}

function boundWait(ms: number): number {
  return Math.min(Math.max(ms, 0), MAX_RETRY_AFTER_MS);
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
function logFault(request: ForwardRequest, error: WallowError): void {
  console.warn(
    "wallow-bff: forward failed",
    redact({
      target: request.target,
      method: request.method,
      headers: Object.fromEntries(request.headers.entries()),
      status: error.status,
      code: error.code,
      title: error.title,
      detail: error.detail,
    }),
  );
}

/**
 * Build the `/api` reverse-proxy h3 handler bound to a configuration.
 *
 * @param config Server-side BFF configuration.
 * @param store Session store used to resolve and persist sessions. Defaults to
 *   a cookie-only {@link CookieSessionStore}, so single-argument callers keep
 *   working.
 * @returns An h3 event handler that proxies to `config.apiBaseUrl`.
 */
export function createApiProxy(
  config: BffConfig,
  store: SessionStore = new CookieSessionStore({
    password: config.cookiePassword,
  }),
): EventHandler {
  return defineEventHandler(async (event: H3Event): Promise<unknown> => {
    const ref: string | null = readSessionRef(event, config);
    if (ref === null) {
      setResponseStatus(event, 401);
      return null;
    }

    const session: BffSession | null = await readSession(event, config, store);
    if (session === null) {
      setResponseStatus(event, 401);
      return null;
    }

    let fresh: BffSession;
    let currentRef: string = ref;
    try {
      fresh = await ensureFreshSession(session, config, store, ref);
    } catch {
      setResponseStatus(event, 401);
      return null;
    }

    // Re-seal the cookie only when the session actually changed.
    if (changed(session, fresh)) {
      currentRef = await writeSession(event, config, store, fresh);
    }

    // Strip the `/api` prefix and re-root at the downstream API base URL.
    const requestPath: string = getRequestPath(event);
    const stripped: string = requestPath.replace(/^\/api/, "") || "/";
    const target: string = new URL(stripped, config.apiBaseUrl).toString();

    const incoming: Record<string, string | undefined> =
      getRequestHeaders(event);
    const method: string = getMethod(event);

    const forwardHeaders: Headers = new Headers();
    if (incoming["content-type"] !== undefined) {
      forwardHeaders.set("content-type", incoming["content-type"]);
    }
    if (incoming["accept"] !== undefined) {
      forwardHeaders.set("accept", incoming["accept"]);
    }

    const hasBody: boolean = method !== "GET" && method !== "HEAD";
    const body: BodyInit | undefined = hasBody
      ? ((await readRawBody(event, false)) as BodyInit | undefined)
      : undefined;

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
      return respondToFailure(event, error);
    }

    // A reactive refresh rotated the session mid-request: the browser needs the
    // new reference, or its next request arrives with a spent refresh token.
    if (changed(fresh, result.session)) {
      writeSessionRef(event, config, result.ref);
    }

    const upstream: Response = result.response;
    setResponseStatus(event, upstream.status);
    setResponseHeaders(event, forwardableHeaders(upstream.headers));

    return new Uint8Array(await upstream.arrayBuffer());
  });
}

/** Whether a refresh rotated the session out from under `before`. */
function changed(before: BffSession, after: BffSession): boolean {
  return (
    after.version !== before.version || after.accessToken !== before.accessToken
  );
}

/**
 * Answer a failed forward.
 *
 * An upstream failure is relayed verbatim — status, headers, and body — so the
 * browser sees the API's own problem details, `errors[]` and `traceId` included.
 * The BFF's own faults (an unreachable API, a forward that timed out) have no
 * upstream response to relay, so they are rendered as problem details of their
 * own.
 */
function respondToFailure(event: H3Event, error: unknown): Uint8Array {
  if (error instanceof UpstreamError) {
    setResponseStatus(event, error.response.status);
    setResponseHeaders(event, forwardableHeaders(error.response.headers));
    return new TextEncoder().encode(error.bodyText);
  }

  if (error instanceof WallowError) {
    setResponseStatus(event, error.status);
    setResponseHeaders(event, {
      "content-type": "application/problem+json",
    });
    return new TextEncoder().encode(
      JSON.stringify({
        type: `https://httpstatuses.io/${error.status}`,
        title: error.title,
        status: error.status,
        detail: error.detail,
        code: error.code,
      }),
    );
  }

  throw error;
}

/** Upstream response headers safe to re-emit on a re-framed response. */
function forwardableHeaders(headers: Headers): Record<string, string> {
  const forwardable: Record<string, string> = {};
  headers.forEach((value: string, key: string): void => {
    const lower: string = key.toLowerCase();
    if (lower === "transfer-encoding" || lower === "content-encoding") {
      return;
    }
    forwardable[key] = value;
  });
  return forwardable;
}
