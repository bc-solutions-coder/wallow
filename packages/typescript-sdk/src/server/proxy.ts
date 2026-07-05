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
import { readSession, writeSession } from "./handlers";

/** How long before real expiry a token is treated as expired (ms). */
export const EXPIRY_SKEW_MS = 30_000;

/** Result of {@link ensureFreshSession}. */
export interface EnsureFreshResult {
  /** The (possibly refreshed) session. */
  session: BffSession;
  /** Whether the session was refreshed and should be re-sealed. */
  refreshed: boolean;
}

/**
 * Ensure the session's access token is fresh, refreshing it when it is within
 * {@link EXPIRY_SKEW_MS} of expiry.
 *
 * @param config BFF configuration.
 * @param doc The issuer discovery document.
 * @param session The current session.
 * @returns The (possibly refreshed) session and whether it changed.
 * @throws When the token is expired and no refresh token is available.
 */
export async function ensureFreshSession(
  config: BffConfig,
  doc: DiscoveryDoc,
  session: BffSession,
): Promise<EnsureFreshResult> {
  if (session.expiresAt - EXPIRY_SKEW_MS > Date.now()) {
    return { session, refreshed: false };
  }

  if (session.refreshToken === undefined || session.refreshToken === "") {
    throw new Error("Session expired and no refresh token is available");
  }

  const tokens: TokenResponse = await refreshTokens(
    config,
    doc,
    session.refreshToken,
  );

  return {
    refreshed: true,
    session: {
      ...session,
      accessToken: tokens.access_token,
      refreshToken: tokens.refresh_token ?? session.refreshToken,
      idToken: tokens.id_token ?? session.idToken,
      expiresAt: Date.now() + tokens.expires_in * 1000,
    },
  };
}

/**
 * Build the `/api` reverse-proxy h3 handler bound to a configuration.
 *
 * @param config Server-side BFF configuration.
 * @returns An h3 event handler that proxies to `config.apiBaseUrl`.
 */
export function createApiProxy(config: BffConfig): EventHandler {
  return defineEventHandler(async (event: H3Event): Promise<unknown> => {
    const session: BffSession | null = await readSession(event, config);
    if (session === null) {
      setResponseStatus(event, 401);
      return null;
    }

    const doc: DiscoveryDoc = await discover(config);

    let result: EnsureFreshResult;
    try {
      result = await ensureFreshSession(config, doc, session);
    } catch {
      setResponseStatus(event, 401);
      return null;
    }

    if (result.refreshed) {
      await writeSession(event, config, result.session);
    }

    // Strip the `/api` prefix and re-root at the downstream API base URL.
    const requestPath: string = getRequestPath(event);
    const stripped: string = requestPath.replace(/^\/api/, "") || "/";
    const target: string = new URL(stripped, config.apiBaseUrl).toString();

    const incoming: Record<string, string | undefined> =
      getRequestHeaders(event);
    const method: string = getMethod(event);

    const forwardHeaders: Headers = new Headers();
    forwardHeaders.set(
      "authorization",
      `Bearer ${result.session.accessToken}`,
    );
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
