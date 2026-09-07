/**
 * Web-standard BFF handlers for login, callback, user identity, and three logout flows.
 */
import { ClientErrorCode, ErrorCode } from "@bc-solutions-coder/api-errors";
import { parse as parseCookies, serialize as serializeCookie } from "cookie-es";

import { isSafeReturnUrl } from "../auth-oidc";
import { resolveRequestId } from "../request-id";
import { createBackchannelLogoutHandler } from "./backchannel-logout";
import { decodeIdTokenClaims, mapClaims } from "./claims";
import type { BffConfig, BffCookieSameSite } from "./config";
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
import { csrfTokenMatches, CSRF_HEADER } from "./csrf";
import { problemResponse } from "./problem";
import { type BffSession } from "./session";
import { CookieSessionStore } from "./store/cookie";
import type { SessionStore } from "./store/types";
import { sealTx, unsealTx, type LoginTx } from "./txstate";

/**
 * Body of the `/bff/user` response: the session's identity claims plus the CSRF
 * synchronizer token the browser must echo on state-changing requests.
 */
export type BffUserResponse = BffSession["user"] & { csrfToken?: string };

/** A BFF route handler: a web-standard request in, a web-standard response out. */
export type BffHandler = (request: Request) => Promise<Response>;

/** The six BFF route handlers returned by {@link createBffHandlers}. */
export interface BffHandlers {
  /**
   * Redirect the browser to OIDC authorization with PKCE and set a ten-minute transaction
   * cookie. Accepts a local returnTo query parameter, falling back to / for unsafe values,
   * and an optional organization hint validated by the issuer.
   */
  login: BffHandler;
  /**
   * Consume the transaction cookie and validate the authorization code, state, nonce, and
   * PKCE verifier. Stores the token session, sets session and readable CSRF cookies, and
   * redirects to the saved local returnTo. Missing or mismatched transaction data returns a
   * 400 problem.
   */
  callback: BffHandler;
  /**
   * Return stored identity claims and the session CSRF token with Cache-Control: no-store.
   * Returns a 401 problem when no session resolves. Does not expose access or refresh tokens
   * or refresh the session.
   */
  user: BffHandler;
  /**
   * Accept POST with a matching x-csrf-token, destroy the session, and clear its cookies.
   * Returns a JSON logoutUrl for browser navigation to the issuer. A missing session clears
   * cookies and returns 204; other methods return 405 and invalid CSRF returns 403.
   */
  logout: BffHandler;
  /**
   * Accept the issuer GET notification with iss and sid query values. Destroys the session
   * and clears cookies only when both match the browser session. Returns the same no-store
   * HTML page for matching and nonmatching notifications.
   */
  frontchannelLogout: BffHandler;
  /**
   * Accept an issuer POST containing a signed logout_token form field without browser
   * cookies or CSRF. Verifies the token, revokes indexed sessions through the store, and
   * attempts upstream refresh-token revocation. Cookie-only stores cannot revoke sessions
   * through this handler.
   */
  backchannelLogout: BffHandler;
}

/** The `Set-Cookie` attributes the BFF writes, in `cookie-es` terms. */
interface CookieAttributes {
  httpOnly: boolean;
  sameSite: BffCookieSameSite;
  secure: boolean;
  path: "/";
  maxAge?: number;
}

/**
 * Base attributes shared by every cookie the BFF writes. `sameSite` defaults to
 * `"lax"` because the one caller that must NOT follow `cookieSameSite` — the
 * login-transaction cookie, which has to ride the cross-site callback redirect
 * from the IdP — relies on that default; every other caller passes the
 * configured value through.
 */
function baseCookieOpts(
  secure: boolean = true,
  sameSite: BffCookieSameSite = "lax",
): CookieAttributes {
  return { httpOnly: true, sameSite, secure, path: "/" };
}

/**
 * Attributes for the session cookie (and each of its chunks): the base
 * attributes with `Secure` driven by `COOKIE_SECURE`, `SameSite` driven by
 * `COOKIE_SAMESITE`, and a `Max-Age` that bounds the cookie's lifetime to the
 * configured session TTL, so a stale browser cookie cannot outlive the session
 * it references.
 */
