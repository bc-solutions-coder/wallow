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
  getRequestURL,
  parseCookies,
  sendRedirect,
  setCookie,
  setResponseStatus,
  type EventHandler,
  type H3Event,
} from "h3";

import { decodeIdTokenClaims, mapClaims } from "./claims";
import type { BffConfig } from "./config";
import { createPkcePair, randomUrlSafe } from "./pkce";
import {
  buildAuthorizeUrl,
  buildLogoutUrl,
  discover,
  exchangeCode,
  fetchUserInfo,
  type DiscoveryDoc,
  type TokenResponse,
} from "./oidc";
import { type BffSession } from "./session";
import { CookieSessionStore } from "./store/cookie";
import type { SessionStore } from "./store/types";
import { sealTx, unsealTx, type LoginTx } from "./txstate";

/**
 * Body of the `/bff/user` response: the session's identity claims plus the CSRF
 * synchronizer token the browser must echo on state-changing requests.
 */
export type BffUserResponse = BffSession["user"] & { csrfToken?: string };

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

/**
 * Attributes for the double-submit CSRF cookie: identical to
 * {@link baseCookieOpts} except that it is deliberately NOT `HttpOnly`, because
 * browser JS must read the token to echo it back in the `x-csrf-token` header.
 * It carries no credential of its own — the session cookie remains `HttpOnly`.
 */
function csrfCookieOpts(secure: boolean = true): {
  httpOnly: false;
  sameSite: "lax";
  secure: boolean;
  path: "/";
} {
  return { ...baseCookieOpts(secure), httpOnly: false };
}

/** Name of the transient login-transaction cookie for a given session cookie. */
function txCookieName(cookieName: string): string {
  return `${cookieName}_tx`;
}

/** Name of the readable CSRF companion cookie for a given session cookie. */
function csrfCookieName(cookieName: string): string {
  return `${cookieName}-csrf`;
}

/**
 * Maximum characters stored in a single session-cookie chunk. Browsers cap each
 * cookie (name plus value plus attributes) at roughly 4096 bytes, so a sealed
 * BFF session carrying access, refresh, and id tokens routinely overflows a
 * single cookie. The sealed value is split into chunks no larger than this so
 * every emitted cookie stays under the limit; the leftover budget covers the
 * cookie name and attributes.
 */
const MAX_COOKIE_VALUE_LENGTH: number = 3800;

/**
 * Upper bound on stale higher-index chunks cleared when writing a shorter
 * session over a previously longer one. A session spanning more than this many
 * chunks is not expected in practice.
 */
const MAX_CHUNK_CLEAR: number = 16;

/**
 * Name of the {@link index}-th session-cookie chunk. Chunk 0 keeps the base
 * cookie name so a single-chunk session is written and read exactly as an
 * unchunked cookie (preserving compatibility with callers that set the base
 * cookie directly).
 */
function chunkCookieName(cookieName: string, index: number): string {
  return index === 0 ? cookieName : `${cookieName}.${index}`;
}

/**
 * Reassemble the opaque session reference from its chunk cookies.
 *
 * Concatenates the chunk cookies (`name`, `name.1`, `name.2`, ...) into the
 * single reference string that was written across them, so references larger
 * than a single cookie are restored transparently.
 *
 * @param event The incoming h3 request event.
 * @param config BFF configuration providing the cookie name.
 * @returns The assembled reference, or `null` when no session cookie exists.
 */
export function readSessionRef(
  event: H3Event,
  config: BffConfig,
): string | null {
  const first: string | undefined = getCookie(event, config.cookieName);
  if (first === undefined || first === "") {
    return null;
  }

  let ref: string = first;
  for (let index: number = 1; ; index += 1) {
    const part: string | undefined = getCookie(
      event,
      chunkCookieName(config.cookieName, index),
    );
    if (part === undefined || part === "") {
      break;
    }
    ref += part;
  }

  return ref;
}

/**
 * Read the current session by resolving the cookie's opaque reference through
 * the injected {@link SessionStore}.
 *
 * Reassembles the reference from its chunk cookies (`name`, `name.1`,
 * `name.2`, ...) before handing it to the store, so references larger than a
 * single cookie are restored transparently.
 *
 * @param event The incoming h3 request event.
 * @param config BFF configuration providing the cookie name.
 * @param store The session store that resolves the reference into a session.
 * @returns The decoded session, or `null` when no valid session cookie exists.
 */
export async function readSession(
  event: H3Event,
  config: BffConfig,
  store: SessionStore,
): Promise<BffSession | null> {
  const ref: string | null = readSessionRef(event, config);
  if (ref === null) {
    return null;
  }
  return store.read(ref);
}

/**
 * Write an opaque session reference to the BFF session cookie(s).
 *
 * The reference is split across as many chunk cookies as needed to stay under
 * the per-cookie size limit, and any stale higher-index chunks from a
 * previously larger reference are cleared.
 *
 * Use this when the session has already been persisted and its reference is in
 * hand — the `/api` proxy re-seals the cookie after a reactive refresh has
 * written the rotated session inside the refresh lock, and must not write it a
 * second time.
 *
 * @param event The h3 request event to attach the cookie to.
 * @param config BFF configuration providing the cookie name.
 * @param ref The opaque store reference to place in the cookie.
 */
