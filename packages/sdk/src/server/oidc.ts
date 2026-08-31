/**
 * OIDC helpers for the BFF: discovery (backed by openid-client), authorization
 * URL building, authorization-code exchange, and refresh-token rotation.
 *
 * Discovery resolves the issuer metadata via openid-client's `discovery()` and
 * exposes both the advertised endpoint strings (consumed by the still
 * native-fetch grant helpers below) and the resolved openid-client
 * `Configuration` handle (for the openid-client-backed call sites migrated in
 * later tasks).
 */
import {
  allowInsecureRequests,
  authorizationCodeGrant,
  buildAuthorizationUrl,
  buildEndSessionUrl,
  discovery,
  fetchUserInfo as oidcFetchUserInfo,
  refreshTokenGrant,
  skipSubjectCheck,
  type Configuration,
  type ServerMetadata,
} from "openid-client";

import type { BffConfig } from "./config";

/** Subset of the OpenID Connect discovery document the BFF depends on. */
export interface DiscoveryDoc {
  authorization_endpoint: string;
  token_endpoint: string;
  end_session_endpoint?: string;
  userinfo_endpoint?: string;
  /**
   * The issuer's JWKS endpoint, used to verify back-channel logout tokens.
   * A server-reachable backchannel URL: used exactly as the metadata
   * advertises it, never rebased to the public issuer.
   */
  jwks_uri?: string;
  /** Whether the issuer advertises OIDC back-channel logout support. */
  backchannel_logout_supported?: boolean;
  /**
   * Handle to the resolved openid-client {@link Configuration}. Optional so that
   * plain endpoint-only doc literals (used by the still native-fetch grant
   * helpers and their tests) continue to typecheck. Populated by
   * {@link discover}.
   */
  configuration?: Configuration;
}

/** Token endpoint response for authorization-code and refresh grants. */
export interface TokenResponse {
  access_token: string;
  refresh_token?: string;
  id_token?: string;
  expires_in: number;
  token_type?: string;
}

/** Parameters required to build an authorization request URL. */
export interface AuthorizeParams {
  state: string;
  codeChallenge: string;
  nonce: string;
  /**
   * Organization hint: the IdP runs this organization's enrollment policy and
   * scopes the tokens to it. Omitted, the IdP falls back to the user's single
   * membership or issues an org-less token.
   */
  organization?: string;
}

/** Parameters required to exchange an authorization code for tokens. */
export interface ExchangeCodeParams {
  code: string;
  codeVerifier: string;
  /**
   * Full callback request URL (carrying `code` and `state`). openid-client's
   * {@link authorizationCodeGrant} extracts and validates the authorization
   * response from this URL. Populated by the callback handler.
   */
  currentUrl?: URL;
  /** Expected `state` value carried in the login transaction. */
  state?: string;
  /** Expected `nonce` value carried in the login transaction. */
  nonce?: string;
}

/** Module-level cache of discovery documents keyed on issuer URL. */
const discoveryCache = new Map<string, DiscoveryDoc>();

/**
 * Decide whether openid-client may perform plain-HTTP (insecure) requests for
 * the given discovery URL.
 *
 * The decision must be reachable at RUNTIME in a bundled server: the SDK's
 * server entry is bundled into each app's nitro production build, where
 * Vite's bundler (Rolldown) statically folds build-time environment reads to
 * literals and then constant-folds the branch away entirely. The signal this reads is
 * therefore a value only known at runtime, never a bundler-substitutable one.
 *
 * The signal is the configured discovery URL itself: plain HTTP is permitted
 * exactly when the OP is reached over plain HTTP, which is the actual intent.
 * An HTTPS issuer keeps openid-client's transport check on.
 *
 * @param metadataUrl Absolute URL the discovery document is fetched from.
 */
export function shouldAllowInsecureRequests(metadataUrl: string): boolean {
  return new URL(metadataUrl).protocol === "http:";
}

/** Lifetime assumed when the token response omits `expires_in` (seconds). */
const NO_EXPIRY_SECONDS = 0;

