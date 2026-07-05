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
import {
  discover,
  refreshTokens,
  type DiscoveryDoc,
  type TokenResponse,
} from "./oidc";
import type { BffSession } from "./session";
import { readSession, readSessionRef, writeSession } from "./handlers";
import { CookieSessionStore } from "./store/cookie";
import type { SessionStore } from "./store/types";

/** How long before real expiry a token is treated as expired (ms). */
export const EXPIRY_SKEW_MS = 30_000;

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

  const refreshToken: string = session.refreshToken;

  const refreshed: BffSession | undefined = await store.withRefreshLock(
    ref,
    async (): Promise<BffSession> => {
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
      await store.write(next);
      return next;
    },
  );

  if (refreshed !== undefined) {
    return refreshed;
  }

  // The lock was held by a concurrent refresh; adopt whatever it stored.
  const peer: BffSession | null = await store.read(ref);
  if (peer === null) {
    throw new Error("Session refresh lock was held but the session is gone");
  }
  return peer;
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
    try {
      fresh = await ensureFreshSession(session, config, store, ref);
    } catch {
      setResponseStatus(event, 401);
      return null;
    }

    // Re-seal the cookie only when the session actually changed.
    if (
      fresh.version !== session.version ||
      fresh.accessToken !== session.accessToken
    ) {
      await writeSession(event, config, store, fresh);
    }

    // Strip the `/api` prefix and re-root at the downstream API base URL.
    const requestPath: string = getRequestPath(event);
    const stripped: string = requestPath.replace(/^\/api/, "") || "/";
    const target: string = new URL(stripped, config.apiBaseUrl).toString();

    const incoming: Record<string, string | undefined> =
      getRequestHeaders(event);
    const method: string = getMethod(event);

    const forwardHeaders: Headers = new Headers();
    forwardHeaders.set("authorization", `Bearer ${fresh.accessToken}`);
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

    const upstream: Response = await fetch(target, {
      method,
      headers: forwardHeaders,
      body,
    });

    setResponseStatus(event, upstream.status);

    const responseHeaders: Record<string, string> = {};
    upstream.headers.forEach((value: string, key: string): void => {
      const lower: string = key.toLowerCase();
      if (lower === "transfer-encoding" || lower === "content-encoding") {
        return;
      }
      responseHeaders[key] = value;
    });
    setResponseHeaders(event, responseHeaders);

    return new Uint8Array(await upstream.arrayBuffer());
  });
}