export function writeSessionRef(
  event: H3Event,
  config: BffConfig,
  ref: string,
): void {
  const chunkCount: number = Math.max(
    1,
    Math.ceil(ref.length / MAX_COOKIE_VALUE_LENGTH),
  );
  for (let index: number = 0; index < chunkCount; index += 1) {
    const start: number = index * MAX_COOKIE_VALUE_LENGTH;
    setCookie(
      event,
      chunkCookieName(config.cookieName, index),
      ref.slice(start, start + MAX_COOKIE_VALUE_LENGTH),
      baseCookieOpts(),
    );
  }

  for (let index: number = chunkCount; index < MAX_CHUNK_CLEAR; index += 1) {
    deleteCookie(
      event,
      chunkCookieName(config.cookieName, index),
      baseCookieOpts(),
    );
  }
}

/**
 * Persist a session through the injected {@link SessionStore} and write the
 * returned opaque reference to the BFF session cookie(s).
 *
 * @param event The h3 request event to attach the cookie to.
 * @param config BFF configuration providing the cookie name.
 * @param store The session store that persists the session and returns its ref.
 * @param session The session to persist.
 * @returns The opaque reference the session was stored under.
 */
export async function writeSession(
  event: H3Event,
  config: BffConfig,
  store: SessionStore,
  session: BffSession,
): Promise<string> {
  const ref: string = await store.write(session);
  writeSessionRef(event, config, ref);
  return ref;
}

/**
 * Clear the session cookie and every one of its chunk cookies present on the
 * request.
 *
 * @param event The h3 request event to clear cookies on.
 * @param config BFF configuration providing the base cookie name.
 */
function clearSession(event: H3Event, config: BffConfig): void {
  deleteCookie(event, config.cookieName, baseCookieOpts());
  deleteCookie(event, csrfCookieName(config.cookieName), csrfCookieOpts());
  const cookies: Record<string, string> = parseCookies(event);
  const chunkPrefix: string = `${config.cookieName}.`;
  for (const name of Object.keys(cookies)) {
    if (name.startsWith(chunkPrefix)) {
      deleteCookie(event, name, baseCookieOpts());
    }
  }
}

/**
 * Build the four BFF route handlers bound to a given configuration.
 *
 * @param config Server-side BFF configuration.
 * @param store Session store used to resolve, persist, and revoke sessions.
 *   Defaults to a cookie-only {@link CookieSessionStore}, so single-argument
 *   callers keep working.
 * @returns `{ login, callback, user, logout }` h3 event handlers.
 */
export function createBffHandlers(
  config: BffConfig,
  store: SessionStore = new CookieSessionStore({
    password: config.cookiePassword,
  }),
): BffHandlers {
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

    callback: defineEventHandler(
      async (event: H3Event): Promise<void | null> => {
        const query: Record<string, unknown> = getQuery(event);
        const code: unknown = query.code;
        const state: unknown = query.state;

        const txName: string = txCookieName(config.cookieName);
        const sealedTx: string | undefined = getCookie(event, txName);
        if (sealedTx === undefined || sealedTx === "") {
          setResponseStatus(event, 400);
          return null;
        }

        const tx: LoginTx | null = await unsealTx(
          sealedTx,
          config.cookiePassword,
        );
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
          currentUrl: getRequestURL(event),
          state: tx.state,
          nonce: tx.nonce,
        });

        // Base identity from the id_token (carries `sub` and any issuer-specific
        // claims), then overlay the userinfo response — providers such as
        // OpenIddict emit standard claims (`email`, `name`, ...) to userinfo
        // rather than the id_token, so this is what surfaces the user's email.
        let user: BffSession["user"] =
          tokens.id_token !== undefined
            ? decodeIdTokenClaims(tokens.id_token)
            : { sub: "" };
        const info: Record<string, unknown> | null = await fetchUserInfo(
          doc,
          tokens.access_token,
        );
        if (info !== null) {
          user = {
            ...user,
            ...info,
            sub: typeof info.sub === "string" ? info.sub : user.sub,
          };
        }
        // Normalize authorization + tenant claims into first-class user fields.
        user = mapClaims(user);

        const csrfToken: string = randomUrlSafe(24);
        const session: BffSession = {
          sessionId: randomUrlSafe(24),
          accessToken: tokens.access_token,
          refreshToken: tokens.refresh_token,
          idToken: tokens.id_token,
          expiresAt: Date.now() + tokens.expires_in * 1000,
          user,
          version: 1,
          csrfToken,
        };
        await writeSession(event, config, store, session);
        // Double-submit companion: the same synchronizer token, in the one
        // cookie the browser is allowed to read.
        setCookie(
          event,
          csrfCookieName(config.cookieName),
          csrfToken,
          csrfCookieOpts(),
        );

        return sendRedirect(event, tx.returnTo, 302);
      },
    ),

    user: defineEventHandler(
      async (event: H3Event): Promise<BffUserResponse | null> => {
        const session: BffSession | null = await readSession(
          event,
          config,
          store,
        );
        if (session === null) {
          setResponseStatus(event, 401);
          return null;
        }
        // The identity claims plus the CSRF token, for SPA clients that read
        // the token from here rather than from the companion cookie. Session
        // tokens are never part of this shape.
        return { ...session.user, csrfToken: session.csrfToken };
      },
    ),

    logout: defineEventHandler(async (event: H3Event): Promise<void> => {
      const session: BffSession | null = await readSession(
        event,
        config,
        store,
      );
      // Revoke the session server-side before clearing the browser cookies so a
      // store-backed session cannot be replayed after logout.
      const ref: string | null = readSessionRef(event, config);
      if (ref !== null) {
        await store.destroy(ref);
      }
      clearSession(event, config);

      const doc: DiscoveryDoc = await discover(config);
      const logoutUrl: string = buildLogoutUrl(config, doc, session?.idToken);
      return sendRedirect(event, logoutUrl, 302);
    }),
  };
}