/**
 * Rebase an endpoint onto the FULL public issuer URL — origin *and* path prefix —
 * preserving the endpoint's own path and query.
 *
 * Used to pin the browser-facing endpoints when discovery is fetched from a
 * server-reachable internal host. Rebasing onto the issuer's origin alone is not
 * enough behind a path-based reverse proxy: with issuer `https://wallow.dev/api`
 * the browser must be sent to `https://wallow.dev/api/connect/authorize`, never
 * `https://wallow.dev/connect/authorize`. Taking the issuer's `origin` (rather
 * than assigning `URL.host`) also drops the internal port, which a bare host
 * assignment would otherwise leave in place.
 *
 * The issuer path is prepended only when the endpoint does not already carry it,
 * so a provider running with a matching PathBase (advertising `/api/connect/*`)
 * is not double-prefixed. A trailing slash on the issuer is ignored.
 *
 * @param endpoint Endpoint to rebase: an absolute URL, or a root-relative path
 *   resolved against the issuer's origin.
 * @param issuer Public issuer URL the browser reaches.
 */
function rebaseToIssuer(endpoint: string, issuer: string): string {
  const source: URL = new URL(endpoint, issuer);
  const base: URL = new URL(issuer);
  const issuerPath: string = base.pathname.replace(/\/+$/u, "");
  const alreadyPrefixed: boolean =
    issuerPath !== "" &&
    (source.pathname === issuerPath || source.pathname.startsWith(`${issuerPath}/`));
  const path: string = alreadyPrefixed ? source.pathname : `${issuerPath}${source.pathname}`;
  return `${base.origin}${path}${source.search}`;
}

/**
 * Point an openid-client-built URL at the browser-facing endpoint recorded on the
 * discovery document, keeping the query openid-client encoded exactly as it built it.
 *
 * openid-client builds authorization and end-session URLs from the
 * {@link Configuration}'s own `serverMetadata()`, which holds the RAW discovery
 * response. Under a split horizon that metadata names the internal host, so the
 * URL handed to the browser would be an unreachable container address — the
 * rebasing {@link discover} performed would never reach the user agent. The
 * endpoint strings on the {@link DiscoveryDoc} are the browser-facing ones, so
 * they, not the Configuration, decide where the user agent is sent.
 *
 * The Configuration itself is deliberately left un-rebased: the backchannel
 * token and userinfo calls read from it and must keep reaching the internal host
 * directly rather than hairpinning back through the public proxy.
 *
 * Origin components are replaced individually — including an explicit `port`
 * reset — because assigning `URL.host` a host without a port leaves the previous
 * port in place, which would leak the internal port into the redirect.
 *
 * @param built URL as openid-client constructed it, carrying the request parameters.
 * @param browserEndpoint Browser-facing endpoint from the discovery document.
 */
function pinToBrowserEndpoint(built: URL, browserEndpoint: string | undefined): string {
  if (browserEndpoint === undefined || browserEndpoint === "") {
    return built.toString();
  }

  const target: URL = new URL(browserEndpoint);
  const url: URL = new URL(built.toString());
  url.protocol = target.protocol;
  url.hostname = target.hostname;
  url.port = target.port;
  url.pathname = target.pathname;
  return url.toString();
}

/**
 * Resolve and cache the issuer's discovery document via openid-client.
 *
 * When {@link BffConfig.metadataUrl} is set, discovery is fetched from that
 * server-reachable URL. The backchannel `token_endpoint` (and `userinfo_endpoint`)
 * are used exactly as the metadata advertises them (reachable from the server),
 * while the browser-facing `authorization_endpoint` and `end_session_endpoint`
 * are pinned to the full public {@link BffConfig.issuer} URL — origin *and* path
 * prefix — so the user agent can follow the redirects (see
 * {@link rebaseToIssuer}).
 *
 * This handles OpenID providers (such as OpenIddict) that derive their endpoint
 * URIs from the incoming request base rather than the configured issuer: when
 * discovery is fetched from the internal host, every advertised endpoint points
 * at that internal host and — behind a path-based reverse proxy — omits the
 * public path prefix, so the interactive endpoints must be re-pinned to the
 * public issuer.
 *
 * Insecure (plain HTTP) requests are permitted for both the discovery request
 * and the resolved {@link Configuration} exactly when the URL discovery is
 * fetched from is itself plain HTTP — see {@link shouldAllowInsecureRequests}.
 *
 * @param config BFF configuration providing the issuer and optional metadata URL.
 */
