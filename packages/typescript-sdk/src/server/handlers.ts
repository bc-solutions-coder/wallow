/**
 * h3 route handlers for the BFF OIDC tunnel: login, callback, user, logout.
 *
 * `createBffHandlers(config)` composes the pure F3 modules (PKCE, OIDC,
 * session, claims, txstate) into four `defineEventHandler` objects. The
 * `readSession`/`writeSession` helpers are shared with the `/api` proxy.
 *
 * Only the OIDC helpers are imported from our own modules — the h3 package
 * ships its own `sealSession`/`unsealSession`, so we deliberately import
 * neither of those names from `h3` to avoid the collision.
 */
import {
  defineEventHandler,
  deleteCookie,
  getCookie,
  getQuery,
  sendRedirect,
  setCookie,
  setResponseStatus,
  type EventHandler,
  type H3Event,
} from "h3";

import { decodeIdTokenClaims } from "./claims";
import type { BffConfig } from "./config";
import { createPkcePair, randomUrlSafe } from "./pkce";
import {
  buildAuthorizeUrl,
  discover,
  exchangeCode,
  type DiscoveryDoc,
  type TokenResponse,
} from "./oidc";
import { sealSession, unsealSession, type BffSession } from "./session";
import { sealTx, unsealTx, type LoginTx } from "./txstate";

/** The four BFF route handlers returned by {@link createBffHandlers}. */
export interface BffHandlers {
  login: EventHandler;
  callback: EventHandler;
  user: EventHandler;
  logout: EventHandler;
}

/** Base attributes shared by every cookie the BFF writes. */
function baseCookieOpts(secure: boolean = true): {
  httpOnly: true;
  sameSite: "lax";
  secure: boolean;
  path: "/";
} {
  return { httpOnly: true, sameSite: "lax", secure, path: "/" };
}

/** Name of the transient login-transaction cookie for a given session cookie. */
function txCookieName(cookieName: string): string {
  return `${cookieName}_tx`;
}

/**
 * Read and unseal the current session from the BFF session cookie.
 *
 * @param event The incoming h3 request event.
 * @param config BFF configuration providing the cookie name and password.
 * @returns The decoded session, or `null` when no valid session cookie exists.
 */
export async function readSession(
  event: H3Event,
  config: BffConfig,
): Promise<BffSession | null> {
  const sealed: string | undefined = getCookie(event, config.cookieName);
  if (sealed === undefined || sealed === "") {
    return null;
  }
  return unsealSession(sealed, config.cookiePassword);
}

/**
 * Seal a session and write it to the BFF session cookie.
 *
 * @param event The h3 request event to attach the cookie to.
 * @param config BFF configuration providing the cookie name and password.
 * @param session The session to seal and persist.
 */
export async function writeSession(
  event: H3Event,
  config: BffConfig,
  session: BffSession,
): Promise<void> {
  const sealed: string = await sealSession(session, config.cookiePassword);
  setCookie(event, config.cookieName, sealed, baseCookieOpts());
}

/**
 * Build the four BFF route handlers bound to a given configuration.
 *
 * @param config Server-side BFF configuration.
 * @returns `{ login, callback, user, logout }` h3 event handlers.
 */
export function createBffHandlers(config: BffConfig): BffHandlers {
  return {
    login: defineEventHandler(async (event: H3Event): Promise<void> => {
      const query: Record<string, unknown> = getQuery(event);
      const returnTo: string =
        typeof query.returnTo === "string" && query.returnTo !== ""
          ? query.returnTo
          : "/";

      const doc: DiscoveryDoc = await discover(config);
      const { verifier, challenge } = await createPkcePair();
      const state: string = randomUrlSafe(24);
      const nonce: string = randomUrlSafe(24);

      const tx: LoginTx = { state, nonce, verifier, returnTo };
      const sealed: string = await sealTx(tx, config.cookiePassword);
      setCookie(event, txCookieName(config.cookieName), sealed, {
        ...baseCookieOpts(),
        maxAge: 600,
      });

      const authorizeUrl: string = buildAuthorizeUrl(config, doc, {
        state,
        codeChallenge: challenge,
        nonce,
      });
      return sendRedirect(event, authorizeUrl, 302);
    }),

    callback: defineEventHandler(async (event: H3Event): Promise<void | null> => {
      const query: Record<string, unknown> = getQuery(event);
      const code: unknown = query.code;
      const state: unknown = query.state;

      const txName: string = txCookieName(config.cookieName);
      const sealedTx: string | undefined = getCookie(event, txName);
      if (sealedTx === undefined || sealedTx === "") {
        setResponseStatus(event, 400);
        return null;
      }

      const tx: LoginTx | null = await unsealTx(sealedTx, config.cookiePassword);
      deleteCookie(event, txName, baseCookieOpts());

      if (
        tx === null ||
        typeof code !== "string" ||
        typeof state !== "string" ||
        state !== tx.state
      ) {
        setResponseStatus(event, 400);
        return null;
      }

      const doc: DiscoveryDoc = await discover(config);
      const tokens: TokenResponse = await exchangeCode(config, doc, {
        code,
        codeVerifier: tx.verifier,
      });

      const session: BffSession = {
        accessToken: tokens.access_token,
        refreshToken: tokens.refresh_token,
        idToken: tokens.id_token,
        expiresAt: Date.now() + tokens.expires_in * 1000,
        user:
          tokens.id_token !== undefined
            ? decodeIdTokenClaims(tokens.id_token)
            : { sub: "" },
      };
      await writeSession(event, config, session);

      return sendRedirect(event, tx.returnTo, 302);
    }),

    user: defineEventHandler(
      async (event: H3Event): Promise<BffSession["user"] | null> => {
        const session: BffSession | null = await readSession(event, config);
        if (session === null) {
          setResponseStatus(event, 401);
          return null;
        }
        return session.user;
      },
    ),

    logout: defineEventHandler(async (event: H3Event): Promise<void> => {
      const session: BffSession | null = await readSession(event, config);
      deleteCookie(event, config.cookieName, baseCookieOpts());

      const doc: DiscoveryDoc = await discover(config);
      if (doc.end_session_endpoint === undefined) {
        return sendRedirect(event, config.postLogoutRedirectUri, 302);
      }

      const url: URL = new URL(doc.end_session_endpoint);
      url.searchParams.set(
        "post_logout_redirect_uri",
        config.postLogoutRedirectUri,
      );
      if (session?.idToken !== undefined) {
        url.searchParams.set("id_token_hint", session.idToken);
      }
      return sendRedirect(event, url.toString(), 302);
    }),
  };
}