function sessionCookieOpts(config: BffConfig): CookieAttributes {
  return {
    ...baseCookieOpts(config.cookieSecure, config.cookieSameSite),
    maxAge: config.sessionTtlSeconds,
  };
}

/**
 * Attributes for the double-submit CSRF cookie: identical to
 * {@link sessionCookieOpts} except that it is deliberately NOT `HttpOnly`,
 * because browser JS must read the token to echo it back in the `x-csrf-token`
 * header. It carries no credential of its own — the session cookie remains
 * `HttpOnly`. It shares the session's `Max-Age` so the companion token never
 * outlives the session it defends.
 */
function csrfCookieOpts(config: BffConfig): CookieAttributes {
  return { ...sessionCookieOpts(config), httpOnly: false };
}

/**
 * Attributes that expire a cookie immediately. `Max-Age=0` is the clearing
 * signal; the name, `Domain`, and `Path` must still match the cookie as
 * written or the browser treats it as a different cookie and leaves the
 * original in place. `SameSite` is not part of cookie identity, which is why
 * one clear helper serves both the Lax-pinned tx cookie and the
 * `cookieSameSite`-driven session and CSRF cookies.
 */
function clearCookieOpts(config: BffConfig, httpOnly: boolean = true): CookieAttributes {
  return {
    ...baseCookieOpts(config.cookieSecure, config.cookieSameSite),
    httpOnly,
    maxAge: CLEARED_MAX_AGE,
  };
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

/** Index of the first (base-named) session-cookie chunk. */
const FIRST_CHUNK_INDEX = 0;

/** Index of the first suffixed chunk (`name.1`), where reassembly resumes. */
const SECOND_CHUNK_INDEX = 1;

/** Step between successive chunk indices. */
const CHUNK_STEP = 1;

/** The minimum chunk count a written reference always occupies. */
const SINGLE_CHUNK = 1;

/** The `Max-Age` that expires a cookie the moment the browser receives it. */
const CLEARED_MAX_AGE = 0;

/** The version stamped on a freshly minted session. */
const INITIAL_SESSION_VERSION = 1;

/** Random-byte count for generated ids and tokens (state, nonce, CSRF, id). */
const TOKEN_BYTES = 24;

/** Milliseconds in a second, for converting `expires_in` deltas. */
const MS_PER_SECOND = 1000;

/** Lifetime of the transient login-transaction cookie (seconds). */
const TX_COOKIE_MAX_AGE_SECONDS = 600;

/** HTTP status for the front-channel logout notification page. */
const OK_STATUS = 200;

/** HTTP status for a logout that had no session to end. */
const NO_CONTENT_STATUS = 204;

/** HTTP status for a malformed or replayed callback request. */
const BAD_REQUEST_STATUS = 400;

/** HTTP status when no valid session backs a `/bff/user` request. */
const UNAUTHORIZED_STATUS = 401;

/** HTTP status for a logout whose CSRF token is missing or does not match. */
const FORBIDDEN_STATUS = 403;

/** HTTP status for a logout that did not arrive as a `POST`. */
const METHOD_NOT_ALLOWED_STATUS = 405;

/** HTTP status for the redirects the tunnel issues. */
const FOUND_STATUS = 302;

/** The only method that may end a session. */
const LOGOUT_METHOD: string = "POST";

/** The only method the OP's hidden iframe uses for a front-channel notification. */
const FRONTCHANNEL_METHOD: string = "GET";

/** The fallback a sanitized `returnTo` lands on. */
const SAFE_RETURN_TO: string = "/";

/**
 * Issuer identity for the front-channel `iss` check. A lone trailing slash is
 * the one representational difference tolerated — an OP configured as
 * `https://auth.example.com` and one advertising `https://auth.example.com/`
 * are the same issuer; anything else is a mismatch.
 */
function normalizeIssuer(issuer: string): string {
  return issuer.replace(/\/$/u, "");
}

/**
 * The body every front-channel notification answers with, hit or miss: the
 * page renders inside the OP's hidden iframe where nobody reads it, and a
 * uniform response keeps a prober from learning whether a session existed.
 */
const FRONTCHANNEL_PAGE: string =
  '<!doctype html><html><head><meta charset="utf-8"><title>Signed out</title></head>' +
  "<body>Signed out.</body></html>";

/**
 * Name of the {@link index}-th session-cookie chunk. Chunk 0 keeps the base
 * cookie name so a single-chunk session is written and read exactly as an
 * unchunked cookie (preserving compatibility with callers that set the base
 * cookie directly).
 */
function chunkCookieName(cookieName: string, index: number): string {
  return index === FIRST_CHUNK_INDEX ? cookieName : `${cookieName}.${index}`;
}

/** Every cookie on the incoming request, by name. */
function requestCookies(request: Request): Record<string, string | undefined> {
  return parseCookies(request.headers.get("cookie") ?? "");
}

/** One cookie from the incoming request, or `undefined` when absent. */
function requestCookie(request: Request, name: string): string | undefined {
  return requestCookies(request)[name];
}

/**
 * Append one `Set-Cookie` line to a response's headers.
 *
 * `append`, never `set`: `Headers.set` would discard every cookie already
 * written, which silently half-writes a chunked session.
 */
function appendCookie(
  headers: Headers,
  name: string,
  value: string,
  options: CookieAttributes,
): void {
  headers.append("set-cookie", serializeCookie(name, value, options));
}

/**
 * A `returnTo` value that is safe to redirect a browser to.
 *
 * `/bff/login?returnTo=` is attacker-reachable and the value survives the whole
 * OIDC round trip inside the tx cookie, so both the login entry point and the
 * callback run it through the shared guard. SANITIZE, DO NOT REFUSE: an unsafe
 * value falls back to `/` and login still proceeds, matching the backend's
 * `ReturnUrlValidator.Sanitize`. A hard 400 here would turn a merely-malformed
 * link into a broken login.
 */
function safeReturnTo(returnTo: string | null | undefined): string {
  return isSafeReturnUrl(returnTo) ? (returnTo as string) : SAFE_RETURN_TO;
}

/**
 * Read and reassemble the opaque BFF session reference from request cookies.
 *
 * Joins the base cookie and consecutive numbered chunks, stopping at the first missing or empty
 * chunk. Does not validate or unseal the reference.
 *
 * @param request Incoming request containing the Cookie header.
 *
 * @param config Configuration providing the session cookie name.
 *
 * @returns The assembled reference, or null when the base cookie is missing or empty.
 */
export function readSessionRef(request: Request, config: BffConfig): string | null {
  const cookies: Record<string, string | undefined> = requestCookies(request);
  const first: string | undefined = cookies[config.cookieName];
  if (first === undefined || first === "") {
    return null;
  }

  let ref: string = first;
  for (let index: number = SECOND_CHUNK_INDEX; ; index += CHUNK_STEP) {
    const part: string | undefined = cookies[chunkCookieName(config.cookieName, index)];
    if (part === undefined || part === "") {
      break;
    }
    ref += part;
  }

  return ref;
}

/**
 * Resolve the incoming BFF cookie through the selected session store.
 *
 * Returns null when there is no reference or the store cannot resolve it. Does not refresh
 * access tokens or modify response cookies.
 *
 * @param request Incoming request containing the session cookie.
 *
 * @param config Configuration providing the session cookie name.
 *
 * @param store The same store used by the login callback and API proxy.
 *
 * @throws Propagates session-store failures.
 */
export async function readSession(
  request: Request,
  config: BffConfig,
  store: SessionStore,
): Promise<BffSession | null> {
  const ref: string | null = readSessionRef(request, config);
  if (ref === null) {
    return null;
  }
  return await store.read(ref);
}

/**
 * Append an opaque session reference to response Set-Cookie headers.
 *
 * Splits long references into cookie chunks and expires stale chunk positions below index 16.
 * Uses the configured cookie name, security attributes, and session TTL; existing response
 * cookies remain intact.
 *
 * @param headers Mutable response headers that the host must send to the browser.
 *
 * @param config Session cookie settings.
 *
 * @param ref Reference returned by the session store.
 */
export function writeSessionRef(headers: Headers, config: BffConfig, ref: string): void {
  const chunkCount: number = Math.max(
    SINGLE_CHUNK,
    Math.ceil(ref.length / MAX_COOKIE_VALUE_LENGTH),
  );
  for (let index: number = FIRST_CHUNK_INDEX; index < chunkCount; index += CHUNK_STEP) {
    const start: number = index * MAX_COOKIE_VALUE_LENGTH;
    appendCookie(
      headers,
      chunkCookieName(config.cookieName, index),
      ref.slice(start, start + MAX_COOKIE_VALUE_LENGTH),
      sessionCookieOpts(config),
    );
  }

  for (let index: number = chunkCount; index < MAX_CHUNK_CLEAR; index += CHUNK_STEP) {
    appendCookie(headers, chunkCookieName(config.cookieName, index), "", clearCookieOpts(config));
  }
}

/**
 * Persist a BFF session and append its reference to response cookies.
 *
 * Uses store.write before adding the session cookies, preserving existing Set-Cookie headers.
 * Does not create the companion CSRF cookie.
 *
 * @param headers Mutable response headers that the host must send to the browser.
 *
 * @param config Session cookie settings.
 *
 * @param store Store shared with the BFF handlers and API proxy.
 *
 * @param session Session containing tokens, identity, and expiry in epoch milliseconds.
 *
 * @returns The opaque reference returned by the store.
 *
 * @throws Propagates persistence and cookie-sealing failures.
 */
export async function writeSession(
  headers: Headers,
  config: BffConfig,
  store: SessionStore,
  session: BffSession,
): Promise<string> {
  const ref: string = await store.write(session);
  writeSessionRef(headers, config, ref);
  return ref;
}

/**
 * Clear the session cookie, its CSRF companion, and every chunk cookie present
 * on the request.
 *
 * Exported for the proxy's refresh-failure teardown: a dead session is ended
 * with exactly the cookie-clearing a logout performs, so the browser cannot
 * keep presenting a reference whose refresh can only fail again.
 *
 * @param headers The response headers under construction.
 * @param request The request whose cookies name the chunks to clear.
 * @param config BFF configuration providing the base cookie name.
 */
export function clearSession(headers: Headers, request: Request, config: BffConfig): void {
  appendCookie(headers, config.cookieName, "", clearCookieOpts(config));
  appendCookie(headers, csrfCookieName(config.cookieName), "", clearCookieOpts(config, false));

  const chunkPrefix: string = `${config.cookieName}.`;
  for (const name of Object.keys(requestCookies(request))) {
    if (name.startsWith(chunkPrefix)) {
      appendCookie(headers, name, "", clearCookieOpts(config));
    }
  }
}

/** A `302` to {@link location}, carrying whatever cookies were accumulated. */
function redirect(location: string, headers: Headers): Response {
  headers.set("location", location);
  return new Response(null, { status: FOUND_STATUS, headers });
}

/**
 * The URL openid-client validates the authorization response against.
 *
 * Rooted at `config.redirectUri`, NOT at the incoming request URL: openid-client
 * derives the token request's `redirect_uri` from this, and the Node hosts build
 * the `Request` as `http://${req.headers.host}${req.url}` without consulting
 * `x-forwarded-proto`. Behind TLS termination a callback that reached the edge
 * as `https://app.example.com/bff/callback` therefore arrives here as
 * `http://localhost:3000/bff/callback`, and the IdP would answer `invalid_grant`
 * in every such deployment while every local test still passed. The incoming
 * query — the `code` and `state` openid-client checks — is copied on verbatim.
 */
function callbackUrl(config: BffConfig, incoming: URL): URL {
  const url: URL = new URL(config.redirectUri);
  for (const [key, value] of incoming.searchParams) {
    url.searchParams.append(key, value);
  }
  return url;
}

/**
 * Create the six browser authentication and OIDC logout handlers.
 *
 * The login and callback handlers implement authorization code with PKCE and keep tokens in the
 * session store. User reads expose identity and CSRF data; logout handlers clear or revoke
 * sessions. Mount these with the same configuration and store as createApiProxy, or use
 * createWallowBffServer for routing.
 *
 * @param config Server-only OIDC and cookie settings.
 *
 * @param store Shared session store, defaulting to CookieSessionStore. Cookie-only sessions
 * cannot be revoked through back-channel logout.
 *
 * @returns login, callback, user, logout, frontchannelLogout, and backchannelLogout web-standard
 * handlers.
 */
export function createBffHandlers(
  config: BffConfig,
  store: SessionStore = new CookieSessionStore({
    password: config.cookiePasswords ?? config.cookiePassword,
    ttlSeconds: config.sessionTtlSeconds,
  }),
): BffHandlers {
  return {
    login: async (request: Request): Promise<Response> => {
      // `searchParams.get` takes the FIRST value of a repeated parameter, so
      // the guard below — not the parameter parser — is what rejects a value
      // smuggled in behind a safe-looking one.
      const loginUrl: URL = new URL(request.url);
      const returnTo: string = safeReturnTo(loginUrl.searchParams.get("returnTo"));
      // The organization picker's hint. Passed through verbatim: the IdP owns
      // its validation and answers a bad one to this redirect URI as an error.
      const organization: string = loginUrl.searchParams.get("organization")?.trim() ?? "";

      const doc: DiscoveryDoc = await discover(config);
      const { verifier, challenge } = await createPkcePair();
      const state: string = randomUrlSafe(TOKEN_BYTES);
      const nonce: string = randomUrlSafe(TOKEN_BYTES);

      const tx: LoginTx = { state, nonce, verifier, returnTo };
      const sealed: string = await sealTx(tx, config.cookiePasswords ?? config.cookiePassword);

      const headers: Headers = new Headers();
      // Deliberately NOT threaded through `cookieSameSite`: the tx cookie must
      // accompany the top-level redirect back from the IdP — a cross-site
      // navigation — or `state` validation fails on every login. `Lax` permits
      // exactly that and nothing more; `Strict` would not.
      appendCookie(headers, txCookieName(config.cookieName), sealed, {
        ...baseCookieOpts(config.cookieSecure),
        maxAge: TX_COOKIE_MAX_AGE_SECONDS,
      });

      const authorizeUrl: string = buildAuthorizeUrl(config, doc, {
        state,
        codeChallenge: challenge,
        nonce,
        ...(organization === "" ? {} : { organization }),
      });
      return redirect(authorizeUrl, headers);
    },

    callback: async (request: Request): Promise<Response> => {
      const incoming: URL = new URL(request.url);
      const code: string | null = incoming.searchParams.get("code");
      const state: string | null = incoming.searchParams.get("state");

      const txName: string = txCookieName(config.cookieName);
      const sealedTx: string | undefined = requestCookie(request, txName);
      if (sealedTx === undefined || sealedTx === "") {
        return problemResponse(BAD_REQUEST_STATUS, ErrorCode.VALIDATION_FAILED, {
          requestId: resolveRequestId(request.headers),
        });
      }

      const tx: LoginTx | null = await unsealTx(
        sealedTx,
        config.cookiePasswords ?? config.cookiePassword,
      );
      const headers: Headers = new Headers();
      appendCookie(headers, txName, "", clearCookieOpts(config));

      if (tx === null || code === null || state === null || state !== tx.state) {
        return problemResponse(BAD_REQUEST_STATUS, ErrorCode.VALIDATION_FAILED, {
          requestId: resolveRequestId(request.headers),
          headers,
        });
      }

      const doc: DiscoveryDoc = await discover(config);
      const tokens: TokenResponse = await exchangeCode(config, doc, {
        code,
        codeVerifier: tx.verifier,
        currentUrl: callbackUrl(config, incoming),
        state: tx.state,
        nonce: tx.nonce,
      });

      // Base identity from the id_token (carries `sub` and any issuer-specific
      // claims), then overlay the userinfo response — providers such as
      // OpenIddict emit standard claims (`email`, `name`, ...) to userinfo
      // rather than the id_token, so this is what surfaces the user's email.
      let user: BffSession["user"] =
        tokens.id_token !== undefined ? decodeIdTokenClaims(tokens.id_token) : { sub: "" };
      // The OP's session id lives in the id_token only — capture it before the
      // userinfo overlay so a front-channel logout notification can be matched
      // against it later.
      const sid: string | undefined = typeof user.sid === "string" ? user.sid : undefined;
      const info: Record<string, unknown> | null = await fetchUserInfo(doc, tokens.access_token);
      if (info !== null) {
        user = {
          ...user,
          ...info,
          sub: typeof info.sub === "string" ? info.sub : user.sub,
        };
      }
      // Normalize authorization + organization claims into first-class user fields.
      user = mapClaims(user);

      const csrfToken: string = randomUrlSafe(TOKEN_BYTES);
      const session: BffSession = {
        sessionId: randomUrlSafe(TOKEN_BYTES),
        accessToken: tokens.access_token,
        refreshToken: tokens.refresh_token,
        idToken: tokens.id_token,
        expiresAt: Date.now() + tokens.expires_in * MS_PER_SECOND,
        user,
        sid,
        version: INITIAL_SESSION_VERSION,
        csrfToken,
      };
      await writeSession(headers, config, store, session);
      // Double-submit companion: the same synchronizer token, in the one
      // cookie the browser is allowed to read.
      appendCookie(headers, csrfCookieName(config.cookieName), csrfToken, csrfCookieOpts(config));

      // Re-checked here too, so a tx cookie sealed by an older build — or by any
      // path that bypasses the login handler — cannot land the browser on a
      // foreign origin.
      return redirect(safeReturnTo(tx.returnTo), headers);
    },

    user: async (request: Request): Promise<Response> => {
      const session: BffSession | null = await readSession(request, config, store);
      if (session === null) {
        return problemResponse(UNAUTHORIZED_STATUS, ClientErrorCode.BFF_SESSION_MISSING, {
          requestId: resolveRequestId(request.headers),
        });
      }
      // The identity claims plus the CSRF token, for SPA clients that read
      // the token from here rather than from the companion cookie. Session
      // tokens are never part of this shape.
      const body: BffUserResponse = { ...session.user, csrfToken: session.csrfToken };
      // Identity claims and a live CSRF token are per-session secrets: no cache
      // between the browser and here may keep a copy to hand to the next caller.
      return Response.json(body, { headers: { "cache-control": "no-store" } });
    },

    logout: async (request: Request): Promise<Response> => {
      // Logout is a state-changing operation: a bare `GET /bff/logout` made an
      // `<img src="/bff/logout">` on any page enough to revoke the victim's
      // session. Both rejections below destroy NOTHING and clear no cookie — a
      // rejected logout that still cleared them is the same denial of service
      // wearing a 403.
      if (request.method.toUpperCase() !== LOGOUT_METHOD) {
        return problemResponse(METHOD_NOT_ALLOWED_STATUS, ErrorCode.HTTP_METHOD_NOT_ALLOWED, {
          requestId: resolveRequestId(request.headers),
          headers: new Headers({ allow: LOGOUT_METHOD }),
        });
      }

      const session: BffSession | null = await readSession(request, config, store);

      // No session at all — including a cookie this server can no longer unseal
      // after a password rotation or an expired seal — leaves the CSRF gate with
      // nothing to protect, and `csrfTokenMatches(undefined, ...)` is false by
      // construction, so the gate would answer a genuinely empty logout with the
      // same 403 as a cross-site attempt. The request is a no-op that succeeds;
      // the cookies are still cleared, because a jar the server cannot read is
      // exactly the state the user is trying to get out of. This escape hatch is
      // for `session === null` ONLY: a session that exists without a csrfToken
      // falls through to the gate below and stays rejected.
      if (session === null) {
        const headers: Headers = new Headers();
        clearSession(headers, request, config);
        return new Response(null, { status: NO_CONTENT_STATUS, headers });
      }

      // `Headers.get` answers `null` where h3 answered `undefined`; coerce, or
      // the constant-time comparison is handed a value its types deny.
      const presented: string | undefined = request.headers.get(CSRF_HEADER) ?? undefined;
      if (!csrfTokenMatches(session.csrfToken, presented)) {
        return problemResponse(FORBIDDEN_STATUS, ClientErrorCode.BFF_CSRF_INVALID, {
          requestId: resolveRequestId(request.headers),
        });
      }

      // Revoke the session server-side before clearing the browser cookies so a
      // store-backed session cannot be replayed after logout.
      const ref: string | null = readSessionRef(request, config);
      if (ref !== null) {
        await store.destroy(ref);
      }
      const headers: Headers = new Headers();
      clearSession(headers, request, config);

      // Answer the end-session URL in the body, NOT as a redirect: the only
      // client this endpoint can have is `fetch` (the `x-csrf-token` header
      // requirement rules out plain form posts and navigations), and a
      // `redirect: "manual"` fetch response is opaqueredirect — status 0,
      // headers filtered — even same-origin, so a `Location` header is
      // unreadable to the caller that must navigate there. The URL carries
      // `id_token_hint`, so nothing between the browser and here may cache it.
      const doc: DiscoveryDoc = await discover(config);
      const logoutUrl: string = buildLogoutUrl(config, doc, session.idToken);
      headers.set("cache-control", "no-store");
      return Response.json({ logoutUrl }, { status: OK_STATUS, headers });
    },

    frontchannelLogout: async (request: Request): Promise<Response> => {
      // The OP's logout page loads this URL in a hidden iframe when the SSO
      // session ends (OIDC front-channel logout). It is a cross-site GET by
      // design, so it sits outside the CSRF gate — the sid requirement below is
      // what stops a forged teardown: an attacker who can make the browser GET
      // this URL still cannot know the OP-issued session id.
      if (request.method.toUpperCase() !== FRONTCHANNEL_METHOD) {
        return problemResponse(METHOD_NOT_ALLOWED_STATUS, ErrorCode.HTTP_METHOD_NOT_ALLOWED, {
          requestId: resolveRequestId(request.headers),
          headers: new Headers({ allow: FRONTCHANNEL_METHOD }),
        });
      }

      const headers: Headers = new Headers({
        "content-type": "text/html; charset=utf-8",
        "cache-control": "no-store",
      });

      const params: URLSearchParams = new URL(request.url).searchParams;
      const iss: string | null = params.get("iss");
      const sid: string | null = params.get("sid");
      const session: BffSession | null = await readSession(request, config, store);

      // Every miss — no session, a session without a sid, a foreign issuer, a
      // wrong sid — is a silent no-op: the response never distinguishes them.
      const matches: boolean =
        session !== null &&
        session.sid !== undefined &&
        sid !== null &&
        sid === session.sid &&
        iss !== null &&
        normalizeIssuer(iss) === normalizeIssuer(config.issuer);

      if (matches) {
        // Revoke server-side before clearing the browser cookies, mirroring the
        // user-initiated logout above.
        const ref: string | null = readSessionRef(request, config);
        if (ref !== null) {
          await store.destroy(ref);
        }
        clearSession(headers, request, config);
      }

      return new Response(FRONTCHANNEL_PAGE, { status: OK_STATUS, headers });
    },

    // OP-to-BFF POST, no browser involved: lives in its own module because its
    // security model (logout-token verification) shares nothing with the
    // cookie-and-CSRF machinery above.
    backchannelLogout: createBackchannelLogoutHandler(config, store),
  };
}