export async function discover(config: BffConfig): Promise<DiscoveryDoc> {
  const metadataUrl: string =
    config.metadataUrl ?? `${config.issuer}/.well-known/openid-configuration`;

  const cached: DiscoveryDoc | undefined = discoveryCache.get(metadataUrl);
  if (cached !== undefined) {
    return cached;
  }

  const allowInsecure: boolean = shouldAllowInsecureRequests(metadataUrl);
  const configuration: Configuration = await discovery(
    new URL(metadataUrl),
    config.clientId,
    config.clientSecret,
    undefined,
    allowInsecure ? { execute: [allowInsecureRequests] } : undefined,
  );

  const metadata: Readonly<ServerMetadata> = configuration.serverMetadata();

  const authorizationEndpoint: string = metadata.authorization_endpoint ?? "";
  const tokenEndpoint: string = metadata.token_endpoint ?? "";
  const endSessionEndpoint: string | undefined = metadata.end_session_endpoint;
  const userinfoEndpoint: string | undefined = metadata.userinfo_endpoint;

  let doc: DiscoveryDoc = {
    authorization_endpoint: authorizationEndpoint,
    token_endpoint: tokenEndpoint,
    end_session_endpoint: endSessionEndpoint,
    userinfo_endpoint: userinfoEndpoint,
    // Backchannel endpoints and capabilities, taken as advertised: the JWKS is
    // fetched server-side, so it must keep naming the server-reachable host.
    jwks_uri: metadata.jwks_uri,
    backchannel_logout_supported: metadata.backchannel_logout_supported === true ? true : undefined,
    configuration,
  };

  if (config.metadataUrl !== undefined) {
    doc = {
      ...doc,
      authorization_endpoint: rebaseToIssuer(authorizationEndpoint, config.issuer),
      end_session_endpoint:
        endSessionEndpoint !== undefined
          ? rebaseToIssuer(endSessionEndpoint, config.issuer)
          : endSessionEndpoint,
    };
  }

  discoveryCache.set(metadataUrl, doc);
  return doc;
}

/**
 * Build the authorization request URL including PKCE, state, nonce, and scopes.
 *
 * Delegates to openid-client's {@link buildAuthorizationUrl} using the resolved
 * {@link Configuration} carried on the discovery {@link DiscoveryDoc}, so the
 * authorization request is constructed and encoded by openid-client rather than
 * hand-rolled. PKCE uses S256.
 *
 * The result is then pinned to the doc's browser-facing
 * `authorization_endpoint` — this URL becomes a redirect the user agent has to
 * follow, so it must name the public issuer rather than the internal discovery
 * host. See {@link pinToBrowserEndpoint}.
 */
export function buildAuthorizeUrl(
  config: BffConfig,
  doc: DiscoveryDoc,
  params: AuthorizeParams,
): string {
  const url: URL = buildAuthorizationUrl(doc.configuration!, {
    response_type: "code",
    client_id: config.clientId,
    redirect_uri: config.redirectUri,
    scope: config.scopes.join(" "),
    state: params.state,
    code_challenge: params.codeChallenge,
    code_challenge_method: "S256",
    nonce: params.nonce,
    ...(params.organization === undefined ? {} : { organization: params.organization }),
  });
  return pinToBrowserEndpoint(url, doc.authorization_endpoint);
}

/**
 * Exchange an authorization code (plus PKCE verifier) for tokens.
 *
 * Delegates to openid-client's {@link authorizationCodeGrant} using the resolved
 * {@link Configuration} carried on the discovery {@link DiscoveryDoc}. Passing
 * the full callback URL along with the expected `state`, expected `nonce`, and
 * PKCE verifier lets openid-client validate the authorization response and the
 * id_token (signature, `iss`/`aud`/`exp`, and nonce) — protections the prior
 * native-fetch token POST lacked. The `config` parameter is retained for
 * signature parity with the other grant helpers.
 */
export async function exchangeCode(
  config: BffConfig,
  doc: DiscoveryDoc,
  params: ExchangeCodeParams,
): Promise<TokenResponse> {
  void config;
  const tokens: Awaited<ReturnType<typeof authorizationCodeGrant>> = await authorizationCodeGrant(
    doc.configuration!,
    params.currentUrl!,
    {
      expectedState: params.state,
      expectedNonce: params.nonce,
      pkceCodeVerifier: params.codeVerifier,
    },
  );

  return {
    access_token: tokens.access_token,
    refresh_token: tokens.refresh_token,
    id_token: tokens.id_token,
    expires_in: tokens.expires_in ?? NO_EXPIRY_SECONDS,
    token_type: tokens.token_type,
  };
}

/**
 * Exchange a refresh token for a fresh set of tokens.
 *
 * Delegates to openid-client's {@link refreshTokenGrant} using the resolved
 * {@link Configuration} carried on the discovery {@link DiscoveryDoc}. This
 * surfaces the provider's rotated `refresh_token` (when refresh-token rotation
 * is enabled) so the caller can persist the new token, and lets openid-client
 * validate the refreshed id_token — protections the prior native-fetch token
 * POST lacked. The `config` parameter is retained for signature parity with the
 * other grant helpers. Callers invoke this inside
 * `store.withRefreshLock` so the rotating token is protected against concurrent
 * refreshes.
 */
export async function refreshTokens(
  config: BffConfig,
  doc: DiscoveryDoc,
  refreshToken: string,
): Promise<TokenResponse> {
  void config;
  const tokens: Awaited<ReturnType<typeof refreshTokenGrant>> = await refreshTokenGrant(
    doc.configuration!,
    refreshToken,
  );

  return {
    access_token: tokens.access_token,
    refresh_token: tokens.refresh_token,
    id_token: tokens.id_token,
    expires_in: tokens.expires_in ?? NO_EXPIRY_SECONDS,
    token_type: tokens.token_type,
  };
}

/**
 * Fetch the resolved identity claims from the issuer's userinfo endpoint.
 *
 * This is a backchannel call made with the confidential BFF's access token, so
 * the endpoint is used exactly as the metadata advertises it (server-reachable,
 * not rewritten to the browser-facing origin). Providers such as OpenIddict may
 * only emit standard identity claims (`email`, `name`, ...) to userinfo rather
 * than the id_token, so the BFF resolves the user identity from here.
 *
 * @param doc Discovery document providing the userinfo endpoint.
 * @param accessToken Bearer access token authorizing the userinfo request.
 * @returns The parsed claims object, or `null` when no userinfo endpoint is
 *          advertised or the request fails.
 */
export async function fetchUserInfo(
  doc: DiscoveryDoc,
  accessToken: string,
): Promise<Record<string, unknown> | null> {
  if (doc.userinfo_endpoint === undefined || doc.userinfo_endpoint === "") {
    return null;
  }

  // The subject is not yet known at this point in the flow, so skip the
  // expected-subject check; openid-client still validates the response shape.
  const claims: Awaited<ReturnType<typeof oidcFetchUserInfo>> = await oidcFetchUserInfo(
    doc.configuration!,
    accessToken,
    skipSubjectCheck,
  );

  return claims as unknown as Record<string, unknown>;
}

/**
 * Build the RP-initiated logout (end-session) URL.
 *
 * When the issuer advertises an `end_session_endpoint`, delegates to
 * openid-client's {@link buildEndSessionUrl} using the resolved
 * {@link Configuration} carried on the discovery {@link DiscoveryDoc}, forwarding
 * `post_logout_redirect_uri` and an optional `id_token_hint`. When it does not,
 * falls back to `<issuer>/connect/logout` (Appendix A) carrying the same
 * parameters so providers without a discovery-advertised end-session endpoint
 * (such as OpenIddict) still terminate the upstream session. The fallback is
 * rebased onto the full issuer URL, keeping any path prefix the issuer carries.
 *
 * @param config BFF configuration providing the issuer and post-logout redirect.
 * @param doc Discovery document providing the end-session endpoint (if any) and
 *   the resolved openid-client {@link Configuration} handle.
 * @param idTokenHint The current session's id_token, forwarded as `id_token_hint`.
 * @returns The absolute logout URL as a string.
 */
export function buildLogoutUrl(config: BffConfig, doc: DiscoveryDoc, idTokenHint?: string): string {
  if (doc.end_session_endpoint !== undefined) {
    const params: Record<string, string> = {
      post_logout_redirect_uri: config.postLogoutRedirectUri,
    };
    if (idTokenHint !== undefined) {
      params.id_token_hint = idTokenHint;
    }
    // Pinned for the same reason as the authorize URL: the browser follows it.
    return pinToBrowserEndpoint(
      buildEndSessionUrl(doc.configuration!, params),
      doc.end_session_endpoint,
    );
  }

  const url: URL = new URL(rebaseToIssuer("/connect/logout", config.issuer));
  url.searchParams.set("post_logout_redirect_uri", config.postLogoutRedirectUri);
  if (idTokenHint !== undefined) {
    url.searchParams.set("id_token_hint", idTokenHint);
  }
  return url.toString();
}
